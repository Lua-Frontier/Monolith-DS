// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Goobstation.Weapons.MissChance; // LuaM renamed: namespace Content.Goobstation.Shared.Weapons.MissChance; -> namespace Content.Shared._Goobstation.Weapons.MissChance;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MissChanceComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Chance = 0.35f; // 65% to hit the target
}
