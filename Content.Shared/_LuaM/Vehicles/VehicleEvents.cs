/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using System.Numerics;
using Content.Shared._LuaM.Vehicles.Components;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._LuaM.Vehicles;

public sealed partial class VehicleHornActionEvent : InstantActionEvent;

public sealed partial class VehicleOverchargeActionEvent : InstantActionEvent;

public sealed partial class VehicleSpotlightActionEvent : InstantActionEvent;

public sealed partial class VehicleDeployActionEvent : InstantActionEvent;

public sealed partial class VehicleSwitchWeaponActionEvent : InstantActionEvent;

public sealed partial class VehicleSwitchShellActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class HardpointInstallDoAfterEvent : DoAfterEvent
{
    [DataField]
    public NetEntity Holder;

    [DataField]
    public string Slot = string.Empty;

    private HardpointInstallDoAfterEvent()
    {
    }

    public HardpointInstallDoAfterEvent(NetEntity holder, string slot)
    {
        Holder = holder;
        Slot = slot;
    }

    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class HardpointRemoveDoAfterEvent : DoAfterEvent
{
    [DataField]
    public NetEntity Holder;

    [DataField]
    public string Slot = string.Empty;

    private HardpointRemoveDoAfterEvent()
    {
    }

    public HardpointRemoveDoAfterEvent(NetEntity holder, string slot)
    {
        Holder = holder;
        Slot = slot;
    }

    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class VehicleAmmoLoadDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed class VehicleTurretAimEvent(NetEntity vehicle, Vector2 offset) : EntityEventArgs
{
    public readonly NetEntity Vehicle = vehicle;
    public readonly Vector2 Offset = offset;
}

[Serializable, NetSerializable]
public sealed class VehicleSpeedLimitEvent(NetEntity vehicle, float limit) : EntityEventArgs
{
    public readonly NetEntity Vehicle = vehicle;
    public readonly float Limit = limit;
}

[ByRefEvent]
public record struct VehicleCanMoveEvent(bool Cancelled = false);

[ByRefEvent]
public readonly record struct VehicleCrashEvent(float Speed, System.Numerics.Vector2 Direction);

[ByRefEvent]
public record struct VehicleEngineStartAttemptEvent(EntityUid Driver, bool Cancelled = false);

[ByRefEvent]
public record struct VehicleSeatsChangedEvent(EntityUid Occupant, VehicleSeatRole Role, bool Seated);

[ByRefEvent]
public readonly record struct HardpointsChangedEvent;
