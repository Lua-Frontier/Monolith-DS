/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._LuaM.Vehicles.Components;

// -- Порт GridVehicleMover из RMC-14: машина двигается кинематически, проверяя корпус пробами по пути
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GridVehicleMoverComponent : Component
{
    [DataField]
    public float MaxSpeed = 6f;

    [DataField]
    public float MaxReverseSpeed = 3f;

    [DataField]
    public float Acceleration = 5f;

    [DataField]
    public float ReverseAcceleration = 4f;

    [DataField]
    public float Deceleration = 12f;

    [DataField]
    public float MaxRotationSpeedDegrees = 120f;

    [DataField]
    public float RotationAccelerationDegrees = 240f;

    [DataField]
    public float RotationDecelerationDegrees = 300f;

    // -- Гусеничная техника разворачивается на месте с этой скоростью; 0 — только на ходу, как колёсная
    [DataField]
    public float PivotRotationSpeedDegrees;

    [DataField]
    public float AlignmentAssistDegrees = 8f;

    [DataField]
    public float AlignmentAssistMaxSpeed = 1.5f;

    [DataField]
    public float ProbeStep = 0.2f;

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
    public float MobThrowDistancePerSpeed = 0.3f;

    [DataField]
    public float MobThrowMinDistance = 1.5f;

    [DataField]
    public float MobThrowMaxDistance = 4f;

    // -- Тяжёлая машина сносит деревья и валуны на малой скорости и почти не теряет ход
    [DataField]
    public float CrushMinSpeedScale = 1f;

    [DataField]
    public float? CrushSpeedRetention;

    [DataField]
    public float CrushHullDamageScale = 1f;

    [DataField]
    public float MobThrowMinSpeed = 6f;

    [DataField]
    public float MobThrowSideRatio = 0.6f;

    [DataField]
    public float FullSpeedIntegrityFraction = 0.7f;

    [DataField]
    public float SpeedAtZeroIntegrity = 0.6f;

    [DataField]
    public float DriftMinSpeed;

    [DataField]
    public float DriftGripDegrees = 70f;

    [DataField]
    public float DriftRecoverDegrees = 240f;

    [DataField]
    public float DriftMaxDegrees = 50f;

    [DataField]
    public float DriftDrag = 10f;

    // -- Ручник на кнопке ходьбы: тормозит и срывает машину в занос даже на небольшой скорости
    [DataField]
    public float HandbrakeDeceleration = 5f;

    [DataField]
    public float HandbrakeGripDegrees = 25f;

    [DataField]
    public float HandbrakeTurnMultiplier = 1.4f;

    [DataField]
    public float HandbrakeDriftMinSpeed = 2.5f;

    [AutoNetworkedField]
    public bool Handbrake;

    // -- Ограничитель скорости водителя, доля от MaxSpeed
    [DataField, AutoNetworkedField]
    public float SpeedLimit = 1f;

    [DataField]
    public float MinSpeedLimit = 0.1f;

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
    public bool Floating;

    [ViewVariables]
    public TimeSpan NextFloatCheck;

    // -- Поза, в которой уже проверяли, не залез ли корпус в стену: пока машина в ней стоит, проверка не повторяется
    [ViewVariables]
    public Vector2 UnstickCheckedPosition;

    [ViewVariables]
    public Angle UnstickCheckedRotation;

    [ViewVariables]
    public TimeSpan UnstickRetryAt;

    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> HitCooldowns = new();

    // -- Класс массы решает, что машина сносит на ходу и кого сдвигает при ударе
    [DataField]
    public VehicleWeightClass WeightClass = VehicleWeightClass.Light;

    // -- На малой скорости машина толкает машину своего класса и легче, а не упирается в неё
    [DataField]
    public bool CanPushVehicles = true;

    [DataField]
    public float PushMaxSpeed = 1.5f;

    // -- Отлёт и закрут после удара о машину, в осях грида; гаснут сами
    [AutoNetworkedField]
    public Vector2 ImpactVelocity;

    [AutoNetworkedField]
    public float ImpactSpinDegrees;

    [ViewVariables]
    public float SmashSlowMultiplier = 1f;

    [ViewVariables]
    public TimeSpan SmashSlowUntil;
}

[Serializable, NetSerializable]
public enum VehicleWeightClass : byte
{
    Weak,
    Light,
    Medium,
    Heavy,
}
