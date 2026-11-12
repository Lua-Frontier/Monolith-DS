/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using System.Numerics;
using Content.Shared._LuaM.Vehicles.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Damage;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._LuaM.Vehicles;

public sealed partial class GridVehicleMoverSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private HardpointSystem _hardpoints = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const float FixedStep = 1f / 60f;
    private const int MaxStepsPerFrame = 6;
    private const float MinSpeed = 0.01f;
    private const float MinMove = 0.0001f;
    private const float Clearance = PhysicsConstants.PolygonRadius * 0.75f;
    private const int MobPassMask = (int) CollisionGroup.MobMask;
    private const int MobPassLayer = (int) CollisionGroup.MobLayer;

    private EntityQuery<FixturesComponent> _fixturesQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;

    private readonly HashSet<Entity<PhysicsComponent>> _intersecting = new();

    public override void Initialize()
    {
        base.Initialize();

        _fixturesQuery = GetEntityQuery<FixturesComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();

        SubscribeLocalEvent<GridVehicleMoverComponent, PreventCollideEvent>(OnPreventCollide);
        InitializeCollision();
    }

    private void OnPreventCollide(Entity<GridVehicleMoverComponent> ent, ref PreventCollideEvent args)
    {
        if (args.Cancelled)
            return;

        if (args.OtherBody.BodyType == BodyType.Static)
        {
            args.Cancelled = true;
            return;
        }

        if (HasComp<MobStateComponent>(args.OtherEntity) &&
            (_mobState.IsDead(args.OtherEntity) || _standing.IsDown(args.OtherEntity)))
        {
            args.Cancelled = true;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<GridVehicleMoverComponent, VehicleSeatsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var mover, out var seats, out var xform))
        {
            if (_net.IsClient && (!_timing.InPrediction || seats.Driver == null || seats.Driver != _player.LocalEntity))
                continue;

            if (xform.GridUid is not { } grid || xform.ParentUid != grid)
                continue;

            var (throttle, steering) = GetInput(uid, seats);
            var wasMoving = mover.IsMoving;

            mover.Accumulator = MathF.Min(mover.Accumulator + frameTime, FixedStep * MaxStepsPerFrame);
            while (mover.Accumulator >= FixedStep)
            {
                Step((uid, mover, xform), seats, grid, throttle, steering, FixedStep);
                mover.Accumulator -= FixedStep;
            }

            mover.IsMoving = MathF.Abs(mover.CurrentSpeed) > MinSpeed;
            if (mover.IsMoving != wasMoving)
            {
                var ev = new SpriteMoveEvent(mover.IsMoving);
                RaiseLocalEvent(uid, ref ev);
            }

            if (mover.IsMoving)
                PlayRunningSound(uid);

            Dirty(uid, mover);
        }
    }

    private (float Throttle, float Steering) GetInput(EntityUid uid, VehicleSeatsComponent seats)
    {
        if (seats.Driver is not { } driver ||
            !TryComp<InputMoverComponent>(driver, out var input) ||
            !_actionBlocker.CanConsciouslyPerformAction(driver))
        {
            return (0f, 0f);
        }

        var ev = new VehicleCanMoveEvent();
        RaiseLocalEvent(uid, ref ev);
        if (ev.Cancelled)
            return (0f, 0f);

        var buttons = input.HeldMoveButtons;
        var throttle = 0f;
        var steering = 0f;

        if ((buttons & MoveButtons.Up) != 0)
            throttle += 1f;
        if ((buttons & MoveButtons.Down) != 0)
            throttle -= 1f;
        if ((buttons & MoveButtons.Left) != 0)
            steering += 1f;
        if ((buttons & MoveButtons.Right) != 0)
            steering -= 1f;

        return (throttle, steering);
    }

    private void Step(
        Entity<GridVehicleMoverComponent, TransformComponent> ent,
        VehicleSeatsComponent seats,
        EntityUid grid,
        float throttle,
        float steering,
        float dt)
    {
        var (uid, mover, xform) = ent;

        if (_timing.CurTime < mover.ImmobileUntil)
        {
            if ((throttle != 0f || steering != 0f) && seats.Driver is { } driver && _timing.CurTime >= mover.NextImmobilePopup)
            {
                mover.NextImmobilePopup = _timing.CurTime + TimeSpan.FromSeconds(2);
                _popup.PopupClient(Loc.GetString("luam-vehicle-immobile"), uid, driver, PopupType.SmallCaution);
            }

            throttle = 0f;
            steering = 0f;
        }

        var rotation = xform.LocalRotation;
        var position = xform.LocalPosition;

        if (MathF.Abs(mover.CurrentSpeed) > MinSpeed)
        {
            var reversing = mover.CurrentSpeed < -MinSpeed;
            var target = (reversing ? -steering : steering) * mover.MaxRotationSpeedDegrees;
            var accel = steering == 0f ? mover.RotationDecelerationDegrees : mover.RotationAccelerationDegrees;
            mover.AngularVelocityDegrees = StepTowards(mover.AngularVelocityDegrees, target, accel * dt);
        }
        else
        {
            mover.AngularVelocityDegrees = 0f;
        }

        if (steering == 0f && throttle != 0f && mover.AlignmentAssistDegrees > 0f &&
            MathF.Abs(mover.CurrentSpeed) > MinSpeed &&
            MathF.Abs(mover.CurrentSpeed) <= mover.AlignmentAssistMaxSpeed)
        {
            var delta = Angle.ShortestDistance(rotation, rotation.GetCardinalDir().ToAngle()).Degrees;
            if (Math.Abs(delta) <= mover.AlignmentAssistDegrees)
                mover.AngularVelocityDegrees = (float) Math.Clamp(delta / dt, -30, 30);
        }

        if (MathF.Abs(mover.AngularVelocityDegrees) > 0.001f)
        {
            var desired = rotation + Angle.FromDegrees(mover.AngularVelocityDegrees * dt);
            if (CanOccupy(uid, mover, seats, grid, position, desired, true))
            {
                rotation = desired;
                _transform.SetLocalRotationNoLerp(uid, rotation, xform);
            }
            else
            {
                mover.AngularVelocityDegrees = 0f;
            }
        }

        var maxSpeed = GetMaxSpeed(uid, mover, false);
        var maxReverse = GetMaxSpeed(uid, mover, true);

        float targetSpeed;
        float rate;
        if (throttle > 0f)
        {
            targetSpeed = mover.CurrentSpeed < 0f ? 0f : maxSpeed;
            rate = mover.CurrentSpeed < 0f ? mover.Deceleration : mover.Acceleration;
        }
        else if (throttle < 0f)
        {
            targetSpeed = mover.CurrentSpeed > 0f ? 0f : -maxReverse;
            rate = mover.CurrentSpeed > 0f ? mover.Deceleration : mover.ReverseAcceleration;
        }
        else
        {
            targetSpeed = 0f;
            rate = mover.Deceleration;
        }

        if (MathF.Abs(mover.CurrentSpeed) > MathF.Abs(targetSpeed) && throttle != 0f && MathF.Sign(mover.CurrentSpeed) == MathF.Sign(targetSpeed))
            rate = mover.Deceleration;

        mover.CurrentSpeed = StepTowards(mover.CurrentSpeed, targetSpeed, rate * dt);

        var travel = MathF.Abs(mover.CurrentSpeed) * dt;
        if (travel <= MinMove)
            return;

        var forward = rotation.ToWorldVec() * MathF.Sign(mover.CurrentSpeed);
        var start = position;
        var steps = Math.Max(1, (int) MathF.Ceiling(travel / Math.Clamp(mover.ProbeStep, 0.02f, 0.5f)));
        var lastGood = start;
        var blocked = false;
        var impactSpeed = MathF.Abs(mover.CurrentSpeed);

        for (var i = 1; i <= steps; i++)
        {
            var candidate = start + forward * (travel * i / steps);
            if (!CanOccupy(uid, mover, seats, grid, candidate, rotation, true))
            {
                blocked = true;
                break;
            }

            lastGood = candidate;
        }

        if (lastGood != start)
            _transform.SetLocalPositionNoLerp(uid, lastGood, xform);

        if (blocked)
        {
            mover.CurrentSpeed = 0f;
            if (mover.CrashImmobileDuration > 0f && impactSpeed >= mover.CrashImmobileMinSpeed)
                SetImmobile(uid, mover, mover.CrashImmobileDuration);
        }

        _physics.WakeBody(uid);
    }

    private float GetMaxSpeed(EntityUid uid, GridVehicleMoverComponent mover, bool reverse)
    {
        if (_timing.CurTime < mover.ImmobileUntil)
            return 0f;

        var speed = reverse ? mover.MaxReverseSpeed : mover.MaxSpeed;

        if (_timing.CurTime < mover.SmashSlowdownUntil)
            speed *= mover.SmashSlowdownMultiplier;

        if (TryComp<VehicleOverchargeComponent>(uid, out var overcharge) && _timing.CurTime < overcharge.ActiveUntil)
            speed *= overcharge.SpeedMultiplier;

        if (TryComp<VehicleIntegrityComponent>(uid, out var integrity) &&
            TryComp<DamageableComponent>(uid, out var damageable) &&
            integrity.MaxIntegrity > 0f)
        {
            var ratio = Math.Clamp(1f - damageable.TotalDamage.Float() / integrity.MaxIntegrity, 0f, 1f);
            if (ratio < mover.FullSpeedIntegrityFraction && mover.FullSpeedIntegrityFraction > 0f)
                speed *= MathHelper.Lerp(mover.SpeedAtZeroIntegrity, 1f, ratio / mover.FullSpeedIntegrityFraction);
        }

        return speed;
    }

    private void SetImmobile(EntityUid uid, GridVehicleMoverComponent mover, float seconds)
    {
        var until = _timing.CurTime + TimeSpan.FromSeconds(seconds);
        if (until <= mover.ImmobileUntil)
            return;

        mover.ImmobileUntil = until;
        mover.CurrentSpeed = 0f;
        mover.AngularVelocityDegrees = 0f;

        if (TryComp<VehicleSeatsComponent>(uid, out var seats) && seats.Driver is { } driver)
            _popup.PopupClient(Loc.GetString("luam-vehicle-crash"), uid, driver, PopupType.MediumCaution);
    }

    public void Stop(EntityUid uid, GridVehicleMoverComponent? mover = null)
    {
        if (!Resolve(uid, ref mover, false))
            return;

        mover.CurrentSpeed = 0f;
        mover.AngularVelocityDegrees = 0f;
        Dirty(uid, mover);
    }

    private static float StepTowards(float current, float target, float maxDelta)
    {
        if (current < target)
            return MathF.Min(current + maxDelta, target);

        if (current > target)
            return MathF.Max(current - maxDelta, target);

        return current;
    }
}
