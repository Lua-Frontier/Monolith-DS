// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.Weapons;

/// <summary>
/// Prevents the holder from attacking with another held weapon.
/// </summary>
[RegisterComponent]
public sealed partial class ExclusiveHandUseComponent : Component
{
    [DataField]
    public LocId Popup = "exclusive-hand-use-blocked";

    [DataField]
    public HashSet<EntProtoId> BlockedItems = [];

    /// <summary>
    /// When non-empty, only these item prototypes may be used as ranged or melee weapons.
    /// </summary>
    [DataField]
    public HashSet<EntProtoId> AllowedItems = [];
}
