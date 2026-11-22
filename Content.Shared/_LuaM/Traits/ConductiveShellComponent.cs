using Content.Shared.Damage;
using Robust.Shared.Audio;

namespace Content.Shared._LuaM.Traits;

/// <summary>
/// The silicon's unarmed hits carry an electric discharge paid for by its battery:
/// shock damage and stamina loss for the living, a defibrillator jolt for the dead.
/// </summary>
[RegisterComponent]
public sealed partial class ConductiveShellComponent : Component
{
    /// <summary>
    /// Share of the maximum battery charge spent on every hit that lands.
    /// </summary>
    [DataField]
    public float BatteryCost = 0.05f;

    [DataField]
    public DamageSpecifier Damage = new();

    [DataField]
    public float StaminaDamage = 20f;

    /// <summary>
    /// Same healing as a defibrillator zap.
    /// </summary>
    [DataField]
    public DamageSpecifier ReviveHeal = new();

    [DataField]
    public TimeSpan ReviveCooldown = TimeSpan.FromSeconds(5);

    [DataField]
    public SoundSpecifier ZapSound = new SoundPathSpecifier("/Audio/Items/Defib/defib_zap.ogg");

    [DataField]
    public SoundSpecifier ReviveSuccessSound = new SoundPathSpecifier("/Audio/Items/Defib/defib_safety_on.ogg");

    [DataField]
    public SoundSpecifier ReviveFailureSound = new SoundPathSpecifier("/Audio/Items/Defib/defib_failed.ogg");

    public TimeSpan NextRevive;
}
