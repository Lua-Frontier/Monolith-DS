using System.Numerics;
using Content.Shared._LuaM.Shuttles;
using Robust.Client.Graphics;
using Robust.Shared.Map;

namespace Content.Client.Shuttles.UI;

// LuaM
public sealed partial class ShuttleMapControl
{
    /// <summary>
    /// Draws the circles of <see cref="MapRadiusIndicatorComponent"/> on the viewed map.
    /// </summary>
    private void DrawRadiusIndicators(DrawingHandleScreen handle, Matrix3x2 matty, Box2 viewBox)
    {
        var indicators = EntManager.System<MapRadiusIndicatorSystem>();

        // Same viewer as the radar stealth check of the grids on this map.
        var viewerUid = _console ?? _shuttleEntity;
        MapCoordinates? viewer = viewerUid != null && EntManager.EntityExists(viewerUid)
            ? _xformSystem.GetMapCoordinates(viewerUid.Value)
            : null;

        var query = EntManager.EntityQueryEnumerator<MapRadiusIndicatorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var indicator, out var xform))
        {
            if (xform.MapID != ViewingMap)
                continue;

            var center = indicators.GetCenter(uid, indicator, xform);
            if (!viewBox.Enlarged(indicator.Radius).Contains(center))
                continue;

            if (viewer is { } viewerCoords && indicators.IsHiddenFromLocalPlayer(uid, indicator, viewerCoords))
                continue;

            var localPos = Vector2.Transform(center, matty);
            var uiPos = ScalePosition(localPos with { Y = -localPos.Y });
            var radius = indicator.Radius * MinimapScale;

            handle.DrawCircle(uiPos, radius, indicator.Color.WithAlpha(indicator.FillAlpha));
            handle.DrawCircle(uiPos, radius, (indicator.OutlineColor ?? indicator.Color).WithAlpha(indicator.OutlineAlpha), filled: false);
        }
    }
}
