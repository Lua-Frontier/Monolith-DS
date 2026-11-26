// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Alert;
using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.MartialArts.Components;

/// <summary>
/// Dragon Kung Fu: standing still gives <see cref="DragonPowerBuffComponent"/>.
/// </summary>
[RegisterComponent]
public sealed partial class DragonKungFuTimerComponent : Component
{
    [ViewVariables]
    public TimeSpan LastMoveTime = TimeSpan.Zero;

    [DataField]
    public float MinVelocitySquared = 0.25f;

    [DataField]
    public TimeSpan PauseDuration = TimeSpan.FromSeconds(1f);

    [DataField]
    public TimeSpan BuffLength = TimeSpan.FromSeconds(5f);
}

/// <summary>
/// Status effect: reduces unarmed melee damage taken and empowers the next attacks after being hit.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class DragonPowerBuffComponent : Component
{
    [DataField]
    public DamageModifierSet ModifierSet = new()
    {
        Coefficients =
        {
            { "Blunt", 0.6f },
            { "Slash", 0.6f },
            { "Piercing", 0.6f },
            { "Heat", 0.6f },
        },
        IgnoreArmorPierceFlags = (int) PartialArmorPierceFlags.All,
    };

    [DataField]
    public float DamageMultiplier = 1.2f;

    [DataField]
    public TimeSpan AttackDamageBuffDuration = TimeSpan.FromSeconds(5);
}

/// <summary>
/// Ninjutsu: attacks while the enemy does not expect it deal more damage.
/// </summary>
[RegisterComponent]
public sealed partial class NinjutsuSneakAttackComponent : Component
{
    [DataField]
    public float Multiplier = 2f;

    [DataField]
    public float AssassinateModifier = 180f;

    [DataField]
    public float AssassinateUnarmedModifier = 115f;

    /// <summary>
    /// Negative value makes armor work better against the assassination.
    /// </summary>
    [DataField]
    public float AssassinateArmorPierce = -2.5f;

    [DataField]
    public TimeSpan TakedownSlowdownTime = TimeSpan.FromSeconds(5);

    [DataField]
    public TimeSpan TakedownMuteTime = TimeSpan.FromSeconds(10);

    [DataField]
    public float TakedownSpeedModifier = 0.2f;

    [DataField]
    public float TakedownBackstabMultiplier = 1.5f;

    [DataField]
    public TimeSpan LossOfSurpriseTime = TimeSpan.FromSeconds(5);

    [DataField]
    public SoundSpecifier AssassinateSoundUnarmed = new SoundPathSpecifier("/Audio/Weapons/genhit1.ogg");

    [DataField]
    public SoundSpecifier AssassinateSoundArmed = new SoundPathSpecifier("/Audio/_Goobstation/Weapons/Effects/guillotine.ogg");

    [DataField]
    public LocId AssassinateComboName = "martial-arts-combo-assassinate";

    [DataField]
    public LocId TakedownComboName = "martial-arts-combo-ninjutsu-takedown";

    [DataField]
    public ProtoId<AlertPrototype> Alert = "SneakAttack";
}

/// <summary>
/// Status effect: Ninjutsu sneak attacks are not possible.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NinjutsuLossOfSurpriseComponent : Component;

/// <summary>
/// Status effect: the entity takes more melee damage.
/// </summary>
[RegisterComponent]
public sealed partial class MartialArtsVulnerableComponent : Component
{
    [DataField]
    public List<DamageModifierSet> ModifierSets = new();
}

/// <summary>
/// The entity is held in a Corporate Judo armbar by <see cref="Puller"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ArmbarredComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public EntityUid Puller;
}
