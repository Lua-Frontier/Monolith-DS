using Content.Server.Cargo.Systems;
using Content.Server.Power.Components;
using Content.Shared._Mono.CCVar;
using Content.Shared.Power.Components;
using Content.Shared.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;

namespace Content.Server._Mono.Cleanup;

/// <summary>
/// This system cleans up small grid fragments that have less than a specified number of tiles after a delay.
/// </summary>
public sealed partial class GridCleanupSystem : BaseCleanupSystem<MapGridComponent>
{
    [Dependency] private CleanupHelperSystem _cleanup = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private PricingSystem _pricing = default!;
    [Dependency] private SharedMapSystem _map = default!;

    private float _maxDistance;
    private float _maxValue;
    private int _aggressiveTiles;
    private TimeSpan _duration;

    private HashSet<Entity<ApcComponent>> _apcList = new();
    private readonly GridCleanupGridComponent _defaultState = new(); // LuaM

    private EntityQuery<BatteryComponent> _batteryQuery;
    private EntityQuery<CleanupImmuneComponent> _immuneQuery;

    public override void Initialize()
    {
        base.Initialize();

        _batteryQuery = GetEntityQuery<BatteryComponent>();
        _immuneQuery = GetEntityQuery<CleanupImmuneComponent>();

        Subs.CVar(_cfg, MonoCVars.GridCleanupDistance, val => _maxDistance = val, true);
        Subs.CVar(_cfg, MonoCVars.GridCleanupMaxValue, val => _maxValue = val, true);
        Subs.CVar(_cfg, MonoCVars.GridCleanupDuration, val => _duration = TimeSpan.FromSeconds(val), true);
        Subs.CVar(_cfg, MonoCVars.GridCleanupAggressiveTiles, val => _aggressiveTiles = val, true);
    }

    protected override bool ShouldEntityCleanup(EntityUid uid)
    {
        // LuaM-start: the checks moved to IsCleanupCandidate and IsAbandoned, the cleanupgrids command uses them too
        if (!IsCleanupCandidate(uid, out var xform, out var scale))
            return false;

        var state = EnsureComp<GridCleanupGridComponent>(uid);

        if (!IsAbandoned(uid, xform, scale, state))
        {
            state.CleanupAccumulator = TimeSpan.FromSeconds(0);
            return false;
        }
        // see if we should update timer or just be deleted
        else if (state.CleanupAccumulator < _duration)
        {
            state.CleanupAccumulator += _cleanupInterval * state.CleanupAcceleration / scale;
            return false;
        }
        // LuaM-end

        return true;
    }

    // LuaM-start
    /// <summary>
    /// Whether the grid can be cleaned up at all: not a planet, not on a planet, not immune and has fixtures.
    /// </summary>
    private bool IsCleanupCandidate(EntityUid uid, out TransformComponent xform, out float scale)
    {
        xform = Transform(uid);
        scale = 0f;

        // if we somehow lost it
        if (!HasComp<MapGridComponent>(uid) || !TryComp<PhysicsComponent>(uid, out var body))
            return false;

        if (HasComp<MapComponent>(uid) // if we're a planetmap ignore
            || HasComp<MapGridComponent>(xform.ParentUid) // do not delete anything on planetmaps either
            || _immuneQuery.HasComp(uid))
            return false;

        var tiles = body.FixturesMass / ShuttleSystem.TileDensityMultiplier;
        scale = MathF.Min(tiles / _aggressiveTiles, 1f);

        // no fixtures yet or none left, nothing to scale by
        return scale > 0f;
    }

    /// <summary>
    /// Whether nothing keeps the grid around right now: IFF label, players nearby, powered APCs, price.
    /// </summary>
    private bool IsAbandoned(EntityUid uid, TransformComponent xform, float scale, GridCleanupGridComponent state)
    {
        return !(!state.IgnoreIFF && TryComp<IFFComponent>(uid, out var iff) && (iff.Flags & IFFFlags.HideLabel) == 0 // delete only if IFF off
            || _cleanup.HasNearbyPlayers(xform.Coordinates, state.DistanceOverride ?? _maxDistance * scale * scale) // square it
            || !state.IgnorePowered && HasPoweredAPC((uid, xform)) // don't delete if it has powered APCs
            || !state.IgnorePrice && _pricing.AppraiseGrid(uid) > _maxValue); // expensive to run, put last
    }

    /// <summary>
    /// The grids that meet the cleanup rules right now, without waiting for the cleanup timer.
    /// </summary>
    public List<EntityUid> GetAbandonedGrids()
    {
        var grids = new List<EntityUid>();
        var query = EntityQueryEnumerator<MapGridComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            grids.Add(uid);
        }

        var result = new List<EntityUid>();
        foreach (var uid in grids)
        {
            if (TerminatingOrDeleted(uid) || !IsCleanupCandidate(uid, out var xform, out var scale))
                continue;

            // grids without settings get the default ones, without adding the component
            var state = CompOrNull<GridCleanupGridComponent>(uid) ?? _defaultState;
            if (IsAbandoned(uid, xform, scale, state))
                result.Add(uid);
        }

        return result;
    }

    /// <summary>
    /// Deletes every grid that meets the cleanup rules right now.
    /// </summary>
    public int ForceCleanupGrids()
    {
        var grids = GetAbandonedGrids();
        foreach (var uid in grids)
        {
            CleanupEnt(uid);
        }

        return grids.Count;
    }
    // LuaM-end

    bool HasPoweredAPC(Entity<TransformComponent> grid)
    {
        _apcList.Clear();
        var worldAABB = _lookup.GetWorldAABB(grid, grid.Comp);

        _lookup.GetEntitiesIntersecting<ApcComponent>(grid.Comp.MapID, worldAABB, _apcList);

        foreach (var apc in _apcList)
        {
            // charge check should ideally be a comparision to 0f but i don't trust that
            if (_batteryQuery.TryComp(apc, out var battery)
                && battery.CurrentCharge > battery.MaxCharge * 0.01f
                && apc.Comp.MainBreakerEnabled // if it's disabled consider it depowered
            )
                return true;
        }
        return false;
    }
}
