using Robust.Shared.GameStates;

namespace Content.Shared._LuaM.Cyberware;

/// <summary>
/// Changes the movement speed of the body it is on. Meant for cyberware: put it in the organ's onAdd,
/// so it is added to the body when the organ is installed and removed with the organ.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class CyberwareSpeedModifierComponent : Component
{
    [DataField, AutoNetworkedField]
    public float WalkModifier = 1f;

    [DataField, AutoNetworkedField]
    public float SprintModifier = 1f;
}
