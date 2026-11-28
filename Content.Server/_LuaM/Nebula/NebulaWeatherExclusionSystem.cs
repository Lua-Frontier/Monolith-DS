using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._LuaM.Nebula;

public sealed partial class NebulaWeatherExclusionSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    /// <summary>
    /// Заходит ли туманность с таким центром и радиусом в запретную зону какой-нибудь станции.
    /// </summary>
    public bool IsExcluded(MapId mapId, Vector2 center, float radius)
    {
        var query = EntityQueryEnumerator<NebulaWeatherExclusionComponent, MapGridComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var exclusion, out var grid, out var xform))
        {
            if (xform.MapID != mapId)
                continue;

            var aabb = grid.LocalAABB;
            var gridCenter = Vector2.Transform(aabb.Center, _transform.GetWorldMatrix(uid));
            var minDist = radius + exclusion.Clearance + aabb.Size.Length() * 0.5f;
            if ((gridCenter - center).LengthSquared() < minDist * minDist)
                return true;
        }

        return false;
    }
}
