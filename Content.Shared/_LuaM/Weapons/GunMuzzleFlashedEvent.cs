namespace Content.Shared._LuaM.Weapons;

/// <summary>
/// Raised on the gun on the client right after its muzzle flash was shown, for own and other players' shots.
/// The angle is the world direction of the shot.
/// </summary>
[ByRefEvent]
public readonly record struct GunMuzzleFlashedEvent(Angle Angle);
