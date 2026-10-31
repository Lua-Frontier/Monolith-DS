using Content.Server._Mono.FireControl;
using Content.Server._Mono.SpaceArtillery;
using Content.Server._Mono.SpaceArtillery.Components;
using Content.Server.DeviceLinking.Systems;
using Content.Shared._Crescent.Hardpoints;
using Content.Shared.Construction.Components;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map.Components; // LuaM
using Robust.Shared.Physics;
using Robust.Shared.Timing;

namespace Content.Server._Crescent.Hardpoint;

/// <summary>
/// This handles...
/// </summary>
public partial class HardpointSystem : SharedHardpointSystem
{
    [Dependency] private FireControlSystem _fcsSystem = default!;
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private DeviceLinkSystem _signalSystem = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private EntityLookupSystem _lookupSystem = default!;
    [Dependency] private SharedMapSystem _maps = default!; // LuaM
    // Explosions can cause a lot of lookups and events to fire. So we time-limit it based on grids
    private const float UpdateDelay = 2f; // LuaM: 60 > 2, a swapped gun stayed unusable for up to a minute
    private float InternalTimer = 0f;
    private HashSet<EntityUid> QueuedGrids = new();
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<FixturesComponent, AnchorStateChangedEvent>(OnFixtureAnchor);
        SubscribeLocalEvent<HardpointComponent, HardpointCannonAnchoredEvent>(OnCannonAnchor);
        SubscribeLocalEvent<HardpointComponent, HardpointCannonDeanchoredEvent>(OnCannonDeanchor);
        SubscribeLocalEvent<HardpointFixedMountComponent, SignalReceivedEvent>(OnSignalReceived);
        SubscribeLocalEvent<HardpointComponent, MapInitEvent>(OnHardpointMapInit); // LuaM
        SubscribeLocalEvent<HardpointComponent, ComponentShutdown>(OnHardpointShutdown); // LuaM
    }

    // LuaM-start
    /// <summary>
    /// A hardpoint that appears under a gun already standing there (ship repair, admin spawn) mounts that gun,
    /// otherwise the gun kept pointing at the old, deleted hardpoint.
    /// </summary>
    private void OnHardpointMapInit(Entity<HardpointComponent> ent, ref MapInitEvent args)
    {
        var xform = Transform(ent);
        if (!xform.Anchored || xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var tile = _maps.TileIndicesFor(gridUid, grid, xform.Coordinates);
        foreach (var other in _maps.GetAnchoredEntities((gridUid, grid), tile))
        {
            if (!TryComp<HardpointAnchorableOnlyComponent>(other, out var gun))
                continue;

            if (gun.anchoredTo is { } current && !TerminatingOrDeleted(current) && HasComp<HardpointComponent>(current))
                continue;

            gun.anchoredTo = null;
            if (TryAnchorToHardpoint(other, gun))
                return;
        }
    }

    /// <summary>
    /// A destroyed or deleted hardpoint frees its gun, so the gun no longer counts as mounted.
    /// </summary>
    private void OnHardpointShutdown(EntityUid uid, HardpointComponent comp, ComponentShutdown args)
    {
        if (comp.anchoring is not { } gun || TerminatingOrDeleted(gun))
            return;

        if (!TryComp<HardpointAnchorableOnlyComponent>(gun, out var gunComp) || gunComp.anchoredTo != uid)
            return;

        gunComp.anchoredTo = null;
        Dirty(gun, gunComp);

        if (Transform(gun).GridUid is { } grid)
            QueueHardpointRefresh(grid);
    }
    // LuaM-end
    private void OnSignalReceived(EntityUid uid, HardpointFixedMountComponent component, ref SignalReceivedEvent args)
    {
        if (!TryComp<HardpointComponent>(uid, out var hard))
            return;
        if (hard.anchoring is null)
            return;
        if (!TryComp<GunComponent>(hard.anchoring.Value, out var gun))
            return;

        var gridUid = Transform(uid).GridUid;
        if (gridUid != null && HasComp<SpaceArtilleryDisabledGridComponent>(gridUid))
        {
                return;
        }

        if (args.Port == component.Trigger)
            _gun.AttemptShoot(hard.anchoring.Value, gun);
        var autoShoot = EnsureComp<AutoShootGunComponent>(hard.anchoring.Value);
        if (args.Port == component.Toggle)
            _gun.SetEnabled(hard.anchoring.Value, autoShoot, !autoShoot.Enabled);
    }

    public void OnFixtureAnchor(EntityUid uid, FixturesComponent comp, ref AnchorStateChangedEvent args)
    {
        if (args.Transform.GridUid is null)
            return;
        QueueHardpointRefresh(args.Transform.GridUid.Value);
    }

    public void QueueHardpointRefresh(EntityUid grid)
    {
        // LuaM: grids without a gunnery server have nothing to refresh
        if (!HasComp<FireControlGridComponent>(grid))
            return;

        QueuedGrids.Add(grid);
    }


    public void OnCannonAnchor(EntityUid uid, HardpointComponent comp, ref HardpointCannonAnchoredEvent args)
    {
        // This is just for turret-cannons!
        if (!TryComp<FireControllableComponent>(args.cannonUid, out var compx))
            return;
        QueueHardpointRefresh(args.gridUid);
    }

    public void OnCannonDeanchor(EntityUid uid, HardpointComponent comp, ref HardpointCannonDeanchoredEvent args)
    {
        // This is just for turret-cannons!
        if (!TryComp<FireControllableComponent>(args.CannonUid, out var compx))
            return;
        QueueHardpointRefresh(args.gridUid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_gameTiming.IsFirstTimePredicted)
            return;

        if (!_gameTiming.IsFirstTimePredicted)
            return;

        InternalTimer += frameTime;
        if (InternalTimer < UpdateDelay)
            return;
        InternalTimer = 0;
        // LuaM: a grid without a gunnery server used to return here, the queue was never cleared and no grid got
        // refreshed again, so guns put on hardpoints stayed undetected. Removing from the set while iterating it threw too.
        foreach (var grid in QueuedGrids)
        {
            if (TerminatingOrDeleted(grid) || !TryComp<FireControlGridComponent>(grid, out var gridFireControlComp))
                continue;

            _fcsSystem.RefreshControllables(grid, gridFireControlComp);
        }
        QueuedGrids.Clear();
        // End LuaM
    }
}
