using Robust.Shared.GameStates;

namespace Content.Shared._LuaM.Vehicles.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class VehicleCrashEjectComponent : Component
{
    [DataField]
    public float MinSpeed = 6f;

    [DataField]
    public float BluntDamage = 6f;

    [DataField]
    public float KnockdownTime = 2f;

    [DataField]
    public float ThrowDistance = 2.5f;

    [DataField]
    public float ThrowSpeed = 8f;
}
