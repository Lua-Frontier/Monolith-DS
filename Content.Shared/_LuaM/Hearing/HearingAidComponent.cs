namespace Content.Shared._LuaM.Hearing;

/// <summary>
/// While switched on and worn around the neck, lets a <see cref="DeafTraitComponent"/> owner of the matching kind hear.
/// It does nothing against any other deafness, such as a running berserk heart.
/// </summary>
[RegisterComponent]
public sealed partial class HearingAidComponent : Component
{
    /// <summary>
    /// Which deafness this device helps with, see <see cref="DeafTraitComponent.AidKind"/>.
    /// </summary>
    [DataField]
    public string Kind = "HearingAid";
}
