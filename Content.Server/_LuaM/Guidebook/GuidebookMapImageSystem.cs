/* LuaCorp - AGPLv3. Copyright (c) 2026 LuaCorp Contributors. See AGPLv3.txt. */
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared._LuaM.Guidebook;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._LuaM.Guidebook;

/// <summary>Only cached PNGs leave this system. MapRenderer runs in its own process.</summary>
public sealed partial class GuidebookMapImageSystem : EntitySystem
{
    private const int ChunkSize = 32 * 1024;
    private const int MaxImageBytes = 48 * 1024 * 1024;
    private string? _root, _tools, _fallback;
    private string _assets = "";
    private int _revision, _validated = -1, _announced;
    private Task<string>? _validation;
    private Task<(int Code, string Error)>? _worker;
    private Process? _process;
    private DateTime _retryWorker, _notifyAfter, _validateAfter;
    private readonly List<FileSystemWatcher> _watchers = new();
    private volatile bool _watchersHealthy = true;
    private sealed class Transfer
    {
        public required GuidebookMapImageChunk Header;
        public byte[] Bytes = Array.Empty<byte>();
        public int Offset;
    }
    private sealed class Pending
    {
        public int Revision;
        public required RequestGuidebookMapImage Request;
        public required Task<Transfer> Task;
        public Transfer? Transfer;
    }
    private readonly Dictionary<ICommonSession, Pending> _pending = new();
    private readonly Dictionary<ICommonSession, DateTime> _last = new();
    private static readonly Dictionary<string, WeakReference<byte[]>> ImageBuffers = new();
    private static byte[] ImageBytes(string path, string imageHash)
    {
        // Several readers of the same plan share the compressed buffer. The weak
        // reference does not keep unused PNGs resident after transfers finish.
        lock (ImageBuffers)
        {
            var key = path + "|" + imageHash;
            if (ImageBuffers.TryGetValue(key, out var cached) && cached.TryGetTarget(out var bytes)) return bytes;
            if (new FileInfo(path).Length > MaxImageBytes) throw new InvalidDataException("MapRender is too large.");
            bytes = File.ReadAllBytes(path);
            ImageBuffers[key] = new(bytes);
            if (ImageBuffers.Count > 16)
                foreach (var old in ImageBuffers.Where(p => !p.Value.TryGetTarget(out _)).Select(p => p.Key).ToArray()) ImageBuffers.Remove(old);
            return bytes;
        }
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RequestGuidebookMapImage>(OnRequest);
        if (!GuidebookMapRendering.Enabled) return;
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(_ => Interlocked.Increment(ref _revision));
        // The renderer itself starts an isolated test server. Never recurse into another worker.
        if (Environment.GetEnvironmentVariable("COLOSS_GUIDEBOOK_WORKER") == "1"
            || AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Content.IntegrationTests")) return;
        var roots = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "Resources"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../Resources")),
            Path.Combine(AppContext.BaseDirectory, "Resources"),
        };
        _root = roots.FirstOrDefault(p => Directory.Exists(Path.Combine(p, "Prototypes"))
            && (Directory.Exists(Path.Combine(p, "Maps")) || Directory.Exists(Path.Combine(p, "SharedMaps"))));
        if (_root == null) { Log.Error("Guidebook MapRender: resource directory not found."); return; }
        _root = Path.GetFullPath(_root);
        _tools = new[]
        {
            Path.GetFullPath(Path.Combine(_root, "../bin/Content.MapRenderer")),
            Path.GetFullPath(Path.Combine(_root, "../GuidebookTools/bin/Content.MapRenderer")),
        }.FirstOrDefault(p => File.Exists(Path.Combine(p, "Content.MapRenderer.dll")));
        if (_tools == null) { Log.Error("Guidebook MapRender worker is missing; package the renderer with the server."); return; }
        _fallback = Path.GetFullPath(Path.Combine(_tools, "../../Resources"));
        foreach (var folder in new[] { "Prototypes", "Maps", "SharedMaps", "Textures", "Shaders", "ServerInfo" })
        {
            var directory = Path.Combine(_root, folder);
            if (!Directory.Exists(directory)) directory = Path.Combine(_fallback, folder);
            if (!Directory.Exists(directory)) continue;
            try
            {
                var watcher = new FileSystemWatcher(directory) { IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size };
                watcher.Changed += (_, _) => Interlocked.Increment(ref _revision);
                watcher.Created += (_, _) => Interlocked.Increment(ref _revision);
                watcher.Deleted += (_, _) => Interlocked.Increment(ref _revision);
                watcher.Renamed += (_, _) => Interlocked.Increment(ref _revision);
                watcher.Error += (_, _) => { _watchersHealthy = false; Interlocked.Increment(ref _revision); };
                _watchers.Add(watcher);
                watcher.EnableRaisingEvents = true;
            }
            catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
            { _watchersHealthy = false; Log.Warning($"Guidebook watcher unavailable; requests will verify assets directly: {e.Message}"); }
        }
    }

