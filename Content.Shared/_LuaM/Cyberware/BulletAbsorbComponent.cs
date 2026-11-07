using Robust.Shared.Audio;

namespace Content.Shared._LuaM.Cyberware;

/// <summary>
/// Fully absorbs the damage of one bullet, then needs <see cref="Cooldown"/> to be ready again.
/// Put on the body through an armor organ's onAdd.
/// </summary>
[RegisterComponent]
public sealed partial class BulletAbsorbComponent : Component
{
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(20);

    /// <summary>
    /// When the next bullet can be absorbed.
    /// </summary>
    [ViewVariables]
    public TimeSpan NextAbsorb;

    [DataField]
    public SoundSpecifier? AbsorbSound = new SoundPathSpecifier("/Audio/Weapons/Guns/Hits/ric1.ogg");

    [DataField]
    public LocId AbsorbPopup = "bullet-absorb-popup";

    /// <summary>
    /// Shown to the owner when the armor is ready again.
    /// </summary>
    [DataField]
    public LocId ReadyPopup = "bullet-absorb-ready";

    [ViewVariables]
    public bool WaitingForReady;
}
