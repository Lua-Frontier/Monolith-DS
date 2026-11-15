/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using Content.Shared._LuaM.Vehicles;
using Content.Shared._LuaM.Vehicles.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Physics;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Timing;

namespace Content.Server._LuaM.Vehicles;

public sealed partial class VehicleAutoTurretSystem : EntitySystem
{
    [Dependency] private NpcFactionSystem _faction = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private VehicleTurretSystem _turret = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<VehicleAutoTurretComponent, VehicleTurretComponent, VehicleDeployableComponent, VehicleSeatsComponent>();
        while (query.MoveNext(out var uid, out var auto, out var turret, out var deploy, out var seats))
        {
            if (!deploy.Deployed || seats.Gunner != null || now < auto.NextTarget)
                continue;

            auto.NextTarget = now + auto.Cooldown;

            if (!IsValidTarget(uid, auto, auto.Target))
                auto.Target = FindTarget(uid, auto);

            if (auto.Target is not { } target)
                continue;

            var targetCoords = _transform.GetMapCoordinates(target);
            _turret.AimAt((uid, turret), targetCoords);

            if (!_turret.TryGetSelectedWeapon(uid, turret, out var weapon, out var gun))
                continue;

            _gun.AttemptShoot(uid, weapon.Value, gun, Transform(target).Coordinates, target);
        }
    }

    private bool IsValidTarget(EntityUid vehicle, VehicleAutoTurretComponent auto, EntityUid? target)
    {
        if (target is not { } t || TerminatingOrDeleted(t) || !_mobState.IsAlive(t))
            return false;

        return _interaction.InRangeUnobstructed(vehicle, t, auto.Range, CollisionGroup.Impassable,
            e => e == vehicle || e == t || Transform(e).ParentUid == vehicle);
    }

    private EntityUid? FindTarget(EntityUid vehicle, VehicleAutoTurretComponent auto)
    {
        if (!TryComp<NpcFactionMemberComponent>(vehicle, out var faction))
            return null;

        var pos = _transform.GetWorldPosition(vehicle);
        EntityUid? best = null;
        var bestDistance = float.MaxValue;

        foreach (var hostile in _faction.GetNearbyHostiles((vehicle, faction, null), auto.Range))
        {
            if (!IsValidTarget(vehicle, auto, hostile))
                continue;

            var distance = (_transform.GetWorldPosition(hostile) - pos).LengthSquared();
            if (distance >= bestDistance)
                continue;

            best = hostile;
            bestDistance = distance;
        }

        return best;
    }
}
