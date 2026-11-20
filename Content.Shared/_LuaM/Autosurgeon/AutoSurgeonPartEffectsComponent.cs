using Robust.Shared.GameStates;

namespace Content.Shared._LuaM.Autosurgeon;

/// <summary>
/// Components an autosurgeon put on a body part for its body, by component name. They are implants: the body keeps
/// them while the part is disabled by wounds and loses them only when the part comes off.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AutoSurgeonPartEffectsComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<string> Components = new();
}
