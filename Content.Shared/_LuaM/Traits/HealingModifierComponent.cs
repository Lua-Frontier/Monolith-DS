namespace Content.Shared._LuaM.Traits;

/// <summary>
/// Changes how well the body heals from any source.
/// </summary>
[RegisterComponent]
public sealed partial class HealingModifierComponent : Component
{
    /// <summary>
    /// Multiplier of all healing the body gets.
    /// </summary>
    [DataField]
    public float Multiplier = 1f;

    /// <summary>
    /// Multiplier of the time repairs take, like welding.
    /// </summary>
    [DataField]
    public float RepairTimeMultiplier = 1f;
}
