// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.MartialArts;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.MartialArts.Components;

/// <summary>
/// Tracks the last attacks of the entity and performs combos. Combos are checked on the server only,
/// the client gets <see cref="LastAttacks"/> to show them near the cursor.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CanPerformComboComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? CurrentTarget;

    [DataField]
    public ProtoId<ComboPrototype>? BeingPerformed;

    [DataField]
    public int LastAttacksLimit = 4;

    [DataField, AutoNetworkedField]
    public List<ComboAttackType> LastAttacks = new();

    [DataField]
    public List<ProtoId<ComboPrototype>> AllowedCombos = new();

    /// <summary>
    /// Last attacks are forgotten when there were no new attacks until this time.
    /// </summary>
    [DataField]
    public TimeSpan ResetTime = TimeSpan.Zero;

    [DataField]
    public TimeSpan ResetDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public int ConsecutiveGnashes;
}
