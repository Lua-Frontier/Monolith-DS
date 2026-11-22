namespace Content.Shared._LuaM.Traits;

/// <summary>
/// The silicon's battery drains faster while its body is cold, instead of slower.
/// </summary>
[RegisterComponent]
public sealed partial class SiliconColdDrainComponent : Component
{
    /// <summary>
    /// Body temperature in kelvin below which the battery drains faster.
    /// </summary>
    [DataField]
    public float Threshold = 273.15f;

    [DataField]
    public float Multiplier = 2f;
}
