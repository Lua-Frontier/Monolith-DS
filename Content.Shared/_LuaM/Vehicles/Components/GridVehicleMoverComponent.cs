/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using Robust.Shared.GameStates;

namespace Content.Shared._LuaM.Vehicles.Components;

// -- Порт GridVehicleMover из RMC-14: машина двигается кинематически, проверяя корпус пробами по пути
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
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
    public float RamMinSpeed = 3f;

    [DataField]
    public float RamStructureDamage = 12f;

    [DataField]
    public float RamHullDamage = 2f;

    [DataField]
    public float MobRamDamage = 2.5f;

    [DataField]
    public float MobRamSpeedMultiplier = 0.9f;

    [DataField]
    public float MobCollisionHullDamage = 4f;

    [DataField]
    public float MobKnockdownTime = 1.5f;

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

    // -- Ручник на кнопке ходьбы: тормозит и срывает машину в занос даже на небольшой скорости
    [DataField, AutoNetworkedField]
    public float HandbrakeDeceleration = 5f;

    [DataField, AutoNetworkedField]
    public float HandbrakeGripDegrees = 25f;

    [DataField, AutoNetworkedField]
    public float HandbrakeTurnMultiplier = 1.4f;

    [DataField, AutoNetworkedField]
    public float HandbrakeDriftMinSpeed = 2.5f;

    [AutoNetworkedField]
    public bool Handbrake;

    [AutoNetworkedField]
    public float CurrentSpeed;

    // -- Угол заноса: насколько нос машины ушёл от направления движения
    [AutoNetworkedField]
    public float DriftDegrees;

    [AutoNetworkedField]
    public float AngularVelocityDegrees;

    [AutoNetworkedField]
    public bool IsMoving;

    [ViewVariables]
    public float Accumulator;
}
