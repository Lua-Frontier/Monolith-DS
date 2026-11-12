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
    private static readonly TimeSpan MobHitCooldown = TimeSpan.FromSeconds(0.75);

    private readonly Dictionary<EntityUid, TimeSpan> _lastMobHit = new();
    private readonly Dictionary<(EntityUid, EntityUid), TimeSpan> _smashCooldowns = new();

    private void InitializeCollision()
    {
        SubscribeLocalEvent<GridVehicleMoverComponent, ComponentShutdown>((_, _, _) => _lastMobHit.Clear());
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
            if (other == uid || other == grid || !otherBody.CanCollide || TerminatingOrDeleted(other))
                continue;

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
                if (_mobState.IsDead(other, mobState) || _standing.IsDown(other))
                    continue;

                if (MathF.Abs(mover.CurrentSpeed) < mover.MobMinImpactSpeed)
                {
                    blocked = true;
                    break;
                }

                if (applyEffects)
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

            if (applyEffects && TrySmash(uid, mover, other))
                continue;

            blocked = true;
            if (applyEffects)
                PlayCollisionSound(uid);

            break;
        }

        return !blocked;
    }

    private bool TrySmash(EntityUid uid, GridVehicleMoverComponent mover, EntityUid target)
    {
        var speed = MathF.Abs(mover.CurrentSpeed);
        if (!mover.CanSmashWalls || speed < mover.WallSmashMinSpeed ||
            HasComp<GridVehicleMoverComponent>(target) ||
            !HasComp<DamageableComponent>(target) ||
            _tag.HasTag(target, SmashIgnoreTag))
        {
            return false;
        }

        if (_net.IsClient)
            return false;

        var now = _timing.CurTime;
        if (_smashCooldowns.TryGetValue((uid, target), out var next) && now < next)
            return false;

        _smashCooldowns[(uid, target)] = now + TimeSpan.FromSeconds(0.5);

        var multiplier = mover.WallSmashDamageMultiplier * _hardpoints.GetStructureDamageMultiplier(uid);
        var damage = new DamageSpecifier();
        damage.DamageDict["Structural"] = speed * speed * multiplier;
        _damageable.TryChangeDamage(target, damage, true, origin: uid);

        if (mover.WallSmashHullDamage > 0f)
        {
            var hull = new DamageSpecifier();
            hull.DamageDict["Blunt"] = mover.WallSmashHullDamage;
            _damageable.TryChangeDamage(uid, hull, true, origin: uid);
        }

        mover.SmashSlowdownMultiplier = mover.WallSmashSlowdownMultiplier;
        mover.SmashSlowdownUntil = now + TimeSpan.FromSeconds(mover.WallSmashSlowdownDuration);
        mover.CurrentSpeed *= mover.WallSmashSlowdownMultiplier;
        PlayCollisionSound(uid, true);

        return TerminatingOrDeleted(target) || EntityManager.IsQueuedForDeletion(target) ||
               !_physicsQuery.TryComp(target, out var targetBody) || !targetBody.CanCollide;
    }

    private void HitMob(EntityUid uid, GridVehicleMoverComponent mover, EntityUid mob, Vector2 hullPos, Angle hullRot)
    {
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        if (_lastMobHit.TryGetValue(mob, out var last) && now < last + MobHitCooldown)
            return;

        _lastMobHit[mob] = now;
        var speed = MathF.Abs(mover.CurrentSpeed);

        var damage = new DamageSpecifier();
        damage.DamageDict["Blunt"] = mover.MobCollisionDamage;
        _damageable.TryChangeDamage(mob, damage, origin: uid);
        _stun.TryKnockdown(mob, TimeSpan.FromSeconds(mover.MobKnockdownTime), true);

        if (mover.MobCollisionHullDamage > 0f)
        {
            var hull = new DamageSpecifier();
            hull.DamageDict["Blunt"] = mover.MobCollisionHullDamage;
            _damageable.TryChangeDamage(uid, hull, true, origin: mob);
        }

        if (TryComp<VehicleSoundComponent>(uid, out var sound) && sound.MobCollisionSound != null)
            _audio.PlayPvs(sound.MobCollisionSound, uid);

        var mobPos = _transform.GetWorldPosition(mob);
        var right = hullRot.RotateVec(new Vector2(1f, 0f));
        var side = Vector2.Dot(mobPos - hullPos, right) >= 0f ? 1f : -1f;
        _transform.SetWorldPosition(mob, mobPos + right * side * 0.6f);

        if (mover.MobCrashImmobileDuration > 0f && speed >= mover.MobCrashImmobileMinSpeed)
            SetImmobile(uid, mover, mover.MobCrashImmobileDuration);
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

    private void PlayRunningSound(EntityUid uid)
    {
        if (_net.IsClient || !TryComp<VehicleSoundComponent>(uid, out var sound) || sound.RunningSound == null)
            return;

        var now = _timing.CurTime;
        if (now < sound.NextRunningSound)
            return;

        sound.NextRunningSound = now + TimeSpan.FromSeconds(sound.RunningSoundCooldown);
        _audio.PlayPvs(sound.RunningSound, uid);
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
