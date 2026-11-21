using System.Linq;
using Content.Server._Mono.Worldgen.Components;
using Content.Server.Power.Components;
using Content.Server.Worldgen.Components;
using Content.Shared.Ghost;
using Content.Shared.Mind.Components;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

// Mono maint note - this system no longer exists upstream and // Mono comments are not a requirement for that reason
namespace Content.Server.Worldgen.Systems;

/// <summary>
///     This handles putting together chunk entities and notifying them about important changes.
/// </summary>
public sealed partial class WorldControllerSystem : EntitySystem
{
    [Dependency] private TransformSystem _xformSys = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;

    // <Mono>
    private const int PlayerLoadRadius = 2;
    private float _updateInterval = 1f;
    private float _updateAccumulator = 0f;
    private Dictionary<EntityUid, Dictionary<Vector2i, List<EntityUid>>> _chunksToLoad = new();
    // </Mono>

    private ISawmill _sawmill = default!;

    /// <inheritdoc />
    public override void Initialize()
    {
        _sawmill = _logManager.GetSawmill("world");
        SubscribeLocalEvent<LoadedChunkComponent, ComponentStartup>(OnChunkLoadedCore);
        SubscribeLocalEvent<LoadedChunkComponent, ComponentShutdown>(OnChunkUnloadedCore);
        SubscribeLocalEvent<WorldChunkComponent, ComponentShutdown>(OnChunkShutdown);
        InitializeUnloadGrace(); // LuaM
        InitializeIdleHelms(); // LuaM
    }

    /// <summary>
    ///     Handles deleting chunks properly.
    /// </summary>
    private void OnChunkShutdown(EntityUid uid, WorldChunkComponent component, ComponentShutdown args)
    {
        _unneededSince.Remove(uid); // LuaM
        if (!TryComp<WorldControllerComponent>(component.Map, out var controller))
            return;

        if (HasComp<LoadedChunkComponent>(uid))
        {
            var ev = new WorldChunkUnloadedEvent(uid, component.Coordinates);
            RaiseLocalEvent(component.Map, ref ev);
            RaiseLocalEvent(uid, ref ev, broadcast: true);
        }

        controller.Chunks.Remove(component.Coordinates);
    }

    /// <summary>
    ///     Handles the inner logic of loading a chunk, i.e. events.
    /// </summary>
    private void OnChunkLoadedCore(EntityUid uid, LoadedChunkComponent component, ComponentStartup args)
    {
        if (!TryComp<WorldChunkComponent>(uid, out var chunk))
            return;

        var ev = new WorldChunkLoadedEvent(uid, chunk.Coordinates);
        RaiseLocalEvent(chunk.Map, ref ev);
        RaiseLocalEvent(uid, ref ev, broadcast: true);
        //_sawmill.Debug($"Loaded chunk {ToPrettyString(uid)} at {chunk.Coordinates}");
    }

    /// <summary>
    ///     Handles the inner logic of unloading a chunk, i.e. events.
    /// </summary>
    private void OnChunkUnloadedCore(EntityUid uid, LoadedChunkComponent component, ComponentShutdown args)
    {
        if (!TryComp<WorldChunkComponent>(uid, out var chunk))
            return;

        if (Terminating(uid))
            return; // SAFETY: This is in case a loaded chunk gets deleted, to avoid double unload.

        var ev = new WorldChunkUnloadedEvent(uid, chunk.Coordinates);
        RaiseLocalEvent(chunk.Map, ref ev);
        RaiseLocalEvent(uid, ref ev);
        //_sawmill.Debug($"Unloaded chunk {ToPrettyString(uid)} at {coords}");
    }

