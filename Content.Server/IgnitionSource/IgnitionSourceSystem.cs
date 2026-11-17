using Content.Server.Atmos.EntitySystems;
using Content.Shared.IgnitionSource;

namespace Content.Server.IgnitionSource;
public sealed partial class IgnitionSourceSystem : SharedIgnitionSourceSystem
{
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // LuaM-start
        _litWalk.Clear();
        _litWalk.AddRange(_lit);
        foreach (var uid in _litWalk)
        {
            if (Paused(uid) || !TryComp(uid, out IgnitionSourceComponent? comp) || !TryComp(uid, out TransformComponent? xform))
                continue;
        // LuaM-end

            if (!comp.Ignited)
                continue;

            if (xform.GridUid is { } gridUid)
            {
                var position = _transform.GetGridOrMapTilePosition(uid, xform);
                // TODO: Should this be happening every single tick?
                _atmosphere.HotspotExpose(gridUid, position, comp.Temperature, 50, uid, true);
            }
        }
    }
}
