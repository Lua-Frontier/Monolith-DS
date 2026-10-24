// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.NPC.Prototypes;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.Traps;

/// <summary>
/// Defines entities that do not activate the trap.
/// The whitelist supports entity tags and components; factions are checked separately.
/// </summary>
[RegisterComponent]
public sealed partial class TrapIgnoreComponent : Component
{
    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public HashSet<ProtoId<NpcFactionPrototype>> Factions = new();
}