    private void OnRequest(RequestGuidebookMapImage request, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!GuidebookMapRendering.Enabled)
        { Reply(session, new(request.Request, "", 0, 0, Array.Empty<byte>(), "disabled")); return; }
        var now = DateTime.UtcNow;
        if (_last.TryGetValue(session, out var previous) && now - previous < TimeSpan.FromSeconds(1)) return;
        _last[session] = now;
        if (_root == null || _tools == null || _fallback == null)
        { Reply(session, new(request.Request, "", 0, 0, Array.Empty<byte>(), "unavailable")); return; }
        if (_validated != _revision)
        { Reply(session, new(request.Request, "", 0, 0, Array.Empty<byte>(), "pending")); return; }
        if (request.Part < 0 || request.Part > 100 || request.Path.Length > 400 || request.Path.Contains("..")
            || request.Path.Contains('\\') || !(request.Path.StartsWith("/Maps/") || request.Path.StartsWith("/SharedMaps/"))) return;
        if (_validated != _revision || _assets.Length == 0)
        { Reply(session, new(request.Request, "", 0, 0, Array.Empty<byte>(), "pending")); return; }
        var root = _root; var assets = _assets;
        var fallback = _fallback; var tools = _tools; var healthy = _watchersHealthy;
        _pending[session] = new() { Revision = _revision, Request = request, Task = Task.Run(() =>
        {
            if (!healthy && GuidebookRenderFiles.AssetsVersion(root, fallback, tools) != assets)
                return new Transfer { Header = new(request.Request, "", 0, 0, Array.Empty<byte>(), "revalidate") };
            return Read(root, assets, request);
        }) };
    }

    private static Transfer Read(string root, string assets, RequestGuidebookMapImage request)
    {
        Transfer Status(string status) => new() { Header = new(request.Request, "", 0, 0, Array.Empty<byte>(), status) };
        try
        {
            var cache = GuidebookRenderFiles.Cache(root);
            var index = GuidebookRenderFiles.ReadIndex(cache);
            if (index == null) return Status("pending");
            if (index.Format != GuidebookRenderFiles.FormatVersion) return Status("pending");
            if (index.Assets != assets) return Status("revalidate");
            // The generated index is the whitelist. Arbitrary server grids cannot be requested.
            if (!index.Maps.TryGetValue(request.Path, out var entry)) return Status("unknown");
            var source = Path.Combine(root, request.Path.TrimStart('/'));
            if (!File.Exists(source)) return Status("unknown");
            var version = GuidebookRenderFiles.MapVersion(assets, source);
            if (entry.Version != version || entry.Parts.Count == 0)
            {
                File.WriteAllText(Path.Combine(cache, "priority.txt"), request.Path);
                return Status("pending");
            }
            if (request.Part >= entry.Parts.Count || !GuidebookRenderFiles.IsVersion(version)) return Status("unknown");
            var bytes = ImageBytes(GuidebookRenderFiles.ImagePath(cache, version, request.Part), entry.Parts[request.Part]);
            var hash = GuidebookRenderFiles.Hash(bytes);
            if (bytes.Length > MaxImageBytes || hash != entry.Parts[request.Part]) return Status("unavailable");
            return new() { Header = new(request.Request, hash, 0, request.KnownVersion == hash ? 0 : bytes.Length, Array.Empty<byte>()) { Parts = entry.Parts.Count },
                Bytes = request.KnownVersion == hash ? Array.Empty<byte>() : bytes };
        }
        catch (InvalidDataException) { return Status("unavailable"); }
        catch (Exception e) when (e is IOException or System.Text.Json.JsonException) { return Status("pending"); }
    }

    // All Result reads below are guarded by IsCompletedSuccessfully. No task is
    // synchronously waited on the game thread; only completed work is collected.
