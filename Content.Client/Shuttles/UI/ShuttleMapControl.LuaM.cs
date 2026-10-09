using System.Numerics;
using Content.Shared._LuaM.Shuttles;
using Robust.Client.Graphics;

namespace Content.Client.Shuttles.UI;

// LuaM
public sealed partial class ShuttleMapControl
{
    /// <summary>
    /// Draws the circles of <see cref="MapRadiusIndicatorComponent"/> on the viewed map.
    /// </summary>
    private void DrawRadiusIndicators(DrawingHandleScreen handle, Matrix3x2 matty, Box2 viewBox)
    {
        var query = EntManager.EntityQueryEnumerator<MapRadiusIndicatorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var indicator, out var xform))
        {
            if (xform.MapID != ViewingMap)
                continue;

            var center = indicator.Center ?? _xformSystem.GetWorldPosition(xform);
            if (!viewBox.Enlarged(indicator.Radius).Contains(center))
                continue;

            var localPos = Vector2.Transform(center, matty);
            var uiPos = ScalePosition(localPos with { Y = -localPos.Y });
            var radius = indicator.Radius * MinimapScale;

            handle.DrawCircle(uiPos, radius, indicator.Color.WithAlpha(0.03f));
            handle.DrawCircle(uiPos, radius, indicator.Color.WithAlpha(0.3f), filled: false);
        }
    }
}
