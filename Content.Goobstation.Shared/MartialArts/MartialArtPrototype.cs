// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.MartialArts;
using Content.Goobstation.Shared.MartialArts.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.MartialArts;

[Prototype("martialArt")]
public sealed partial class MartialArtPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public MartialArtsForms MartialArtsForm = MartialArtsForms.CloseQuartersCombat;

    /// <summary>
    /// Random bonus damage of <see cref="DamageModifierType"/> added to every unarmed hit.
    /// </summary>
    [DataField]
    public bool RandomDamageModifier;

    [DataField]
    public int MinRandomDamageModifier;

    [DataField]
    public int MaxRandomDamageModifier = 5;

    /// <summary>
    /// Unarmed damage of <see cref="DamageModifierType"/> added permanently when the art is learned.
    /// </summary>
    [DataField]
    public FixedPoint2 BaseDamageModifier;

    [DataField]
    public ProtoId<DamageTypePrototype> DamageModifierType = "Blunt";

    [DataField(required: true)]
    public ProtoId<ComboListPrototype> RoundstartCombos;

    [DataField]
    public List<LocId> RandomSayings = new();

    [DataField]
    public List<LocId> RandomSayingsDowned = new();
}

[Prototype("combo")]
public sealed partial class ComboPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public MartialArtsForms MartialArtsForm;

    [DataField("attacks", required: true)]
    public List<ComboAttackType> AttackTypes = new();

    /// <summary>
    /// Raised on the performer when the combo is done.
    /// </summary>
    [DataField("event", required: true)]
    public object? ResultEvent;

    /// <summary>
    /// Extra damage of <see cref="DamageType"/> the move does.
    /// </summary>
    [DataField]
    public float ExtraDamage;

    [DataField]
    public TimeSpan ParalyzeTime = TimeSpan.Zero;

    /// <summary>
    /// Can a lying person perform this combo.
    /// </summary>
    [DataField]
    public bool CanDoWhileProne = true;

    [DataField]
    public float StaminaDamage;

    [DataField]
    public ProtoId<DamageTypePrototype> DamageType = "Blunt";

    /// <summary>
    /// How fast the target is thrown by the move.
    /// </summary>
    [DataField]
    public float ThrownSpeed = 7f;

    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// Is this combo performed on self only or only on other targets.
    /// </summary>
    [DataField]
    public bool PerformOnSelf;
}

[Prototype("comboList")]
public sealed partial class ComboListPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Combos are checked in this order, the first one that matches is performed.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<ComboPrototype>> Combos = new();
}
