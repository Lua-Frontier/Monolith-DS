/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using System.Numerics;
using Content.Shared._LuaM.Vehicles.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Damage;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Physics;
using Content.Shared.Standing;
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
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const float FixedStep = 1f / 60f;
    private const int MaxStepsPerFrame = 6;
    private const float MinSpeed = 0.01f;
    private const float MinMove = 0.0001f;
    private const float HardTurnFraction = 0.8f;
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

            var (throttle, steering, handbrake) = GetInput(uid, seats);
            var wasMoving = mover.IsMoving;
            var wasDrifting = mover.DriftDegrees != 0f;
            var handbrakeChanged = mover.Handbrake != handbrake;
            mover.Handbrake = handbrake;

            mover.Accumulator = MathF.Min(mover.Accumulator + frameTime, FixedStep * MaxStepsPerFrame);
            while (mover.Accumulator >= FixedStep)
            {
                Step((uid, mover, xform), seats, grid, throttle, steering, handbrake, FixedStep);
                mover.Accumulator -= FixedStep;
            }

            mover.IsMoving = MathF.Abs(mover.CurrentSpeed) > MinSpeed;
            if (mover.IsMoving != wasMoving)
            {
                var ev = new SpriteMoveEvent(mover.IsMoving);
                RaiseLocalEvent(uid, ref ev);
            }

            if (mover.IsMoving || wasMoving || wasDrifting || handbrakeChanged || mover.AngularVelocityDegrees != 0f)
                Dirty(uid, mover);
        }
    }

    private (float Throttle, float Steering, bool Handbrake) GetInput(EntityUid uid, VehicleSeatsComponent seats)
    {
        if (seats.Driver is not { } driver ||
            !TryComp<InputMoverComponent>(driver, out var input) ||
            !_actionBlocker.CanConsciouslyPerformAction(driver))
        {
            return (0f, 0f, false);
        }

        var ev = new VehicleCanMoveEvent();
        RaiseLocalEvent(uid, ref ev);
        if (ev.Cancelled)
            return (0f, 0f, false);

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

        return (throttle, steering, (buttons & MoveButtons.Walk) != 0);
    }

    private void Step(
        Entity<GridVehicleMoverComponent, TransformComponent> ent,
        VehicleSeatsComponent seats,
        EntityUid grid,
        float throttle,
        float steering,
        bool handbrake,
        float dt)
    {
        var (uid, mover, xform) = ent;

        var rotation = xform.LocalRotation;
        var position = xform.LocalPosition;

        if (MathF.Abs(mover.CurrentSpeed) > MinSpeed)
        {
            var reversing = mover.CurrentSpeed < -MinSpeed;
            var target = (reversing ? -steering : steering) * mover.MaxRotationSpeedDegrees;
            if (handbrake)
                target *= mover.HandbrakeTurnMultiplier;

            var accel = steering == 0f ? mover.RotationDecelerationDegrees : mover.RotationAccelerationDegrees;
            mover.AngularVelocityDegrees = StepTowards(mover.AngularVelocityDegrees, target, accel * dt);
        }
        else
        {
            mover.AngularVelocityDegrees = 0f;
        }

        if (steering == 0f && throttle != 0f && mover.DriftDegrees == 0f && mover.AlignmentAssistDegrees > 0f &&
            MathF.Abs(mover.CurrentSpeed) > MinSpeed &&
            MathF.Abs(mover.CurrentSpeed) <= mover.AlignmentAssistMaxSpeed)
        {
            var delta = Angle.ShortestDistance(rotation, rotation.GetCardinalDir().ToAngle()).Degrees;
            if (Math.Abs(delta) <= mover.AlignmentAssistDegrees)
                mover.AngularVelocityDegrees = (float) Math.Clamp(delta / dt, -30, 30);
        }

        var turned = 0f;
        if (MathF.Abs(mover.AngularVelocityDegrees) > 0.001f)
        {
            var desired = rotation + Angle.FromDegrees(mover.AngularVelocityDegrees * dt);
            if (CanOccupy(uid, mover, seats, grid, position, desired, true))
            {
                turned = mover.AngularVelocityDegrees * dt;
                rotation = desired;
                _transform.SetLocalRotationNoLerp(uid, rotation, xform);
            }
            else
            {
                mover.AngularVelocityDegrees = 0f;
            }
        }

        UpdateDrift(mover, steering, handbrake, turned, dt);

        var maxSpeed = GetMaxSpeed(uid, mover, false);
        var maxReverse = GetMaxSpeed(uid, mover, true);

        float targetSpeed;
        float rate;
        if (handbrake)
        {
            targetSpeed = 0f;
            rate = mover.HandbrakeDeceleration;
        }
        else if (throttle > 0f)
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

        if (!handbrake && MathF.Abs(mover.CurrentSpeed) > MathF.Abs(targetSpeed) && throttle != 0f && MathF.Sign(mover.CurrentSpeed) == MathF.Sign(targetSpeed))
            rate = mover.Deceleration;

        mover.CurrentSpeed = StepTowards(mover.CurrentSpeed, targetSpeed, rate * dt);

        if (mover.DriftDegrees != 0f)
        {
            var drag = mover.DriftDrag * MathF.Abs(MathF.Sin(MathHelper.DegreesToRadians(mover.DriftDegrees))) * dt;
            mover.CurrentSpeed = StepTowards(mover.CurrentSpeed, 0f, drag);
        }

        var travel = MathF.Abs(mover.CurrentSpeed) * dt;
        if (travel <= MinMove)
            return;

        var heading = rotation - Angle.FromDegrees(mover.DriftDegrees);
        var forward = heading.ToWorldVec() * MathF.Sign(mover.CurrentSpeed);
        var start = position;
        var steps = Math.Max(1, (int) MathF.Ceiling(travel / Math.Clamp(mover.ProbeStep, 0.02f, 0.5f)));
        var lastGood = start;
        var blocked = false;

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
            mover.DriftDegrees = 0f;
        }

        _physics.WakeBody(uid);
    }

    // -- Занос: при резком повороте на скорости или с ручником корпус поворачивается быстрее, чем меняется направление движения
    private static void UpdateDrift(GridVehicleMoverComponent mover, float steering, bool handbrake, float turned, float dt)
    {
        var minSpeed = handbrake ? mover.HandbrakeDriftMinSpeed : mover.DriftMinSpeed;
        var sliding = mover.DriftMinSpeed > 0f &&
                      steering != 0f &&
                      mover.CurrentSpeed >= minSpeed &&
                      (handbrake || MathF.Abs(mover.AngularVelocityDegrees) >= mover.MaxRotationSpeedDegrees * HardTurnFraction);

        if (!sliding && mover.DriftDegrees == 0f)
            return;

        var grip = !sliding ? mover.DriftRecoverDegrees : handbrake ? mover.HandbrakeGripDegrees : mover.DriftGripDegrees;
        var drift = StepTowards(mover.DriftDegrees + turned, 0f, grip * dt);
        mover.DriftDegrees = Math.Clamp(drift, -mover.DriftMaxDegrees, mover.DriftMaxDegrees);

        if (mover.CurrentSpeed <= MinSpeed)
            mover.DriftDegrees = 0f;
    }

    private float GetMaxSpeed(EntityUid uid, GridVehicleMoverComponent mover, bool reverse)
    {
        var speed = reverse ? mover.MaxReverseSpeed : mover.MaxSpeed;

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

    // -- Машина двигается через трансформ, физической скорости у неё и пассажиров нет: эффектам отдаём её вручную
    public Vector2 GetCarrierVelocity(EntityUid entity)
    {
        var parent = Transform(entity).ParentUid;
        if (!TryComp<GridVehicleMoverComponent>(parent, out var mover) || MathF.Abs(mover.CurrentSpeed) <= MinSpeed)
            return Vector2.Zero;

        var heading = _transform.GetWorldRotation(parent) - Angle.FromDegrees(mover.DriftDegrees);
        return heading.ToWorldVec() * mover.CurrentSpeed;
    }

    public void Stop(EntityUid uid, GridVehicleMoverComponent? mover = null)
    {
        if (!Resolve(uid, ref mover, false))
            return;

        mover.CurrentSpeed = 0f;
        mover.AngularVelocityDegrees = 0f;
        mover.DriftDegrees = 0f;
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
