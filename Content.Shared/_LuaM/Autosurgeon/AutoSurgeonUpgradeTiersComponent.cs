namespace Content.Shared._LuaM.Autosurgeon;

/// <summary>
/// Remembers the tier of each autosurgeon upgrade on a body part, by component name,
/// so a weaker version can't replace a stronger one.
/// </summary>
[RegisterComponent]
public sealed partial class AutoSurgeonUpgradeTiersComponent : Component
{
    [ViewVariables]
    public Dictionary<string, int> Tiers = new();
}
