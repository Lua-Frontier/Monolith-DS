using Robust.Shared.GameStates;

namespace Content.Shared._LuaM.Traits;

/// <summary>
/// Changes how much damage the limbs of this body take before they can be torn off.
/// Sits on the body, so replaced limbs are covered as well.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SeverIntegrityModifierComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Multiplier = 1f;
}
