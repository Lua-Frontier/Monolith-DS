// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage;

namespace Content.Shared._LuaM.TheCircle.Geist;

[RegisterComponent]
public sealed partial class GeistLethalStrikeComponent : Component
{
    [DataField(required: true)]
    public DamageSpecifier Damage = new();

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(120);

    [DataField]
    public bool Armed;

    [DataField]
    public TimeSpan NextReady;
}
