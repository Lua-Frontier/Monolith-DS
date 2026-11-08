using System.Linq;
using System.Numerics;
using Content.Client.Viewport;
using Content.Shared._NF.Shipyard.Prototypes;
using Content.Shared.GameTicking;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Events;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Client._LuaM.Shipyard;

/// <summary>
/// Layout data read from the loaded shuttle grid.
/// </summary>
public sealed record ShipyardPreviewInfo(Vector2i Dimensions, int Tiles);

/// <summary>
/// A cached shuttle picture: the whole grid rendered once.
/// </summary>
public sealed class ShipyardPreviewEntry
{
    public required string VesselId;
    public IRenderTexture? Texture;
    public int PixelsPerTile;
    public ShipyardPreviewInfo? Info;
    public bool Failed;
    public long Bytes;
    public long LastUsed;
}

/// <summary>
/// Renders shipyard vessels into cached textures. The grid is loaded on a client-only map,
/// drawn once through an off-screen viewport and deleted right away; nothing reaches the server.
/// </summary>
public sealed partial class ShipyardPreviewRenderSystem : EntitySystem
{
    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private IDependencyCollection _deps = default!;
    [Dependency] private IClientNetManager _net = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private const long CacheBudgetBytes = 96L * 1024 * 1024;
    private const long MaxEntryBytes = 32L * 1024 * 1024;
    private const int MaxTextureSide = 4096;
    private const int BaseTilePixels = 32;
    private const int SettleFrames = 2;
    private const int MaxUndrawnFrames = 120;
    // -- Спрайты нарисованы 32 пикселя на тайл: рендер в родном размере и вывод без сглаживания дают чёткую картинку при любом зуме
    private static readonly int[] PixelsPerTileSteps = { 32, 16 };
    private static readonly Color Transparent = new(0f, 0f, 0f, 0f);

    private readonly Dictionary<string, ShipyardPreviewEntry> _cache = new();
    private long _cacheBytes;
    private long _useCounter;
    private Job? _job;
    private readonly List<string> _notify = new();

    /// <summary>
    /// Raised with the vessel id once its picture is ready or failed; an empty id means the cache was reset.
    /// </summary>
    public event Action<string>? PreviewChanged;

    private enum Stage : byte
    {
        Queued,
        Settling,
        Ready,
    }

    private sealed class Job
    {
        public required VesselPrototype Vessel;
        public Stage Stage;
        public int Frames;
        public EntityUid Map;
        public MapId MapId;
        public Box2 Bounds;
        public ShipyardPreviewInfo? Info;
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<RoundRestartCleanupEvent>(_ => Clear());
        _net.Disconnect += OnDisconnect;
        _proto.PrototypesReloaded += OnPrototypesReloaded;
    }

    public override void Shutdown()
    {
        _net.Disconnect -= OnDisconnect;
        _proto.PrototypesReloaded -= OnPrototypesReloaded;
        Clear();
        base.Shutdown();
    }

