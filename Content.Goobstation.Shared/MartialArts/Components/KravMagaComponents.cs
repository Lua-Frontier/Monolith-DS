// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.MartialArts.Components;

/// <summary>
/// A Krav Maga move action.
/// </summary>
[RegisterComponent]
public sealed partial class KravMagaActionComponent : Component
{
    [DataField(required: true)]
    public KravMagaMoves Configuration;

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public float StaminaDamage;

    [DataField]
    public TimeSpan EffectTime = TimeSpan.Zero;
}

/// <summary>
/// The entity knows Krav Maga, usually granted by gloves.
/// </summary>
[RegisterComponent]
public sealed partial class KravMagaComponent : Component
{
    [DataField]
    public List<EntProtoId> BaseKravMagaMoves = new()
    {
        "ActionLegSweep",
        "ActionNeckChop",
        "ActionLungPunch",
    };

    [ViewVariables]
    public List<EntityUid> KravMagaMoveEntities = new();

    [ViewVariables]
    public KravMagaMoves? SelectedMove;

    [ViewVariables]
    public EntityUid? SelectedMoveAction;

    /// <summary>
    /// Bonus blunt damage of a hit without a selected move.
    /// </summary>
    [DataField]
    public float BaseDamage = 5;

    [DataField]
    public float DownedDamageModifier = 2;

    [DataField]
    public TimeSpan LegSweepKnockdownTime = TimeSpan.FromSeconds(4);
}

public enum KravMagaMoves : byte
{
    LegSweep,
    NeckChop,
    LungPunch,
}
