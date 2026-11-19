using Content.Server.Worldgen.Components.Debris;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Stopwatch = System.Diagnostics.Stopwatch; // LuaM

namespace Content.Server.Worldgen.Systems.Debris;

/// <summary>
///     This handles populating simple structures, simply using a loot table for each tile.
/// </summary>
public sealed partial class SimpleFloorPlanPopulatorSystem : BaseWorldSystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ITileDefinitionManager _tileDefinition = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TurfSystem _turf = default!;

    // LuaM-start: the entities spawn over several ticks within a time budget
    private static readonly TimeSpan PopulateBudget = TimeSpan.FromMilliseconds(6);

    private readonly Queue<PopulateJob> _populateJobs = new();
    private List<string?> _placeables = new(4);

    private sealed class PopulateJob(EntityUid grid, SimpleFloorPlanPopulatorComponent component)
    {
        public readonly EntityUid Grid = grid;
        public readonly SimpleFloorPlanPopulatorComponent Component = component;
        public readonly List<Vector2i> Tiles = new();
        public int Next;
    }
    // LuaM-end

    /// <inheritdoc />
    public override void Initialize()
    {
        SubscribeLocalEvent<SimpleFloorPlanPopulatorComponent, LocalStructureLoadedEvent>(OnFloorPlanBuilt);
    }

    private void OnFloorPlanBuilt(EntityUid uid, SimpleFloorPlanPopulatorComponent component,
        LocalStructureLoadedEvent args)
    {
        // LuaM-start: queued, populated in Update
        var grid = Comp<MapGridComponent>(uid);
        var job = new PopulateJob(uid, component);
        var enumerator = _map.GetAllTiles(uid, grid);
        while (enumerator.MoveNext(out var tile))
        {
            job.Tiles.Add(tile.Value.GridIndices);
        }

        _populateJobs.Enqueue(job);
        // LuaM-end
    }

    // LuaM-start
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_populateJobs.Count == 0)
            return;

        var watch = Stopwatch.StartNew();
        while (_populateJobs.TryPeek(out var job))
        {
            if (TerminatingOrDeleted(job.Grid) || !TryComp<MapGridComponent>(job.Grid, out var grid))
            {
                _populateJobs.Dequeue();
                continue;
            }

            while (job.Next < job.Tiles.Count)
            {
                PopulateTile(job.Grid, grid, job.Component, job.Tiles[job.Next++]);
                if (watch.Elapsed >= PopulateBudget)
                    return;
            }

            _populateJobs.Dequeue();
        }
    }

    private void PopulateTile(EntityUid uid, MapGridComponent grid, SimpleFloorPlanPopulatorComponent component, Vector2i indices)
    {
        if (!_map.TryGetTileRef(uid, grid, indices, out var tile) || tile.Tile.IsEmpty)
            return;

        var selector = _turf.GetContentTileDefinition(tile).ID;
        if (!component.Caches.TryGetValue(selector, out var cache))
            return;

        _placeables.Clear();
        cache.GetSpawns(_random, ref _placeables);

        var coords = _map.GridTileToLocal(uid, grid, indices);
        foreach (var proto in _placeables)
        {
            if (proto is null)
                continue;

            Spawn(proto, coords);
        }
    }
    // LuaM-end
}
