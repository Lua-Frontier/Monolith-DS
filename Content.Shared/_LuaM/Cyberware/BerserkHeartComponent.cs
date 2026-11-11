using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.Cyberware;

/// <summary>
/// Berserk heart on the body. While it is on the user is much harder to put down, hits harder in melee and
/// heals from every hit, but bleeds health every second, is deaf and can not use guns.
/// After a while the user stops telling friend from foe. It stays on until the user turns it off.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class BerserkHeartComponent : Component
{
    [DataField]
    public EntProtoId ActionProto = "ActionToggleBerserkHeart";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionUid;

    /// <summary>
    /// Damage needed to go critical while the heart is on. Never lowers what the body already has.
    /// </summary>
    [DataField]
    public FixedPoint2 CritThreshold = 150;

    /// <summary>
    /// Damage needed to die while the heart is on. Never lowers what the body already has.
    /// </summary>
    [DataField]
    public FixedPoint2 DeadThreshold = 250;

    [DataField]
    public float MeleeDamageMultiplier = 1.2f;

    /// <summary>
    /// Damage the user takes every <see cref="DamageInterval"/> while the heart is on.
    /// </summary>
    [DataField(required: true)]
    public DamageSpecifier SelfDamage = new();

    [DataField]
    public TimeSpan DamageInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Healing for every melee attack that hits a living creature, negative values.
    /// </summary>
    [DataField(required: true)]
    public DamageSpecifier HealOnHit = new();

    /// <summary>
    /// How long the heart runs before the user stops telling friend from foe.
    /// </summary>
    [DataField]
    public TimeSpan FrenzyAfter = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How much of the normal volume sounds keep while the heart is on. Speech and radio are not heard at all.
    /// </summary>
    [DataField]
    public float MuffledSoundVolume = 0.15f;

    /// <summary>
    /// How long the heart can not be turned on again after it turns off, for any reason.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(2);

    [DataField]
    public SoundSpecifier? ActivateSound;

    [DataField]
    public SoundSpecifier? DeactivateSound;

    [DataField, AutoNetworkedField]
    public bool Active;

    /// <summary>
    /// The user sees everyone as black shadows and can not tell names or job icons.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Frenzy;

    [ViewVariables]
    public TimeSpan ActivatedAt;

    [ViewVariables]
    public TimeSpan NextDamage;

    /// <summary>
    /// When the heart last turned off and when it can be turned on again. Null once the cooldown is over.
    /// Kept here because the actions system resets the action cooldown right after the toggle is handled.
    /// </summary>
    [ViewVariables]
    public (TimeSpan Start, TimeSpan End)? CooldownTime;

    /// <summary>
    /// Thresholds the body had before the heart turned on, put back when it turns off.
    /// </summary>
    [ViewVariables]
    public FixedPoint2? OldCritThreshold;

    [ViewVariables]
    public FixedPoint2? OldDeadThreshold;

    /// <summary>
    /// Whether the heart made the user deaf, so it does not cure a deafness it did not cause.
    /// </summary>
    [ViewVariables]
    public bool AddedDeaf;

    [ViewVariables]
    public TimeSpan NextGunPopup;
}

public sealed partial class ToggleBerserkHeartEvent : InstantActionEvent;
