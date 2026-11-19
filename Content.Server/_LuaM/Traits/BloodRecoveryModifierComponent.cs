namespace Content.Server._LuaM.Traits;

/// <summary>
/// Changes how this body copes with bleeding and blood loss.
/// </summary>
[RegisterComponent]
public sealed partial class BloodRecoveryModifierComponent : Component
{
    /// <summary>
    /// Multiplies how fast bleeding stops on its own.
    /// </summary>
    [DataField]
    public float BleedReductionMultiplier = 1f;

    /// <summary>
    /// Multiplies the damage taken from low blood level.
    /// </summary>
    [DataField]
    public float BloodlossDamageMultiplier = 1f;
}