    /// <inheritdoc />
    public override void Update(float frameTime)
    {
        _updateAccumulator += frameTime;
        if (_updateAccumulator < _updateInterval)
            return;
        _updateAccumulator -= _updateInterval; // Mono - Delay between updates

        //there was a to-do here about every frame alloc but it turns out it's a nothing burger here.
        _chunksToLoad.Clear();

        var controllerEnum = EntityQueryEnumerator<WorldControllerComponent>();
        while (controllerEnum.MoveNext(out var uid, out _))
        {
            _chunksToLoad[uid] = new Dictionary<Vector2i, List<EntityUid>>();
        }

        if (_chunksToLoad.Count == 0 && EntityManager.Count<LoadedChunkComponent>() == 0) // LuaM: unload chunks of removed controllers too
            return; // Just bail early.

        CollectActiveHelmGrids(); // LuaM

        var loaderEnum = EntityQueryEnumerator<WorldLoaderComponent, TransformComponent>();

        while (loaderEnum.MoveNext(out var uid, out var worldLoader, out var xform))
        {
            if (worldLoader.Disabled) // Frontier: disable world loading
                continue; // Frontier

            if (IsIdleHelm(uid, xform)) // LuaM
                continue; // LuaM

            // Mono edit
            TryAddChunkLoader(uid, xform, (int) Math.Ceiling(worldLoader.Radius / (float) WorldGen.ChunkSize) + 1);
        }

        var mindEnum = EntityQueryEnumerator<MindContainerComponent, TransformComponent>();
        var chunkLoaderEnum = EntityQueryEnumerator<ChunkLoaderComponent, TransformComponent>();
        var ghostQuery = GetEntityQuery<GhostComponent>();

        // Mindful entities get special privilege as they're always a player and we don't want the illusion being broken around them.
        while (mindEnum.MoveNext(out var uid, out var mind, out var xform))
        {
            if (!mind.HasMind)
                continue;

            if (ghostQuery.HasComponent(uid))
                continue;

            // Mono edit
            TryAddChunkLoader(uid, xform, PlayerLoadRadius);
        }

        // Mono edit - Component for loading chunks.
        while (chunkLoaderEnum.MoveNext(out var uid, out var load, out var xform))
        {
            if (load.RequirePower && TryComp<ApcPowerReceiverComponent>(uid, out var receiver))
            {
                if (!receiver.Powered)
                    continue;
            }

            TryAddChunkLoader(uid, xform, (int) Math.Ceiling(load.LoadingDistance / (float) WorldGen.ChunkSize) + 1);
        }
        // Mono edit end.

        var loadedEnum = EntityQueryEnumerator<LoadedChunkComponent, WorldChunkComponent>();
        var chunksUnloaded = 0;

        // Make sure these chunks get unloaded at the end of the tick.
        while (loadedEnum.MoveNext(out var uid, out var loaded, out var chunk))
        {
            // LuaM-start: retained chunks no longer have loaders, and their map may have been deleted.
            var hasController = _chunksToLoad.TryGetValue(chunk.Map, out var demand);
            var needed = demand?.ContainsKey(chunk.Coordinates) == true;
            if (!needed)
                loaded.Loaders = null;

            if (!hasController || ShouldUnload(uid, needed))
            // LuaM-end
            {
                if (!hasController)
                    _unneededSince.Remove(uid); // LuaM
                RemCompDeferred<LoadedChunkComponent>(uid);
                chunksUnloaded++;
            }
        }

        if (chunksUnloaded > 0)
            _sawmill.Debug($"Queued {chunksUnloaded} chunks for unload.");

        LogChunkCount(); // LuaM

        if (_chunksToLoad.All(x => x.Value.Count == 0))
            return;

        var startTime = _gameTiming.RealTime;
        var count = 0;
        var loadedQuery = GetEntityQuery<LoadedChunkComponent>();
        var controllerQuery = GetEntityQuery<WorldControllerComponent>();
        foreach (var (map, chunks) in _chunksToLoad)
        {
            var controller = controllerQuery.GetComponent(map);
            foreach (var (chunk, loaders) in chunks)
            {
                var ent = GetOrCreateChunk(chunk, map, controller); // Ensure everything loads.
                LoadedChunkComponent? c = null;
                if (ent is not null && !loadedQuery.TryGetComponent(ent.Value, out c))
                {
                    c = AddComp<LoadedChunkComponent>(ent.Value);
                    count += 1;
                }

                if (c is not null)
                    c.Loaders = loaders;
            }
        }

        if (count > 0)
        {
            var timeSpan = _gameTiming.RealTime - startTime;
            _sawmill.Debug($"Loaded {count} chunks in {timeSpan.TotalMilliseconds:N2}ms.");
        }
    }

