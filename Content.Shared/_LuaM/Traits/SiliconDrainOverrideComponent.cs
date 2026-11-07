namespace Content.Shared._LuaM.Traits;

/// <summary>
/// Sets how much battery charge the silicon uses every second.
/// </summary>
[RegisterComponent]
public sealed partial class SiliconDrainOverrideComponent : Component
{
    [DataField(required: true)]
    public float DrainPerSecond;
}
