/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._LuaM.Vehicles.Components;

// -- Порт GridVehicleMover из RMC-14: машина двигается кинематически, проверяя корпус пробами по пути
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class GridVehicleMoverComponent : Component
{
    [DataField, AutoNetworkedField]
    public float MaxSpeed = 6f;

    [DataField, AutoNetworkedField]
    public float MaxReverseSpeed = 3f;

    [DataField, AutoNetworkedField]
    public float Acceleration = 5f;

    [DataField, AutoNetworkedField]
    public float ReverseAcceleration = 4f;

    [DataField, AutoNetworkedField]
    public float Deceleration = 12f;

    [DataField, AutoNetworkedField]
    public float MaxRotationSpeedDegrees = 120f;

    [DataField, AutoNetworkedField]
    public float RotationAccelerationDegrees = 240f;

    [DataField, AutoNetworkedField]
    public float RotationDecelerationDegrees = 300f;

    [DataField, AutoNetworkedField]
    public float AlignmentAssistDegrees = 8f;

    [DataField, AutoNetworkedField]
    public float AlignmentAssistMaxSpeed = 1.5f;

    [DataField]
    public float ProbeStep = 0.1f;

    [DataField]
    public bool CanSmashWalls;

    [DataField]
    public float WallSmashMinSpeed = 7f;

    [DataField]
    public float WallSmashDamageMultiplier = 4f;

    [DataField]
    public float WallSmashHullDamage = 18f;

    [DataField]
    public float WallSmashSlowdownMultiplier = 0.75f;

    [DataField]
    public float WallSmashSlowdownDuration = 0.25f;

    [DataField]
    public float MobCollisionDamage = 8f;

    [DataField]
    public float MobCollisionHullDamage = 8f;

    [DataField]
    public float MobKnockdownTime = 1.5f;

    [DataField]
    public float MobMinImpactSpeed = 1f;

    [DataField]
    public float CrashImmobileDuration = 3f;

    [DataField]
    public float CrashImmobileMinSpeed = 8f;

    [DataField]
    public float MobCrashImmobileDuration = 4f;

    [DataField]
    public float MobCrashImmobileMinSpeed = 5f;

    [DataField]
    public float FullSpeedIntegrityFraction = 0.7f;

    [DataField]
    public float SpeedAtZeroIntegrity = 0.6f;

    [DataField, AutoNetworkedField]
    public float DriftMinSpeed;

    [DataField, AutoNetworkedField]
    public float DriftGripDegrees = 70f;

    [DataField, AutoNetworkedField]
    public float DriftRecoverDegrees = 240f;

    [DataField, AutoNetworkedField]
    public float DriftMaxDegrees = 50f;

    [DataField, AutoNetworkedField]
    public float DriftDrag = 10f;

    [AutoNetworkedField]
    public float CurrentSpeed;

    // -- Угол заноса: насколько нос машины ушёл от направления движения
    [AutoNetworkedField]
    public float DriftDegrees;

    [AutoNetworkedField]
    public float AngularVelocityDegrees;

    [AutoNetworkedField]
    public bool IsMoving;

    [AutoNetworkedField]
    public float SmashSlowdownMultiplier = 1f;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan SmashSlowdownUntil;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan ImmobileUntil;

    [ViewVariables]
    public float Accumulator;

    [ViewVariables]
    public TimeSpan NextImmobilePopup;
}