    /// <summary>
    ///     Attempts to get a chunk, creating it if it doesn't exist.
    /// </summary>
    /// <param name="chunk">Chunk coordinates to get the chunk entity for.</param>
    /// <param name="map">Map the chunk is in.</param>
    /// <param name="controller">The controller this chunk belongs to.</param>
    /// <returns>A chunk, if available.</returns>
    [Pure]
    public EntityUid? GetOrCreateChunk(Vector2i chunk, EntityUid map, WorldControllerComponent? controller = null)
    {
        if (!Resolve(map, ref controller))
            throw new Exception($"Tried to use {ToPrettyString(map)} as a world map, without actually being one.");

        if (controller.Chunks.TryGetValue(chunk, out var ent))
            return ent;
        return CreateChunkEntity(chunk, map, controller);
    }

    /// <summary>
    ///     Constructs a new chunk entity, attaching it to the map.
    /// </summary>
    /// <param name="chunkCoords">The coordinates the new chunk should be initialized for.</param>
    /// <param name="map"></param>
    /// <param name="controller"></param>
    /// <returns></returns>
    private EntityUid CreateChunkEntity(Vector2i chunkCoords, EntityUid map, WorldControllerComponent controller)
    {
        var chunk = Spawn(controller.ChunkProto, MapCoordinates.Nullspace);
        StartupChunkEntity(chunk, chunkCoords, map, controller);
        _metaData.SetEntityName(chunk, $"Chunk {chunkCoords.X}/{chunkCoords.Y}");
        return chunk;
    }

    private void StartupChunkEntity(EntityUid chunk, Vector2i coords, EntityUid map,
        WorldControllerComponent controller)
    {
        if (!TryComp<WorldChunkComponent>(chunk, out var chunkComponent))
        {
            _sawmill.Error($"Chunk {ToPrettyString(chunk)} is missing WorldChunkComponent.");
            return;
        }

        ref var chunks = ref controller.Chunks;

        chunks[coords] = chunk; // Add this entity to chunk index.
        chunkComponent.Coordinates = coords;
        chunkComponent.Map = map;
        var ev = new WorldChunkAddedEvent(chunk, coords);
        RaiseLocalEvent(map, ref ev, broadcast: true);
    }

    /// <summary>
    /// Mono edit - This method will try to add entity to LoadedChunkComponent. If it fails - method returns False.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="xform"></param>
    /// <param name="_chunksToLoad"></param>
    /// <param name="radius"></param>
    /// <returns></returns>
    private bool TryAddChunkLoader(
        EntityUid uid,
        TransformComponent xform,
        int radius)
    {
        var mapOrNull = xform.MapUid;
        if (mapOrNull is null)
            return false;

        var map = mapOrNull.Value;
        if (!_chunksToLoad.TryGetValue(map, out var set)) // LuaM
            return false;

        var wc = _xformSys.GetWorldPosition(xform);
        // Mono - load ahead a little
        var mapVel = _physics.GetMapLinearVelocity(uid, xform: xform);
        wc += mapVel * _updateInterval;
        var coords = WorldGen.WorldToChunkCoords(wc);
        var chunks = new GridPointsNearEnumerator(coords.Floored(), radius);

        while (chunks.MoveNext(out var chunk))
        {
            if (!set.TryGetValue(chunk.Value, out var loaders)) // LuaM
                set[chunk.Value] = loaders = new List<EntityUid>(4); // LuaM
            loaders.Add(uid); // LuaM
        }

        return true;
    }
}

/// <summary>
///     A directed event fired when a chunk is initially set up in the world. The chunk is not loaded at this point.
/// </summary>
[ByRefEvent]
[PublicAPI]
public readonly record struct WorldChunkAddedEvent(EntityUid Chunk, Vector2i Coords);

/// <summary>
///     A directed event fired when a chunk is loaded into the world, i.e. a player or other world loader has entered vicinity.
/// </summary>
[ByRefEvent]
[PublicAPI]
public readonly record struct WorldChunkLoadedEvent(EntityUid Chunk, Vector2i Coords);

/// <summary>
///     A directed event fired when a chunk is unloaded from the world, i.e. no world loaders remain nearby.
/// </summary>
[ByRefEvent]
[PublicAPI]
public readonly record struct WorldChunkUnloadedEvent(EntityUid Chunk, Vector2i Coords);
