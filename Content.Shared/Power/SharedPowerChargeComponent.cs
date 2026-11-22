namespace Content.Shared.Power;

/// <summary>
/// Component for a powered machine that slowly powers on and off over a period of time.
/// </summary>
public abstract partial class SharedPowerChargeComponent : Component
{
    /// <summary>
    /// The title used for the default charged machine window if used
    /// </summary>
    [DataField]
    public LocId WindowTitle { get; set; } = string.Empty;

    // Lua start
    /// <summary>
    /// Show an action button in the UI that spends the charge.
    /// </summary>
    [DataField]
    public bool ActionUI { get; set; }
    // Lua end

}
