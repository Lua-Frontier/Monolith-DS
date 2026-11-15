using System.Numerics;
using Content.Client._Lua.Shuttles;
using Robust.Client.Graphics;

namespace Content.Client.Shuttles.UI;

public sealed partial class ShuttleMapControl
{
    /// <summary>
    /// Draws the nebulae of the viewed map under everything else.
    /// </summary>
    private void DrawNebulae(DrawingHandleScreen handle, Matrix3x2 mapTransform, Box2 viewBox)
    {
        // Same as ScalePosition(mapPos with { Y = -mapPos.Y }), as a matrix.
        var worldToScreen = mapTransform *
                            Matrix3x2.CreateScale(new Vector2(MinimapScale, -MinimapScale)) *
                            Matrix3x2.CreateTranslation(MidPointVector);
        NebulaRadarDraw.Draw(handle, EntManager, ViewingMap, worldToScreen, viewBox, 27f * UIScale);
    }
}
