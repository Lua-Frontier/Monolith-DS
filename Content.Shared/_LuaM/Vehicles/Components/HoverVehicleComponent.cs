using Robust.Shared.GameStates;

namespace Content.Shared._LuaM.Vehicles.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class HoverVehicleComponent : Component
{
    [DataField]
    public float Thrust = 4f;

    [DataField]
    public float MaxSpeed = 12f;

    [DataField]
    public float BrakeRate = 2f;

    [DataField]
    public float RotationSpeedDegrees = 120f;
}
