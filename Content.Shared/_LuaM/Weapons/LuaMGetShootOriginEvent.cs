using Robust.Shared.Map;

namespace Content.Shared._LuaM.Weapons;

[ByRefEvent]
public record struct LuaMGetShootOriginEvent(EntityUid Gun, EntityCoordinates Target, EntityCoordinates? Origin = null);
