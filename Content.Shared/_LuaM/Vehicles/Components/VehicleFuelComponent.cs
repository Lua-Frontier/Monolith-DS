using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.Vehicles.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class VehicleFuelComponent : Component
{
    [DataField]
    public string Solution = "fuel";

    [DataField]
    public ProtoId<ReagentPrototype> Reagent = "WeldingFuel";

    [DataField]
    public float ConsumptionPerMinute = 4f;

    [DataField]
    public float IdleFraction = 0.3f;

    [DataField]
    public float UpdateInterval = 3f;

    [ViewVariables]
    public float Accumulator;
}
