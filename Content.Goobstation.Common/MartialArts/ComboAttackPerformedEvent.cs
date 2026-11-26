// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Goobstation.Common.MartialArts;

/// <summary>
/// Raised on the performer when they make an attack that can be a part of a martial arts combo.
/// </summary>
[ByRefEvent]
public readonly record struct ComboAttackPerformedEvent(
    EntityUid Performer,
    EntityUid Target,
    EntityUid Weapon,
    ComboAttackType Type);

[Serializable, NetSerializable]
public enum ComboAttackType : byte
{
    Harm,
    HarmLight,
    Disarm,
    Grab,
    Hug,
}