    private void OnDisconnect(object? sender, NetDisconnectedArgs args)
    {
        Clear();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<VesselPrototype>() || args.WasModified<EntityPrototype>())
            Clear();
    }

    public ShipyardPreviewEntry? Get(string vesselId)
    {
        if (!_cache.TryGetValue(vesselId, out var entry))
            return null;

        entry.LastUsed = ++_useCounter;
        return entry;
    }

    public bool IsLoading(string vesselId)
    {
        return _job?.Vessel.ID == vesselId;
    }

    /// <summary>
    /// Starts building the picture unless it is cached or already being built; replaces any other pending build.
    /// </summary>
    public void Request(VesselPrototype vessel)
    {
        if (_cache.ContainsKey(vessel.ID) || _job?.Vessel.ID == vessel.ID)
            return;

        Cancel();
        _job = new Job { Vessel = vessel };
    }

    public void Cancel()
    {
        if (_job == null)
            return;

        if (_job.Map.IsValid())
            QueueDel(_job.Map);

        _job = null;
    }

    public void Clear()
    {
        Cancel();

        foreach (var entry in _cache.Values)
        {
            entry.Texture?.Dispose();
        }

        _cache.Clear();
        _cacheBytes = 0;
        _notify.Clear();
        PreviewChanged?.Invoke(string.Empty);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        // -- Уведомление из Draw откладывается до кадра: окно перестраивает свои элементы, а посреди отрисовки дерево UI менять нельзя
        if (_notify.Count > 0)
        {
            var ids = _notify.ToArray();
            _notify.Clear();
            foreach (var id in ids)
            {
                PreviewChanged?.Invoke(id);
            }
        }

        if (_job is not { } job)
            return;

        switch (job.Stage)
        {
            // -- Кадр паузы: надпись загрузки успевает отрисоваться до пролага от загрузки грида
            case Stage.Queued when job.Frames == 0:
                job.Frames++;
                break;
            case Stage.Queued:
                Load(job);
                break;
            case Stage.Settling:
                // -- Сглаживание стен и визуалайзеры обновляются в FrameUpdate, без ожидания снимок выйдет "сырым"
                if (--job.Frames <= 0)
                    job.Stage = Stage.Ready;
                break;
            case Stage.Ready:
                // -- Рендер идёт только из Draw окна верфи; закрытое окно не держит временную карту
                if (++job.Frames > MaxUndrawnFrames)
                    Cancel();
                break;
        }
    }

    private void Load(Job job)
    {
        try
        {
            if (!TryBuildGrid(job.Vessel, out job.Map, out job.MapId, out job.Bounds, out job.Info))
            {
                Fail(job);
                return;
            }

            job.Stage = Stage.Settling;
            job.Frames = SettleFrames;
        }
        catch (Exception e)
        {
            Log.Error($"Failed to load shipyard preview for {job.Vessel.ID}: {e}");
            Fail(job);
        }
    }

    /// <summary>
    /// Loads the vessel grid north-up on a new client-only map. The caller owns the map and deletes it.
    /// </summary>
    public bool TryBuildGrid(VesselPrototype vessel, out EntityUid map, out MapId mapId, out Box2 bounds, out ShipyardPreviewInfo? info)
    {
        map = EntityUid.Invalid;
        mapId = MapId.Nullspace;
        bounds = default;
        info = null;

        if (!_loader.TryReadFile(vessel.ShuttlePath.ToRootedPath(), out var data))
            return false;

        StripServerComponents(data);

        var read = new BeforeEntityReadEvent();
        RaiseLocalEvent(read);

        // -- Свой проход вместо TryLoadGrid: спрайты чинятся между созданием и стартом сущностей
        var deserializer = new EntityDeserializer(_deps,
            data,
            new DeserializationOptions { LogOrphanedGrids = false },
            read.RenamedPrototypes,
            read.DeletedPrototypes);

        if (!deserializer.TryProcessData())
            return false;

        var result = deserializer.Result;
        try
        {
            deserializer.CreateEntities();
            foreach (var uid in result.Entities)
            {
                RestoreSprite(uid);
            }

            deserializer.StartEntities();
        }
        catch (Exception e)
        {
            Log.Error($"Failed to load shipyard preview grid {vessel.ShuttlePath}: {e}");
            _loader.Delete(result);
            return false;
        }

        if (result.Grids.Count != 1 || result.Maps.Count != 1)
        {
            _loader.Delete(result);
            return false;
        }

        var grid = result.Grids.First();
        var mapEntity = result.Maps.First();
        map = mapEntity.Owner;
        mapId = mapEntity.Comp.MapId;
        _meta.SetEntityName(map, "Shipyard preview");

        _xform.SetLocalRotation(grid.Owner, Angle.Zero);

        var aabb = grid.Comp.LocalAABB;
        bounds = aabb.Translated(_xform.GetWorldPosition(grid.Owner));

        var tiles = _map.GetAllTiles(grid.Owner, grid.Comp).Count();
        info = new ShipyardPreviewInfo(
            new Vector2i((int) MathF.Round(aabb.Width), (int) MathF.Round(aabb.Height)),
            tiles);
        return true;
    }

    /// <summary>
    /// Applies the sprite data a map-loaded entity received after its sprite was added.
    /// </summary>
    private void RestoreSprite(EntityUid uid)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        // -- Загрузчик карт добавляет спрайт пустым и копирует данные после: RSI и слои надо подтянуть вручную, иначе стены и шлюзы без текстур
        _sprite.CopySprite((uid, sprite), (uid, sprite));

        if (sprite.BaseRSI != null
            || MetaData(uid).EntityPrototype is not { } proto
            || !proto.TryComp<SpriteComponent>(out var protoSprite, _factory)
            || protoSprite.BaseRSI is not { } rsi)
        {
            return;
        }

        _sprite.SetBaseRsi((uid, sprite), rsi);
    }

    /// <summary>
    /// Drops components the client does not know from the map data.
    /// </summary>
    private void StripServerComponents(MappingDataNode data)
    {
        // -- Клиент игнорирует серверные компоненты в прототипах, но загрузчик карт пишет на каждый ошибку: вырезаем их заранее
        if (!data.TryGet("entities", out SequenceDataNode? groups))
            return;

        foreach (var group in groups.Sequence.OfType<MappingDataNode>())
        {
            if (!group.TryGet("entities", out SequenceDataNode? entities))
                continue;

            foreach (var entity in entities.Sequence.OfType<MappingDataNode>())
            {
                if (!entity.TryGet("components", out SequenceDataNode? components))
                    continue;

                for (var i = components.Sequence.Count - 1; i >= 0; i--)
                {
                    if (components.Sequence[i] is MappingDataNode component
                        && component.TryGet("type", out ValueDataNode? type)
                        && !_factory.TryGetRegistration(type.Value, out _))
                    {
                        components.RemoveAt(i);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Renders the pending grid; called from a preview control's Draw, where rendering into targets is allowed.
    /// </summary>
    public void RenderPending(IRenderHandle handle)
    {
        if (_job is not { Stage: Stage.Ready } job)
            return;

        var size = job.Bounds.Size + new Vector2(2f, 2f);
        var pixelsPerTile = PickPixelsPerTile(size);
        var pixelSize = new Vector2i(
            Math.Max(1, (int) MathF.Ceiling(size.X * pixelsPerTile)),
            Math.Max(1, (int) MathF.Ceiling(size.Y * pixelsPerTile)));

        IClydeViewport? viewport = null;
        IRenderTexture? texture = null;
        try
        {
            viewport = _clyde.CreateViewport(pixelSize, "ShipyardPreview");
            viewport.AutomaticRender = false;
            viewport.ClearColor = Transparent;
            // -- ZEye без параллакса: обычный глаз рисует фон-параллакс и эффекты z-уровней поверх шаттла
            viewport.Eye = new ScalingViewport.ZEye(0f, 0f, 0f)
            {
                DrawParallax = false,
                DrawFov = false,
                DrawLight = false,
                Position = new MapCoordinates(job.Bounds.Center, job.MapId),
                Zoom = new Vector2(BaseTilePixels / (float) pixelsPerTile),
            };
            viewport.Render();

            texture = _clyde.CreateRenderTarget(pixelSize,
                new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
                new TextureSampleParameters { Filter = false },
                "ShipyardPreviewCache");

            var source = viewport.RenderTarget.Texture;
            handle.RenderInRenderTarget(texture, () =>
            {
                var screen = handle.DrawingHandleScreen;
                screen.SetTransform(Matrix3x2.Identity);
                screen.DrawTextureRect(source, UIBox2.FromDimensions(Vector2.Zero, pixelSize));
            }, Transparent);
        }
        catch (Exception e)
        {
            Log.Error($"Failed to render shipyard preview for {job.Vessel.ID}: {e}");
            texture?.Dispose();
            texture = null;
        }
        finally
        {
            viewport?.Dispose();
        }

        Finish(job, texture, pixelsPerTile, pixelSize);
    }

    private static int PickPixelsPerTile(Vector2 size)
    {
        foreach (var step in PixelsPerTileSteps)
        {
            var width = size.X * step;
            var height = size.Y * step;
            if (width <= MaxTextureSide && height <= MaxTextureSide && (long) (width * height) * 4 <= MaxEntryBytes)
                return step;
        }

        return Math.Max(4, (int) (MaxTextureSide / MathF.Max(size.X, size.Y)));
    }

    private void Fail(Job job)
    {
        Finish(job, null, 0, Vector2i.Zero);
    }

    private void Finish(Job job, IRenderTexture? texture, int pixelsPerTile, Vector2i pixelSize)
    {
        if (job.Map.IsValid())
            QueueDel(job.Map);

        if (_job == job)
            _job = null;

        var entry = new ShipyardPreviewEntry
        {
            VesselId = job.Vessel.ID,
            Texture = texture,
            PixelsPerTile = pixelsPerTile,
            Info = job.Info,
            Failed = texture == null,
            Bytes = texture == null ? 0 : (long) pixelSize.X * pixelSize.Y * 4,
            LastUsed = ++_useCounter,
        };

        if (_cache.Remove(entry.VesselId, out var old))
        {
            _cacheBytes -= old.Bytes;
            old.Texture?.Dispose();
        }

        _cache[entry.VesselId] = entry;
        _cacheBytes += entry.Bytes;
        Evict(entry.VesselId);

        _notify.Add(entry.VesselId);
    }

    private void Evict(string keep)
    {
        while (_cacheBytes > CacheBudgetBytes && _cache.Count > 1)
        {
            var oldest = _cache.Values.Where(e => e.VesselId != keep).MinBy(e => e.LastUsed);
            if (oldest == null)
                return;

            _cache.Remove(oldest.VesselId);
            _cacheBytes -= oldest.Bytes;
            oldest.Texture?.Dispose();
        }
    }
}
