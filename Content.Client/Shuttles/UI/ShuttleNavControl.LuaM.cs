using System.Numerics;
using Content.Shared._LuaM.Shuttles;
using Robust.Client.Graphics;

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

        var viewScale = MathF.Sqrt((worldToView.M11 * worldToView.M11) + (worldToView.M12 * worldToView.M12));
        var radarRange = MaxRadarRangeVector.Length();

        var query = EntManager.EntityQueryEnumerator<MapRadiusIndicatorComponent, TransformComponent>();
        while (query.MoveNext(out _, out var indicator, out var xform))
        {
            if (xform.MapUid != mapUid)
                continue;

            var center = indicator.Center ?? _transform.GetWorldPosition(xform);
            if (Vector2.Distance(center, radarPos) > indicator.Radius + radarRange)
                continue;

            var uiPos = Vector2.Transform(center, worldToView);
            var radius = indicator.Radius * viewScale;

            handle.DrawCircle(uiPos, radius, indicator.Color.WithAlpha(0.03f));
            handle.DrawCircle(uiPos, radius, indicator.Color.WithAlpha(0.3f), filled: false);
        }
    }
}
