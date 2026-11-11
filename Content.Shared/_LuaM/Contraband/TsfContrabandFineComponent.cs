namespace Content.Shared._LuaM.Contraband;

/// <summary>
/// On a contraband turn-in console: whatever is turned in here fines the TSF budget
/// by the item's TSF tax (the negative nfsd one in ItemTax), times the multiplier.
/// </summary>
[RegisterComponent]
public sealed partial class TsfContrabandFineComponent : Component
{
    [DataField]
    public float Multiplier = 3f;
}
