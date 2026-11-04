namespace Content.Shared._LuaM.Cyberware;

/// <summary>
/// Shows the cyberpsychosis glitch shader to the player attached to this entity.
/// It sits on the viewer the player watches through while their body is out of control.
/// </summary>
[RegisterComponent]
public sealed partial class CyberpsychosisGlitchComponent : Component
{
    /// <summary>
    /// Strength of the glitch, 0 to 1.
    /// </summary>
    [DataField]
    public float Intensity = 1f;
}
