/* LuaCorp - AGPLv3. Copyright (c) 2026 LuaCorp Contributors. See AGPLv3.txt. */
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Content.IntegrationTests;
using Content.MapRenderer.Painters;
using Content.Server._LuaM.Guidebook;
using Robust.Shared.GameObjects;
using Robust.UnitTesting.Pool;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Content.MapRenderer._LuaM;

internal static class GuidebookImageBuilder
{
    public static async Task<int> Run(string[] args)
    {
        if (!Content.Shared._LuaM.Guidebook.GuidebookMapRendering.Enabled)
        { Console.WriteLine("Guidebook map rendering is temporarily disabled."); return 0; }
        Environment.SetEnvironmentVariable("COLOSS_GUIDEBOOK_WORKER", "1");
        string? Arg(string name) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
        var tools = AppContext.BaseDirectory;
        var root = Path.GetFullPath(Arg("--resources") ?? Path.Combine(tools, "../../Resources"));
        var fallback = Path.GetFullPath(Path.Combine(tools, "../../Resources"));
        var cache = GuidebookRenderFiles.Cache(root);
        Directory.CreateDirectory(cache);
        FileStream? lease = null;
        for (var i = 0; i < 3600 && lease == null; i++)
        {
            try { lease = new FileStream(Path.Combine(cache, "worker.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { await Task.Delay(500); }
        }
        if (lease == null) { Console.Error.WriteLine("Guidebook renderer is already busy."); return 1; }
        using (lease)
        {
            try
            {
                var assets = GuidebookRenderFiles.AssetsVersion(root, fallback, tools);
                var index = GuidebookRenderFiles.ReadIndex(cache) ?? new GuidebookRenderFiles.Index();
                var discover = index.Assets != assets || index.Format != GuidebookRenderFiles.FormatVersion || index.Maps.Count == 0;
                PoolManager.ResourceMounts.Add(fallback);
                PoolManager.ResourceMounts.Add(root);
                var engineResources = Path.GetFullPath(Path.Combine(tools, "../../RobustToolbox/Resources"));
                if (Directory.Exists(engineResources)) PoolManager.ResourceMounts.Insert(0, engineResources);
                var started = false;
                void Start() { if (started) return; PoolManager.Startup(); started = true; }
                var maps = index.Maps.Keys.ToHashSet(StringComparer.Ordinal);
                if (discover)
                {
                    Start();
                    var context = new ExternalTestContext("Guidebook.MapRenderer.Discovery", Console.Out);
                    await using (var pair = await PoolManager.GetServerClient(new PoolSettings { NoLoadTestPrototypes = true }, context))
                    {
                        await pair.Server.WaitPost(() => maps = pair.Server.ResolveDependency<IEntityManager>()
                            .System<GuidebookCatalogSystem>().BuildSnapshot().MapNames.Keys.ToHashSet(StringComparer.Ordinal));
                        await pair.CleanReturnAsync();
                    }
                    // Explicit guide plans also include event grids not currently spawned by the preset.
                    var guides = Path.Combine(root, "ServerInfo");
                    if (!Directory.Exists(guides)) guides = Path.Combine(fallback, "ServerInfo");
                    if (Directory.Exists(guides))
                        foreach (var file in Directory.EnumerateFiles(guides, "*.xml", SearchOption.AllDirectories))
                            foreach (Match match in GuidebookRenderFiles.GuideMapRegex.Matches(File.ReadAllText(file))) maps.Add(match.Groups[1].Value);
                    index.Format = GuidebookRenderFiles.FormatVersion;
                    index.Assets = assets;
                    maps.RemoveWhere(p => p.StartsWith("/Maps/Test/", StringComparison.Ordinal));
                    foreach (var old in index.Maps.Keys.Except(maps).ToArray()) index.Maps.Remove(old);
                    foreach (var path in maps) index.Maps.TryAdd(path, new());
                    GuidebookRenderFiles.WriteIndex(cache, index);
                }
                var only = Arg("--only");
                var selected = only?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
                var pending = maps.Where(path => selected == null || selected.Contains(path)).OrderBy(p => p, StringComparer.Ordinal).ToList();
                var failures = 0;
                while (pending.Count > 0)
                {
                    // Requests can bring a station/ship to the front of an initial full generation.
                    string? priority = null;
                    try { priority = File.ReadAllText(Path.Combine(cache, "priority.txt")).Trim(); } catch (IOException) { }
                    var path = priority != null && pending.Contains(priority) ? priority : pending[0];
                    pending.Remove(path);
                    var source = Path.GetFullPath(Path.Combine(root, path.TrimStart('/')));
                    if (!source.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(source))
                    { Console.Error.WriteLine("Missing guidebook map: " + path); failures++; continue; }
                    var version = GuidebookRenderFiles.MapVersion(assets, source);
                    var old = index.Maps[path];
                    if (old.Version == version && old.Parts.Count > 0 && old.Parts.Select((hash, part) =>
                            File.Exists(GuidebookRenderFiles.ImagePath(cache, version, part))
                            && GuidebookRenderFiles.Hash(File.ReadAllBytes(GuidebookRenderFiles.ImagePath(cache, version, part))) == hash).All(valid => valid)) continue;
                    Start();
                    Console.WriteLine("Guidebook MapRender: " + path);
                    try
                    {
                        var entry = new GuidebookRenderFiles.Entry { Version = version };
                        await foreach (var rendered in MapPainter.PaintGuidebookGrid(path))
                        {
                            using var image = rendered.Image;
                            if (image.Width > 8192 || image.Height > 8192)
                                image.Mutate(c => c.Resize(new ResizeOptions { Mode = ResizeMode.Max, Size = new Size(8192, 8192) }));
                            var destination = GuidebookRenderFiles.ImagePath(cache, version, entry.Parts.Count);
                            var temporary = destination + ".tmp";
                            await image.SaveAsPngAsync(temporary);
                            File.Move(temporary, destination, true);
                            entry.Parts.Add(GuidebookRenderFiles.Hash(File.ReadAllBytes(destination)));
                        }
                        if (entry.Parts.Count == 0) throw new InvalidDataException("MapRenderer produced no grids.");
                        // Do not publish a render if files changed while the worker was drawing it.
                        if (GuidebookRenderFiles.MapVersion(assets, source) != version) throw new IOException("Map changed during rendering.");
                        index.Maps[path] = entry;
                        GuidebookRenderFiles.WriteIndex(cache, index);
                    }
                    catch (Exception e) { failures++; Console.Error.WriteLine("Guidebook render failed for " + path + ": " + e); }
                }
                if (started) PoolManager.Shutdown();
                if (GuidebookRenderFiles.AssetsVersion(root, fallback, tools) != assets)
                    throw new IOException("Render inputs changed during generation; run again before publishing.");
                if (failures == 0 && only == null)
                {
                    var retained = index.Maps.Values.Where(e => GuidebookRenderFiles.IsVersion(e.Version))
                        .SelectMany(e => e.Parts.Select((_, i) => GuidebookRenderFiles.ImagePath(cache, e.Version, i)))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    // This private cache owns only map-*.png files; never recurse or
                    // delete source maps. Readers may still be finishing an old transfer.
                    foreach (var file in Directory.EnumerateFiles(cache, "map-*.png"))
                        if (!retained.Contains(file)) try { File.Delete(file); } catch (IOException) { }
                }
                Console.WriteLine("Guidebook MapRender complete; errors: " + failures);
                return failures == 0 ? 0 : 1;
            }
            catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        }
    }
}
