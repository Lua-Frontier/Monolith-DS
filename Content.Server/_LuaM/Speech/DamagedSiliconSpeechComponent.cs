namespace Content.Server._LuaM.Speech;

/// <summary>
/// Breaks the speech of a damaged synthetic: the closer its damage is to the critical threshold
/// (or the death threshold if it has no critical state), the more letters stutter, double or turn into glitch symbols.
/// </summary>
[RegisterComponent, Access(typeof(DamagedSiliconSpeechSystem))]
public sealed partial class DamagedSiliconSpeechComponent : Component
{
    /// <summary>
    /// Fraction of the incapacitation threshold at which speech starts breaking.
    /// </summary>
    [DataField]
    public float StartFraction = 0.4f;

    /// <summary>
    /// Chance for each letter to break right at the incapacitation threshold.
    /// </summary>
    [DataField]
    public float MaxCorruptionChance = 0.5f;
}
