// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.MartialArts.Events;

/// <summary>
/// Selects a Krav Maga move for the next unarmed hit.
/// </summary>
public sealed partial class KravMagaActionEvent : InstantActionEvent;

/// <summary>
/// Raised on the performer when a combo is done. Inheritors are used as the combo prototype event.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class BaseComboPerformedEvent : EntityEventArgs
{
    [DataField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Weapons/genhit3.ogg");
}

#region CQC

public sealed partial class CqcSlamPerformedEvent : BaseComboPerformedEvent;

public sealed partial class CqcKickPerformedEvent : BaseComboPerformedEvent
{
    [DataField]
    public TimeSpan ForcedSleepTime = TimeSpan.FromSeconds(10);
}

public sealed partial class CqcRestrainPerformedEvent : BaseComboPerformedEvent;

public sealed partial class CqcPressurePerformedEvent : BaseComboPerformedEvent;

public sealed partial class CqcConsecutivePerformedEvent : BaseComboPerformedEvent;

#endregion

#region Corporate Judo

public sealed partial class JudoDiscombobulatePerformedEvent : BaseComboPerformedEvent
{
    [DataField]
    public TimeSpan SlowdownTime = TimeSpan.FromSeconds(5);

    [DataField]
    public float SpeedMultiplier = 0.5f;
}

public sealed partial class JudoEyePokePerformedEvent : BaseComboPerformedEvent
{
    [DataField]
    public TimeSpan BlindnessTime = TimeSpan.FromSeconds(2);
}

public sealed partial class JudoThrowPerformedEvent : BaseComboPerformedEvent;

public sealed partial class JudoArmbarPerformedEvent : BaseComboPerformedEvent;

public sealed partial class JudoWheelThrowPerformedEvent : BaseComboPerformedEvent
{
    [DataField]
    public float ThrowDistance = 5f;
}

#endregion

#region Sleeping Carp

public sealed partial class SleepingCarpGnashingTeethPerformedEvent : BaseComboPerformedEvent
{

    /// <summary>
    /// Extra damage for every gnash in a row.
    /// </summary>
    [DataField]
    public float ConsecutiveDamage = 5f;
}

public sealed partial class SleepingCarpKneeHaulPerformedEvent : BaseComboPerformedEvent;

public sealed partial class SleepingCarpCrashingWavesPerformedEvent : BaseComboPerformedEvent;

#endregion

#region Capoeira

public abstract partial class BaseCapoeiraEvent : BaseComboPerformedEvent
{
    [DataField]
    public float VelocityPowerMultiplier = 0.6f;

    [DataField]
    public float MinPower = 1f;

    [DataField]
    public float MaxPower = 4f;

    [DataField]
    public float MinVelocity;

    [DataField]
    public float AttackSpeedMultiplier = 1f;

    [DataField]
    public TimeSpan AttackSpeedMultiplierTime = TimeSpan.Zero;

    /// <summary>
    /// Stamina given to the performer, negative values restore stamina.
    /// </summary>
    [DataField]
    public float StaminaToHeal = -20f;
}

public sealed partial class PushKickPerformedEvent : BaseCapoeiraEvent
{
    [DataField]
    public DamageModifierSet ModifierSet = new();

    [DataField]
    public float ThrowRange = 1f;
}

public sealed partial class SweepKickPerformedEvent : BaseCapoeiraEvent;

public sealed partial class CircleKickPerformedEvent : BaseCapoeiraEvent
{
    [DataField]
    public TimeSpan SlowDownTime = TimeSpan.FromSeconds(2);
}

public sealed partial class SpinKickPerformedEvent : BaseCapoeiraEvent
{
    [DataField]
    public ProtoId<EmotePrototype>? Emote = "Spin";
}

public sealed partial class KickUpPerformedEvent : BaseCapoeiraEvent;

#endregion

#region Dragon Kung Fu

public sealed partial class DragonClawPerformedEvent : BaseComboPerformedEvent
{
    [DataField]
    public TimeSpan SlowdownTime = TimeSpan.FromSeconds(2);

    [DataField]
    public float SpeedModifier = 0.6f;
}

public sealed partial class DragonTailPerformedEvent : BaseComboPerformedEvent
{
    [DataField]
    public TimeSpan DownedStunTime = TimeSpan.FromSeconds(1);
}

public sealed partial class DragonStrikePerformedEvent : BaseComboPerformedEvent;

#endregion

#region Ninjutsu

public sealed partial class BiteTheDustPerformedEvent : BaseComboPerformedEvent;

public sealed partial class DirtyKillPerformedEvent : BaseComboPerformedEvent;

#endregion
