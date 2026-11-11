using System.Numerics;
using Content.Shared._LuaM.Shuttles;
using Robust.Client.Graphics;
using Robust.Shared.Map;

namespace Content.Client.Shuttles.UI;

// LuaM
public partial class ShuttleNavControl
{
    /// <summary>
    /// Draws the circles of <see cref="MapRadiusIndicatorComponent"/> on the radar.
    /// </summary>
    private void DrawRadiusIndicators(DrawingHandleScreen handle, Matrix3x2 worldToView, Vector2 radarPos, EntityUid? mapUid)
    {
        if (mapUid == null)
            return;

        var indicators = EntManager.System<MapRadiusIndicatorSystem>();
        var viewScale = MathF.Sqrt((worldToView.M11 * worldToView.M11) + (worldToView.M12 * worldToView.M12));
        var radarRange = MaxRadarRangeVector.Length();

        var query = EntManager.EntityQueryEnumerator<MapRadiusIndicatorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var indicator, out var xform))
        {
            if (xform.MapUid != mapUid)
                continue;

            var center = indicators.GetCenter(uid, indicator, xform);
            if (Vector2.Distance(center, radarPos) > indicator.Radius + radarRange)
                continue;

            if (indicators.IsHiddenFromLocalPlayer(uid, indicator, new MapCoordinates(radarPos, xform.MapID)))
                continue;

            var uiPos = Vector2.Transform(center, worldToView);
            var radius = indicator.Radius * viewScale;

            handle.DrawCircle(uiPos, radius, indicator.Color.WithAlpha(indicator.FillAlpha));
            handle.DrawCircle(uiPos, radius, (indicator.OutlineColor ?? indicator.Color).WithAlpha(indicator.OutlineAlpha), filled: false);
        }
    }
}
