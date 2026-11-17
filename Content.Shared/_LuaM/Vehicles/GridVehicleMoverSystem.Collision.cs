/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared._LuaM.Vehicles.Components;
using Content.Shared.Damage;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Tiles;
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
    [Dependency] private ThrowingSystem _throwing = default!;

    [Dependency] private VehicleSmashRuleSystem _smashRules = default!;

    private static readonly ProtoId<TagPrototype> SmashIgnoreTag = "VehicleSmashIgnore";
    private static readonly TimeSpan HitCooldown = TimeSpan.FromSeconds(0.75);
    private const int HitCooldownPruneCount = 16;
    private const float ImpactRestitution = 0.15f;
    private const float MaxImpactSpinDegrees = 90f;

    private readonly List<EntityUid> _expiredHits = new();
    private readonly List<(EntityUid Target, VehicleSmashRulePrototype Rule)> _smashes = new();
    private EntityUid? _lastBlocker;
    private EntityUid? _ignoreInProbe;

    // -- То, что не меняется между пробами одного тика: считается один раз, а не на каждую пробу
    private readonly record struct Hull(
        FixturesComponent? Fixtures,
        Box2 Bounds,
        EntityUid Grid,
        Vector2 GridPos,
        Angle GridRot,
        MapId Map);

    private Hull GetHull(EntityUid uid, EntityUid grid, TransformComponent xform)
    {
        if (xform.MapID == MapId.Nullspace ||
            !_physicsQuery.TryComp(uid, out var body) || !body.CanCollide ||
            !_fixturesQuery.TryComp(uid, out var fixtures) ||
            !TryGetLocalBounds(fixtures, out var localBounds))
        {
            return default;
        }

        var (gridPos, gridRot) = _transform.GetWorldPositionRotation(grid);
        return new Hull(fixtures, localBounds.Enlarged(-Clearance), grid, gridPos, gridRot, xform.MapID);
    }

    private bool CanOccupy(
        EntityUid uid,
        GridVehicleMoverComponent mover,
        VehicleSeatsComponent seats,
        in Hull hullData,
        Vector2 localPos,
        Angle localRot,
        bool applyEffects)
    {
        if (hullData.Fixtures is not { } fixtures)
            return true;

        var grid = hullData.Grid;
        var worldRot = hullData.GridRot + localRot;
        var worldPos = hullData.GridPos + hullData.GridRot.RotateVec(localPos);
        var hull = new Box2Rotated(hullData.Bounds.Translated(worldPos), worldRot, worldPos);

        _intersecting.Clear();
        _lookup.GetEntitiesIntersecting(hullData.Map, hull, _intersecting, LookupFlags.Dynamic | LookupFlags.Static);

        var blocked = false;
        _lastBlocker = null;
        _smashes.Clear();
        foreach (var (other, otherBody) in _intersecting)
        {
            if (other == uid || other == grid || other == _ignoreInProbe || !otherBody.CanCollide ||
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

            if (TryComp<VehicleCrushableComponent>(other, out var crushable))
            {
                if (MathF.Abs(mover.CurrentSpeed) >= crushable.MinSpeed * mover.CrushMinSpeedScale)
                {
                    if (applyEffects)
                        Crush(uid, mover, (other, crushable));

                    continue;
                }
            }
            else if (TryGetSmash(mover, other, out var rule))
            {
                _smashes.Add((other, rule));
                continue;
            }

            blocked = true;
            _lastBlocker = other;
            if (applyEffects)
                Ram(uid, mover, other);

            break;
        }

        if (blocked)
            return false;

        // -- Сносим только когда больше ничто не держит машину: иначе решётка под окном, которое машине не по силам,
        // -- разлетится, а само окно останется висеть
        foreach (var (target, rule) in _smashes)
        {
            if (applyEffects)
                Smash(uid, mover, target, rule);

            if (mover.WeightClass < rule.MinContinueWeightClass)
            {
                blocked = true;
                _lastBlocker = target;
            }
        }

        return !blocked;
    }

    private bool TryGetSmash(GridVehicleMoverComponent mover, EntityUid target, [NotNullWhen(true)] out VehicleSmashRulePrototype? rule)
    {
        rule = null;
        if (rule == null || mover.WeightClass < rule.MinDestroyWeightClass)
            return false;

        // -- На станциях, защищённых от взрывов, машина ничего не сносит, а упирается как раньше
        if (Transform(target).GridUid is { } grid &&
            TryComp<ProtectedGridComponent>(grid, out var protectedGrid) && protectedGrid.PreventExplosions)
        {
            return false;
        }

        return !rule.RequiresDoorUnpowered ||
               !(TryComp<AirlockComponent>(target, out var airlock) && airlock.Powered ||
                 TryComp<FirelockComponent>(target, out var firelock) && firelock.Powered);
    }

    private void Smash(EntityUid uid, GridVehicleMoverComponent mover, EntityUid target, VehicleSmashRulePrototype rule)
    {
        if (mover.WeightClass < rule.SlowdownBelowWeightClass)
        {
            mover.SmashSlowMultiplier = rule.SlowdownMultiplier;
            mover.SmashSlowUntil = _timing.CurTime + TimeSpan.FromSeconds(rule.SlowdownDuration);
        }

        if (_net.IsClient)
            return;

        var coords = Transform(target).Coordinates;
        var damage = new DamageSpecifier();
        damage.DamageDict["Structural"] = rule.Damage;
        damage.DamageDict["Blunt"] = rule.Damage;
        var dealt = _damageable.TryChangeDamage(target, damage, true, origin: uid);

        // -- Что устояло после удара, всё равно убираем: правило обещает проезд
        if (dealt != null && dealt.GetTotal() > 0 && !TerminatingOrDeleted(target) && !EntityManager.IsQueuedForDeletion(target))
            QueueDel(target);

        if (rule.SmashSound != null)
            _audio.PlayPvs(rule.SmashSound, coords);

        PlayCollisionSound(uid);
    }

    private bool CanPush(GridVehicleMoverComponent mover, GridVehicleMoverComponent pushed)
    {
        return mover.CanPushVehicles && !pushed.Floating &&
               MathF.Abs(mover.CurrentSpeed) <= mover.PushMaxSpeed &&
               pushed.WeightClass <= mover.WeightClass;
    }

    private bool TryPush(EntityUid uid, GridVehicleMoverComponent mover, EntityUid target, Vector2 delta)
    {
        if (!TryComp<GridVehicleMoverComponent>(target, out var pushed) || !CanPush(mover, pushed) ||
            !TryComp<VehicleSeatsComponent>(target, out var pushedSeats))
        {
            return false;
        }

        var xform = Transform(uid);
        var targetXform = Transform(target);
        if (targetXform.ParentUid != xform.ParentUid || targetXform.GridUid is not { } grid)
            return false;

        var hull = GetHull(target, grid, targetXform);
        var position = targetXform.LocalPosition + delta;
        _ignoreInProbe = uid;
        var free = CanOccupy(target, pushed, pushedSeats, hull, position, targetXform.LocalRotation, false);
        _ignoreInProbe = null;
        if (!free)
            return false;

        _transform.SetLocalPositionNoLerp(target, position, targetXform);
        _physics.WakeBody(target);
        return true;
    }

    // -- Удар машины о машину: импульс по массам, лёгкую отбрасывает и закручивает, таранящая теряет ход
    private void RamVehicle(EntityUid uid, GridVehicleMoverComponent mover, Entity<GridVehicleMoverComponent> struck)
    {
        if (_net.IsClient || struck.Comp.Floating || CanPush(mover, struck.Comp))
            return;

        var xform = Transform(uid);
        var otherXform = Transform(struck);
        if (otherXform.ParentUid != xform.ParentUid)
            return;

        var delta = otherXform.LocalPosition - xform.LocalPosition;
        if (delta.LengthSquared() < 0.001f)
            return;

        var normal = Vector2.Normalize(delta);
        var velocity = GetLocalVelocity(mover, xform);
        var closing = Vector2.Dot(velocity - GetLocalVelocity(struck.Comp, otherXform), normal);
        if (closing <= 0.05f || !TryStartHit(mover, struck))
            return;

        var (mass, inertia, reach) = GetBodyMetrics(uid);
        var (otherMass, otherInertia, _) = GetBodyMetrics(struck);
        var impulse = normal * ((1f + ImpactRestitution) * closing / (1f / mass + 1f / otherMass));

        struck.Comp.ImpactVelocity += impulse / otherMass;
        mover.ImpactVelocity = velocity - impulse / mass;
        mover.CurrentSpeed = 0f;
        mover.DriftDegrees = 0f;

        var heading = xform.LocalRotation - Angle.FromDegrees(mover.DriftDegrees);
        var contact = xform.LocalPosition + heading.ToWorldVec() * MathF.Sign(mover.CurrentSpeed) * reach;
        struck.Comp.ImpactSpinDegrees += GetSpin(contact - otherXform.LocalPosition, impulse, otherInertia);
        mover.ImpactSpinDegrees += GetSpin(contact - xform.LocalPosition, -impulse, inertia);

        Dirty(struck);
        Dirty(uid, mover);

        if (closing < mover.RamMinSpeed)
            return;

        var heavier = Math.Clamp(1f + 0.25f * ((int) mover.WeightClass - (int) struck.Comp.WeightClass), 0.5f, 2f);
        var damage = new DamageSpecifier();
        damage.DamageDict["Structural"] = closing * mover.RamStructureDamage * heavier * _hardpoints.GetStructureDamageMultiplier(uid);
        _damageable.TryChangeDamage(struck, damage, true, origin: uid);
        DamageHull(uid, closing * mover.RamHullDamage / heavier, struck);
        PlayCollisionSound(uid);

        var parentRot = _transform.GetWorldRotation(xform) - xform.LocalRotation;
        var crash = new VehicleCrashEvent(closing, _transform.GetWorldRotation(uid).ToWorldVec() * MathF.Sign(mover.CurrentSpeed));
        RaiseLocalEvent(uid, ref crash);

        var struckCrash = new VehicleCrashEvent((impulse / otherMass).Length(), parentRot.RotateVec(normal));
        RaiseLocalEvent(struck, ref struckCrash);
    }

    private static Vector2 GetLocalVelocity(GridVehicleMoverComponent mover, TransformComponent xform)
    {
        var heading = xform.LocalRotation - Angle.FromDegrees(mover.DriftDegrees);
        return heading.ToWorldVec() * mover.CurrentSpeed + mover.ImpactVelocity;
    }

    private (float Mass, float Inertia, float Reach) GetBodyMetrics(EntityUid uid)
    {
        var mass = _physicsQuery.TryComp(uid, out var body) ? MathF.Max(1f, body.Mass) : 1f;
        if (!_fixturesQuery.TryComp(uid, out var fixtures) || !TryGetLocalBounds(fixtures, out var bounds))
            return (mass, mass, 0.5f);

        var inertia = mass * (bounds.Width * bounds.Width + bounds.Height * bounds.Height) / 12f;
        return (mass, MathF.Max(1f, inertia), MathF.Max(bounds.Width, bounds.Height) / 2f);
    }

    private static float GetSpin(Vector2 arm, Vector2 impulse, float inertia)
    {
        var torque = arm.X * impulse.Y - arm.Y * impulse.X;
        return Math.Clamp(MathHelper.RadiansToDegrees(torque / inertia), -MaxImpactSpinDegrees, MaxImpactSpinDegrees);
    }

    // -- Таран: строение получает урон по скорости, сама машина тоже, после чего она упирается и встаёт
    private void Ram(EntityUid uid, GridVehicleMoverComponent mover, EntityUid target)
    {
        if (TryComp<GridVehicleMoverComponent>(target, out var struck))
        {
            RamVehicle(uid, mover, (target, struck));
            return;
        }

        var speed = MathF.Abs(mover.CurrentSpeed);
        if (_net.IsClient || speed < mover.RamMinSpeed || !TryStartHit(mover, target))
            return;

        if (HasComp<DamageableComponent>(target) && !_tag.HasTag(target, SmashIgnoreTag))
        {
            var damage = new DamageSpecifier();
            damage.DamageDict["Structural"] = speed * mover.RamStructureDamage * _hardpoints.GetStructureDamageMultiplier(uid);
            _damageable.TryChangeDamage(target, damage, true, origin: uid);
        }

        DamageHull(uid, speed * mover.RamHullDamage, target);
        PlayCollisionSound(uid);

        var crash = new VehicleCrashEvent(speed, _transform.GetWorldRotation(uid).ToWorldVec() * MathF.Sign(mover.CurrentSpeed));
        RaiseLocalEvent(uid, ref crash);
    }

    // -- Деревья на скорости сносятся целиком: остаётся одно бревно, машина проезжает дальше, теряя скорость
    private void Crush(EntityUid uid, GridVehicleMoverComponent mover, Entity<VehicleCrushableComponent> target)
    {
        if (_net.IsClient)
            return;

        mover.CurrentSpeed *= mover.CrushSpeedRetention ?? target.Comp.SpeedMultiplier;
        var coords = Transform(target).Coordinates;
        _audio.PlayPvs(target.Comp.Sound, coords);
        if (target.Comp.Drop is { } drop)
            Spawn(drop, coords);

        QueueDel(target);
        DamageHull(uid, target.Comp.HullDamage * mover.CrushHullDamageScale, target);
        PlayCollisionSound(uid, true);
    }

    private void HitMob(EntityUid uid, GridVehicleMoverComponent mover, EntityUid mob, Vector2 hullPos, Angle hullRot)
    {
        if (_net.IsClient)
            return;

        var mobPos = _transform.GetWorldPosition(mob);
        var right = hullRot.RotateVec(new Vector2(1f, 0f));
        var side = Vector2.Dot(mobPos - hullPos, right) >= 0f ? 1f : -1f;
        _transform.SetWorldPosition(mob, mobPos + right * side * 0.6f);

        var speed = MathF.Abs(mover.CurrentSpeed);
        if (speed < mover.RamMinSpeed || !TryStartHit(mover, mob))
            return;

        var damage = new DamageSpecifier();
        damage.DamageDict["Blunt"] = speed * mover.MobRamDamage;
        _damageable.TryChangeDamage(mob, damage, origin: uid);
        _stun.TryKnockdown(mob, TimeSpan.FromSeconds(mover.MobKnockdownTime), true);

        var forward = hullRot.ToWorldVec() * MathF.Sign(mover.CurrentSpeed);
        var direction = Vector2.Normalize(forward + right * side * mover.MobThrowSideRatio);
        var distance = Math.Clamp(speed * mover.MobThrowDistancePerSpeed, mover.MobThrowMinDistance, mover.MobThrowMaxDistance);
        _throwing.TryThrow(mob, direction * distance, MathF.Max(speed, mover.MobThrowMinSpeed), uid,
            pushbackRatio: 0f, recoil: false, playSound: false, doSpin: false);

        DamageHull(uid, mover.MobCollisionHullDamage, mob);
        mover.CurrentSpeed *= mover.MobRamSpeedMultiplier;

        if (TryComp<VehicleSoundComponent>(uid, out var sound))
            _audio.PlayPvs(sound.MobCollisionSound, uid);
    }

    private bool TryStartHit(GridVehicleMoverComponent mover, EntityUid target)
    {
        var now = _timing.CurTime;
        if (mover.HitCooldowns.TryGetValue(target, out var until) && now < until)
            return false;

        if (mover.HitCooldowns.Count >= HitCooldownPruneCount)
        {
            _expiredHits.Clear();
            foreach (var (hit, end) in mover.HitCooldowns)
            {
                if (now >= end)
                    _expiredHits.Add(hit);
            }

            foreach (var hit in _expiredHits)
            {
                mover.HitCooldowns.Remove(hit);
            }
        }

        mover.HitCooldowns[target] = now + HitCooldown;
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

    internal static bool TryGetLocalBounds(FixturesComponent fixtures, out Box2 bounds)
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
