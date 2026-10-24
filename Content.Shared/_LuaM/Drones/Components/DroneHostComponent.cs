using Content.Shared._LuaM.Drones.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.Drones.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DroneHostComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<FpvIconPrototype> Icon { get; set; } = "FPVIcon";

    [DataField, AutoNetworkedField]
    public EntityUid? Drone;

    [DataField, AutoNetworkedField]
    public EntityUid? Controller;
}
