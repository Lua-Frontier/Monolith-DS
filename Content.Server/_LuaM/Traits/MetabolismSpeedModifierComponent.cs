namespace Content.Server._LuaM.Traits;

/// <summary>
/// Makes the organs of this body process the reagents in its blood faster.
/// </summary>
[RegisterComponent]
public sealed partial class MetabolismSpeedModifierComponent : Component
{
    [DataField]
    public float RateMultiplier = 1f;
}
