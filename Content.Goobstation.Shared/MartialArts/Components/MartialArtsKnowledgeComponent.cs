// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Goobstation.Shared.MartialArts.Components;

/// <summary>
/// The entity knows a martial art.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MartialArtsKnowledgeComponent : Component
{
    [DataField, AutoNetworkedField]
    public MartialArtsForms MartialArtsForm = MartialArtsForms.CloseQuartersCombat;

    /// <summary>
    /// Unarmed damage before the art was learned, restored when it is lost.
    /// </summary>
    [DataField]
    public DamageSpecifier? OriginalFistDamage;
}

[Serializable, NetSerializable]
public enum MartialArtsForms : byte
{
    CorporateJudo,
    CloseQuartersCombat,
    SleepingCarp,
    Capoeira,
    KungFuDragon,
    Ninjutsu,
}
