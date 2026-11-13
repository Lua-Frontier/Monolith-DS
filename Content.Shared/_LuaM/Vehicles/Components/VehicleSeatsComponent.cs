/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using System.Numerics;
using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._LuaM.Vehicles.Components;

// -- Порядок мест совпадает с порядком смещений в StrapSlots
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleSeatsComponent : Component
{
    [DataField(required: true)]
    public List<VehicleSeatRole> Seats = new();

    [DataField, AutoNetworkedField]
    public EntityUid? Driver;

    [DataField]
    public Vector2 Zoom = new(1.5f, 1.5f);

    [DataField, AutoNetworkedField]
    public EntityUid? Gunner;

    [DataField]
    public List<EntProtoId> DriverActions = new();

    [DataField]
    public List<EntProtoId> GunnerActions = new();
}

[Serializable, NetSerializable]
public enum VehicleSeatRole : byte
{
    Driver,
    Passenger,
    Gunner,
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VehicleOccupantComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Vehicle;

    [DataField, AutoNetworkedField]
    public VehicleSeatRole Role;

    [DataField, AutoNetworkedField]
    public int Seat;

    [DataField]
    public Vector2? PreviousMaxZoom;
}
