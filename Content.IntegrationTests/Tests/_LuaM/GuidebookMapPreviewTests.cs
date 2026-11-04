/* LuaCorp - AGPLv3. Copyright (c) 2026 LuaCorp Contributors. See AGPLv3.txt. */
using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using Content.Client._LuaM.Guidebook;
using Content.Server._LuaM.Guidebook;
using Content.Shared._LuaM.Guidebook;
using Robust.Shared.GameObjects;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class GuidebookMapPreviewTests
{
    private static byte[] Png()
    {
        using var image = new Image<Rgba32>(2, 2, new Rgba32(255, 0, 0, 255));
        using var stream = new MemoryStream(); image.SaveAsPng(stream); return stream.ToArray();
    }

    [Test]
    public void ServerNeverReturnsAnImageForChangedMapOrAssets()
    {
        var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
        var root = Path.Combine(temp, "coloss-guide-render-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Maps"));
        try
        {
            var source = Path.Combine(root, "Maps/example.yml"); File.WriteAllText(source, "map: original\n");
            var cache = GuidebookRenderFiles.Cache(root); Directory.CreateDirectory(cache);
            var version = GuidebookRenderFiles.MapVersion("assets-A", source); var bytes = Png();
            var imageHash = GuidebookRenderFiles.Hash(bytes);
            File.WriteAllBytes(GuidebookRenderFiles.ImagePath(cache, version, 0), bytes);
            GuidebookRenderFiles.WriteIndex(cache, new() { Assets = "assets-A", Maps = new()
                { ["/Maps/example.yml"] = new() { Version = version, Parts = new() { imageHash } } } });
            var method = typeof(GuidebookMapImageSystem).GetMethod("Read", BindingFlags.NonPublic | BindingFlags.Static)!;
            (GuidebookMapImageChunk Header, byte[] Bytes) Read(string assets, string known = "")
            {
                var result = method.Invoke(null, new object[] { root, assets, new RequestGuidebookMapImage("/Maps/example.yml", 1, known) })!;
                return ((GuidebookMapImageChunk) result.GetType().GetField("Header")!.GetValue(result)!, (byte[]) result.GetType().GetField("Bytes")!.GetValue(result)!);
            }
            Assert.That(Read("assets-A").Bytes, Is.EqualTo(bytes));
            Assert.That(Read("assets-A", imageHash).Header.Total, Is.Zero, "A current client cache needs no duplicate PNG transfer.");
            File.WriteAllText(source, "map: changed\n");
            Assert.That(Read("assets-A").Header.Status, Is.EqualTo("pending"));
            Assert.That(Read("assets-A").Bytes, Is.Empty, "An old image must never substitute for a changed map.");
            File.WriteAllText(source, "map: original\n");
            Assert.That(Read("assets-B").Header.Status, Is.EqualTo("revalidate"));
            Assert.That(Read("assets-B").Bytes, Is.Empty);
        }
        finally
        {
            if (string.Equals(Directory.GetParent(root)!.FullName, temp, StringComparison.OrdinalIgnoreCase)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task PngPreviewDoesNotSpawnMapsAndIgnoresCancelledTransfers()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.ResolveDependency<IEntityManager>();
            var preview = entities.System<GuidebookMapPreviewSystem>();
            var before = entities.EntityCount;
            if (!GuidebookMapRendering.Enabled)
            {
                preview.Open("/SharedMaps/_Mono/Shuttles/World/bracket.yml", "Bracket");
                Assert.That(preview.IsLoading, Is.False);
                Assert.That(typeof(GuidebookMapPreviewSystem).GetField("_window", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(preview), Is.Null);
                Assert.That(entities.EntityCount, Is.EqualTo(before));
                return;
            }
            var receive = typeof(GuidebookMapPreviewSystem).GetMethod("Receive", BindingFlags.NonPublic | BindingFlags.Instance)!;
            int Request() => (int) typeof(GuidebookMapPreviewSystem).GetField("_request", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(preview)!;
            void Deliver(GuidebookMapImageChunk chunk) => receive.Invoke(preview, new object[] { chunk });
            var bytes = Png(); var hash = GuidebookRenderFiles.Hash(bytes);
            preview.Open("/SharedMaps/_Mono/Shuttles/World/bracket.yml", "Bracket");
            Deliver(new(Request(), hash, 0, bytes.Length, bytes));
            Assert.That(preview.IsLoading, Is.False);
            Assert.That(typeof(GuidebookMapPreviewSystem).GetField("_texture", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(preview), Is.Not.Null);
            Assert.That(entities.EntityCount, Is.EqualTo(before), "No YAML loading, preview entities or simulated machinery.");
            preview.Close();
            preview.Open("/SharedMaps/_Mono/Shuttles/World/bracket.yml", "Bracket");
            var cancelled = Request(); var half = bytes.Length / 2;
            Deliver(new(cancelled, hash, 0, bytes.Length, bytes[..half]));
            preview.Close();
            Deliver(new(cancelled, hash, half, bytes.Length, bytes[half..]));
            Assert.That(preview.IsLoading, Is.False);
            Assert.That(entities.EntityCount, Is.EqualTo(before));
        });
        await pair.CleanReturnAsync();
    }
}