#pragma warning disable RA0004
    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_root == null || _tools == null || _fallback == null) return;
        if (_validated != _revision && _validation == null && DateTime.UtcNow >= _validateAfter)
        {
            var root = _root; var fallback = _fallback; var tools = _tools; var revision = _revision;
            _validation = Task.Run(() => GuidebookRenderFiles.AssetsVersion(root, fallback, tools));
            _validationRevision = revision;
        }
        if (_validation is { IsCompleted: true })
        {
            if (_validation.IsCompletedSuccessfully)
            { _assets = _validation.Result; _validated = _validationRevision; _retryWorker = DateTime.MinValue; }
            else
            {
                Log.Warning("Guidebook source validation failed: " + _validation.Exception?.GetBaseException().Message);
                _validateAfter = DateTime.UtcNow.AddSeconds(3);
            }
            _validation = null;
        }
        if (_announced != _revision && DateTime.UtcNow >= _notifyAfter)
        {
            _announced = _revision; _notifyAfter = DateTime.UtcNow.AddSeconds(1);
            EntityManager.System<GuidebookCatalogSystem>().InvalidateResources();
            RaiseNetworkEvent(new GuidebookImagesInvalidatedEvent());
        }
        if (_worker is { IsCompleted: true })
        {
            if (!_worker.IsCompletedSuccessfully || _worker.Result.Code != 0)
            { Log.Warning("Guidebook MapRender: " + (_worker.IsCompletedSuccessfully ? _worker.Result.Error : _worker.Exception?.GetBaseException().Message)); _retryWorker = DateTime.UtcNow.AddMinutes(1); }
            _worker = null; _process?.Dispose(); _process = null;
        }
        if (_worker == null && _validated == _revision && DateTime.UtcNow >= _retryWorker)
        {
            _retryWorker = DateTime.MaxValue;
            StartWorker();
        }
        foreach (var session in _last.Keys.Where(s => !s.Channel.IsConnected).ToArray()) _last.Remove(session);
        var chunkBudget = 4;
        foreach (var (session, pending) in _pending.ToArray())
        {
            if (!session.Channel.IsConnected) { _pending.Remove(session); _last.Remove(session); continue; }
            if (pending.Revision != _revision)
            { Reply(session, new(pending.Request.Request, "", 0, 0, Array.Empty<byte>(), "pending")); _pending.Remove(session); continue; }
            if (!pending.Task.IsCompleted) continue;
            if (!pending.Task.IsCompletedSuccessfully) { _pending.Remove(session); continue; }
            pending.Transfer ??= pending.Task.Result;
            var transfer = pending.Transfer; var header = transfer.Header;
            if (header.Status == "unknown" && _worker != null) header.Status = "pending";
            if (header.Status == "revalidate")
            { if (_worker == null) Interlocked.Increment(ref _revision); header.Status = "pending"; }
            if (header.Status == "pending" && _worker == null && _retryWorker == DateTime.MaxValue)
                _retryWorker = DateTime.UtcNow.AddSeconds(2);
            if (header.Status.Length > 0 || transfer.Bytes.Length == 0)
            { Reply(session, header); _pending.Remove(session); continue; }
            for (var i = 0; i < 2 && chunkBudget > 0 && transfer.Offset < transfer.Bytes.Length; i++, chunkBudget--)
            {
                var count = Math.Min(ChunkSize, transfer.Bytes.Length - transfer.Offset);
                var chunk = new byte[count]; Array.Copy(transfer.Bytes, transfer.Offset, chunk, 0, count);
                Reply(session, new(header.Request, header.Version, transfer.Offset, transfer.Bytes.Length, chunk) { Parts = header.Parts });
                transfer.Offset += count;
            }
            if (transfer.Offset == transfer.Bytes.Length) _pending.Remove(session);
            else { _pending.Remove(session); _pending.Add(session, pending); }
        }
    }
#pragma warning restore RA0004
    private int _validationRevision;
    private void Reply(ICommonSession session, GuidebookMapImageChunk data) => RaiseNetworkEvent(data, session);
    private void StartWorker()
    {
        var info = new ProcessStartInfo("dotnet") { WorkingDirectory = Path.GetFullPath(Path.Combine(_root!, "..")),
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden };
        info.ArgumentList.Add(Path.Combine(_tools!, "Content.MapRenderer.dll"));
        info.ArgumentList.Add("--guidebook"); info.ArgumentList.Add("--resources"); info.ArgumentList.Add(_root!);
        try
        {
            _process = Process.Start(info)!;
            try { _process.PriorityClass = ProcessPriorityClass.BelowNormal; } catch (Exception e) when (e is System.ComponentModel.Win32Exception or PlatformNotSupportedException) { }
            var process = _process;
            _worker = Task.Run(async () =>
            {
                static async Task<string> Drain(StreamReader reader, bool remember)
                {
                    var tail = "";
                    while (await reader.ReadLineAsync() is { } line)
                        if (remember) { tail += "\n" + line; if (tail.Length > 3000) tail = tail[^3000..]; }
                    return tail;
                }
                var output = Drain(process.StandardOutput, false); var error = Drain(process.StandardError, true);
                await process.WaitForExitAsync(); await output;
                var message = await error;
                return (process.ExitCode, message.Length > 3000 ? message[^3000..] : message);
            });
        }
        catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception)
        {
            Log.Error("Cannot start guidebook MapRenderer: " + e.Message);
            _retryWorker = DateTime.UtcNow.AddMinutes(1);
        }
    }
    public override void Shutdown()
    {
        foreach (var watcher in _watchers) watcher.Dispose();
        _watchers.Clear(); _pending.Clear(); _last.Clear();
        try { if (_process is { HasExited: false }) _process.Kill(true); } catch (InvalidOperationException) { }
        base.Shutdown();
    }
}
