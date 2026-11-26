// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Goobstation.Shared.Grab;

/// <summary>
/// A mob thrown from a grab or by a martial arts move, it hits everyone it flies into.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class GrabThrownComponent : Component
{
    /// <summary>
    /// Damage the thrown one takes when the throw ends.
    /// </summary>
    [ViewVariables]
    public DamageSpecifier? DamageOnLand;

    /// <summary>
    /// Entities already hit, they are not hit twice.
    /// </summary>
    [ViewVariables]
    public HashSet<EntityUid> IgnoreEntity = new();
}
