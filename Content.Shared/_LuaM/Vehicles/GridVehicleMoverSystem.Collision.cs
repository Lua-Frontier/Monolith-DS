/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using System.Numerics;
using Content.Shared._LuaM.Vehicles.Components;
using Content.Shared.Damage;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.Vehicles;

public sealed partial class GridVehicleMoverSystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoorSystem _door = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private TagSystem _tag = default!;

    private static readonly ProtoId<TagPrototype> SmashIgnoreTag = "VehicleSmashIgnore";
    private static readonly TimeSpan HitCooldown = TimeSpan.FromSeconds(0.75);

    private readonly Dictionary<EntityUid, TimeSpan> _lastHit = new();

    private void InitializeCollision()
    {
        SubscribeLocalEvent<GridVehicleMoverComponent, ComponentShutdown>((_, _, _) => _lastHit.Clear());
    }

    private bool CanOccupy(
        EntityUid uid,
        GridVehicleMoverComponent mover,
        VehicleSeatsComponent seats,
        EntityUid grid,
        Vector2 localPos,
        Angle localRot,
        bool applyEffects)
    {
        if (!_physicsQuery.TryComp(uid, out var body) || !body.CanCollide ||
            !_fixturesQuery.TryComp(uid, out var fixtures) ||
            !TryGetLocalBounds(fixtures, out var localBounds))
        {
            return true;
        }

        var (gridPos, gridRot) = _transform.GetWorldPositionRotation(grid);
        var worldRot = gridRot + localRot;
        var worldPos = gridPos + gridRot.RotateVec(localPos);
        var mapId = Transform(uid).MapID;
        if (mapId == MapId.Nullspace)
            return true;

        var shrunk = localBounds.Enlarged(-Clearance);
        var hull = new Box2Rotated(shrunk.Translated(worldPos), worldRot, worldPos);

        _intersecting.Clear();
        _lookup.GetEntitiesIntersecting(mapId, hull, _intersecting, LookupFlags.Dynamic | LookupFlags.Static);

        var blocked = false;
        foreach (var (other, otherBody) in _intersecting)
        {
            if (other == uid || other == grid || !otherBody.CanCollide ||
                TerminatingOrDeleted(other) || EntityManager.IsQueuedForDeletion(other))
            {
                continue;
            }

            if (TryComp<VehicleOccupantComponent>(other, out var occupant) && occupant.Vehicle == uid)
                continue;

            var otherXform = Transform(other);
            if (otherXform.ParentUid == uid)
                continue;

            if (!otherXform.Anchored && HasComp<ItemComponent>(other))
                continue;

            if (!_fixturesQuery.TryComp(other, out var otherFixtures) || !IsHardAgainst(fixtures, otherFixtures))
                continue;

            if (TryComp<MobStateComponent>(other, out var mobState))
            {
                if (applyEffects && !_mobState.IsDead(other, mobState) && !_standing.IsDown(other))
                    HitMob(uid, mover, other, worldPos, worldRot);

                continue;
            }

            if (IsMobPassable(otherFixtures))
                continue;

            if (!otherXform.Anchored && otherBody.BodyType != BodyType.Static && !HasComp<GridVehicleMoverComponent>(other))
                continue;

            if (TryComp<DoorComponent>(other, out var door) && seats.Driver is { } driver && applyEffects && !_net.IsClient)
            {
                if (door.State == DoorState.Closed && door.BumpOpen)
                    _door.TryOpen(other, door, driver);
            }

            if (TryComp<VehicleCrushableComponent>(other, out var crushable) && MathF.Abs(mover.CurrentSpeed) >= crushable.MinSpeed)
            {
                if (applyEffects)
                    Crush(uid, mover, (other, crushable));

                continue;
            }

            blocked = true;
            if (applyEffects)
                Ram(uid, mover, other);

            break;
        }

        return !blocked;
    }

    // -- Таран: строение получает урон по скорости, сама машина тоже, после чего она упирается и встаёт
    private void Ram(EntityUid uid, GridVehicleMoverComponent mover, EntityUid target)
    {
        var speed = MathF.Abs(mover.CurrentSpeed);
        if (_net.IsClient || speed < mover.RamMinSpeed || !TryStartHit(target))
            return;

        if (HasComp<DamageableComponent>(target) && !_tag.HasTag(target, SmashIgnoreTag))
        {
            var damage = new DamageSpecifier();
            damage.DamageDict["Structural"] = speed * mover.RamStructureDamage * _hardpoints.GetStructureDamageMultiplier(uid);
            _damageable.TryChangeDamage(target, damage, true, origin: uid);
        }

        DamageHull(uid, speed * mover.RamHullDamage, target);
        PlayCollisionSound(uid);
    }

    // -- Деревья на скорости сносятся целиком: остаётся одно бревно, машина проезжает дальше, теряя скорость
    private void Crush(EntityUid uid, GridVehicleMoverComponent mover, Entity<VehicleCrushableComponent> target)
    {
        if (_net.IsClient)
            return;

        mover.CurrentSpeed *= target.Comp.SpeedMultiplier;
        var coords = Transform(target).Coordinates;
        _audio.PlayPvs(target.Comp.Sound, coords);
        if (target.Comp.Drop is { } drop)
            Spawn(drop, coords);

        QueueDel(target);
        DamageHull(uid, target.Comp.HullDamage, target);
        PlayCollisionSound(uid, true);
    }

    private void HitMob(EntityUid uid, GridVehicleMoverComponent mover, EntityUid mob, Vector2 hullPos, Angle hullRot)
    {
        if (_net.IsClient)
            return;

        var speed = MathF.Abs(mover.CurrentSpeed);
        if (speed >= mover.RamMinSpeed && TryStartHit(mob))
        {
            var damage = new DamageSpecifier();
            damage.DamageDict["Blunt"] = speed * mover.MobRamDamage;
            _damageable.TryChangeDamage(mob, damage, origin: uid);
            _stun.TryKnockdown(mob, TimeSpan.FromSeconds(mover.MobKnockdownTime), true);

            DamageHull(uid, mover.MobCollisionHullDamage, mob);
            mover.CurrentSpeed *= mover.MobRamSpeedMultiplier;

            if (TryComp<VehicleSoundComponent>(uid, out var sound))
                _audio.PlayPvs(sound.MobCollisionSound, uid);
        }

        var mobPos = _transform.GetWorldPosition(mob);
        var right = hullRot.RotateVec(new Vector2(1f, 0f));
        var side = Vector2.Dot(mobPos - hullPos, right) >= 0f ? 1f : -1f;
        _transform.SetWorldPosition(mob, mobPos + right * side * 0.6f);
    }

    private bool TryStartHit(EntityUid target)
    {
        var now = _timing.CurTime;
        if (_lastHit.TryGetValue(target, out var last) && now < last + HitCooldown)
            return false;

        if (_lastHit.Count > 256)
            _lastHit.Clear();

        _lastHit[target] = now;
        return true;
    }

    private void DamageHull(EntityUid uid, float amount, EntityUid origin)
    {
        if (amount <= 0f)
            return;

        var hull = new DamageSpecifier();
        hull.DamageDict["Blunt"] = amount;
        _damageable.TryChangeDamage(uid, hull, true, origin: origin);
    }

    private void PlayCollisionSound(EntityUid uid, bool force = false)
    {
        if (_net.IsClient || !TryComp<VehicleSoundComponent>(uid, out var sound) || sound.CollisionSound == null)
            return;

        var now = _timing.CurTime;
        if (!force && now < sound.NextCollisionSound)
            return;

        sound.NextCollisionSound = now + TimeSpan.FromSeconds(sound.CollisionSoundCooldown);
        _audio.PlayPvs(sound.CollisionSound, uid);
    }

    private static bool TryGetLocalBounds(FixturesComponent fixtures, out Box2 bounds)
    {
        bounds = default;
        var found = false;
        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (!fixture.Hard)
                continue;

            var box = fixture.Shape switch
            {
                PolygonShape poly => poly.ComputeAABB(Robust.Shared.Physics.Transform.Empty, 0),
                PhysShapeCircle circle => Box2.CenteredAround(circle.Position, new Vector2(circle.Radius * 2f)),
                _ => fixture.Shape.ComputeAABB(Robust.Shared.Physics.Transform.Empty, 0),
            };

            bounds = found ? bounds.Union(box) : box;
            found = true;
        }

        return found;
    }

    private static bool IsHardAgainst(FixturesComponent ours, FixturesComponent theirs)
    {
        foreach (var a in ours.Fixtures.Values)
        {
            if (!a.Hard)
                continue;

            foreach (var b in theirs.Fixtures.Values)
            {
                if (!b.Hard)
                    continue;

                if ((a.CollisionMask & b.CollisionLayer) != 0 || (a.CollisionLayer & b.CollisionMask) != 0)
                    return true;
            }
        }

        return false;
    }

    private static bool IsMobPassable(FixturesComponent fixtures)
    {
        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (fixture.Hard && ((fixture.CollisionMask & MobPassLayer) != 0 || (fixture.CollisionLayer & MobPassMask) != 0))
                return false;
        }

        return true;
    }
}
