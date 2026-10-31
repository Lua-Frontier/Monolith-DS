using Content.Shared._LuaM.Shuttles;
using Content.Shared._Mono.GridEdgeMarker;
using Robust.Shared.Map.Components;

namespace Content.Server._LuaM.Shuttles;

public sealed class RadarGridEdgesSystem : EntitySystem
{
    private readonly Dictionary<EntityUid, EntityUid> _markerGrids = new();
    private readonly HashSet<EntityUid> _dirtyGrids = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GridEdgeMarkerComponent, ComponentStartup>(OnMarkerStartup);
        SubscribeLocalEvent<GridEdgeMarkerComponent, ComponentShutdown>(OnMarkerShutdown);
        SubscribeLocalEvent<GridEdgeMarkerComponent, EntParentChangedMessage>(OnMarkerParentChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_dirtyGrids.Count == 0)
            return;

        foreach (var grid in _dirtyGrids)
        {
            if (TerminatingOrDeleted(grid) ||
                !TryComp<RadarGridEdgesComponent>(grid, out var edges) ||
                !TryComp<MapGridComponent>(grid, out var mapGrid))
            {
                continue;
            }

            if (edges.Markers.Count == 0)
            {
                RemCompDeferred<RadarGridEdgesComponent>(grid);
                continue;
            }

            edges.Edges.Clear();
            foreach (var marker in edges.Markers)
            {
                if (!TryComp<GridEdgeMarkerComponent>(marker, out var comp))
                    continue;

                var xform = Transform(marker);
                var position = xform.LocalPosition;
                var rotation = xform.LocalRotation;
                edges.Edges.Add(position + rotation.RotateVec(comp.Begin) * mapGrid.TileSize);
                edges.Edges.Add(position + rotation.RotateVec(comp.End) * mapGrid.TileSize);
            }

            Dirty(grid, edges);
        }

        _dirtyGrids.Clear();
    }

    private void OnMarkerStartup(Entity<GridEdgeMarkerComponent> ent, ref ComponentStartup args)
    {
        Track(ent, Transform(ent).ParentUid);
    }

    private void OnMarkerShutdown(Entity<GridEdgeMarkerComponent> ent, ref ComponentShutdown args)
    {
        Track(ent, null);
    }

    private void OnMarkerParentChanged(Entity<GridEdgeMarkerComponent> ent, ref EntParentChangedMessage args)
    {
        if (LifeStage(ent) < EntityLifeStage.Initialized || LifeStage(ent) >= EntityLifeStage.Terminating)
            return;

        Track(ent, args.Transform.ParentUid);
    }

    private void Track(EntityUid marker, EntityUid? parent)
    {
        EntityUid? grid = parent is { } uid && HasComp<MapGridComponent>(uid) ? uid : null;

        if (_markerGrids.TryGetValue(marker, out var oldGrid))
        {
            if (oldGrid == grid)
            {
                _dirtyGrids.Add(oldGrid);
                return;
            }

            if (TryComp<RadarGridEdgesComponent>(oldGrid, out var oldEdges))
                oldEdges.Markers.Remove(marker);

            _markerGrids.Remove(marker);
            _dirtyGrids.Add(oldGrid);
        }

        if (grid is not { } newGrid || TerminatingOrDeleted(newGrid))
            return;

        EnsureComp<RadarGridEdgesComponent>(newGrid).Markers.Add(marker);
        _markerGrids[marker] = newGrid;
        _dirtyGrids.Add(newGrid);
    }
}
