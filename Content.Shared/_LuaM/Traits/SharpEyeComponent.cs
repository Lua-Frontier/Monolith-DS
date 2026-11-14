using Robust.Shared.GameStates;

namespace Content.Shared._LuaM.Traits;

/// <summary>
/// Sharp eye: the vision focus looks further ahead than usual.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SharpEyeComponent : Component
{
    /// <summary>
    /// How much further the vision focus reaches.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float FocusRangeMultiplier = 1.5f;
}
