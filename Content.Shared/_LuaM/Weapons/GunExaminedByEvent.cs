using Content.Shared.Examine;

namespace Content.Shared._LuaM.Weapons;

/// <summary>
/// Raised on whoever examines a gun, so their own traits can add lines to the examine text.
/// </summary>
[ByRefEvent]
public readonly record struct GunExaminedByEvent(EntityUid Gun, ExaminedEvent Examine);
