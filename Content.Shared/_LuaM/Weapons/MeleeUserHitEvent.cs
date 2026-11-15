namespace Content.Shared._LuaM.Weapons;

/// <summary>
/// Raised on the attacker after a melee attack that dealt damage to at least one target.
/// MeleeHitEvent only goes to the weapon, so effects on the attacker itself use this one.
/// </summary>
[ByRefEvent]
public readonly record struct MeleeUserHitEvent(EntityUid Weapon, IReadOnlyList<EntityUid> Targets);
