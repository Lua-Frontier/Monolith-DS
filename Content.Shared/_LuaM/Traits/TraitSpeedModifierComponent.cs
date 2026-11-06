using Robust.Shared.GameStates;

namespace Content.Shared._LuaM.Traits;

/// <summary>
/// Permanent movement speed change from a trait. Networked so the client predicts the movement.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TraitSpeedModifierComponent : Component
{
    [DataField, AutoNetworkedField]
    public float WalkModifier = 1f;

    [DataField, AutoNetworkedField]
    public float SprintModifier = 1f;
}
