using Robust.Shared.Map;

namespace Content.Shared._LuaM.Weapons;

[ByRefEvent]
public record struct LuaMGetMuzzleFlashOriginEvent(EntityCoordinates? Origin = null);
