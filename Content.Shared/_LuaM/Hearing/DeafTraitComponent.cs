namespace Content.Shared._LuaM.Hearing;

/// <summary>
/// Born deaf: the creature is deaf unless it wears a switched on <see cref="HearingAidComponent"/> of the right kind.
/// </summary>
[RegisterComponent]
public sealed partial class DeafTraitComponent : Component
{
    /// <summary>
    /// How much of the normal volume other sounds keep without a hearing aid.
    /// </summary>
    [DataField]
    public float SoundVolume = 0.1f;

    /// <summary>
    /// The <see cref="HearingAidComponent.Kind"/> that helps: a hearing aid for the living, a hearing sensor for synthetics.
    /// </summary>
    [DataField]
    public string AidKind = "HearingAid";
}
