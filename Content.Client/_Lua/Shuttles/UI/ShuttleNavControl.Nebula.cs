using System.Numerics;
using Content.Client._Lua.Shuttles;
using Robust.Client.Graphics;
using Robust.Shared.Map;

namespace Content.Client.Shuttles.UI;

public partial class ShuttleNavControl
{
    /// <summary>
    /// Draws the nebulae in radar range under everything else.
    /// </summary>
    private void DrawNebulae(DrawingHandleScreen handle, Matrix3x2 worldToView, MapCoordinates viewCenter)
    {
        var worldView = new Box2(viewCenter.Position - MaxRadarRangeVector, viewCenter.Position + MaxRadarRangeVector);
        NebulaRadarDraw.Draw(handle, EntManager, viewCenter.MapId, worldToView, worldView, RadarBlipSize * UIScale * 1.7f);
    }
}
