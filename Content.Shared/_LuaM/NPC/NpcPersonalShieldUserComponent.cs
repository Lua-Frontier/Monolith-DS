namespace Content.Shared._LuaM.NPC;

/// <summary>
/// An NPC that turns on the personal energy shield of its hardsuit (like the M-92X or the CV-53) by itself
/// and turns it back on after it comes back online.
/// </summary>
[RegisterComponent]
public sealed partial class NpcPersonalShieldUserComponent : Component
{
    /// <summary>
    /// Inventory slot the shielded suit is worn in.
    /// </summary>
    [DataField]
    public string Slot = "outerClothing";

    [DataField]
    public TimeSpan CheckInterval = TimeSpan.FromSeconds(1);

    [ViewVariables]
    public TimeSpan NextCheck;
}
