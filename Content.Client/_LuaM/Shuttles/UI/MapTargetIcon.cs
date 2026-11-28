using System.Numerics;
using Content.Client._Lua.UserInterface;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client.Shuttles.UI;

public sealed class MapTargetIcon : Control
{
    public MapTargetIcon()
    {
        MinSize = new Vector2(14f, 14f);
        MaxSize = new Vector2(14f, 14f);
        HorizontalAlignment = HAlignment.Center;
        VerticalAlignment = VAlignment.Center;
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var center = new Vector2(PixelWidth * 0.5f, PixelHeight * 0.5f);
        var color = Color.FromHex("#C5CEDB");
        const float radius = 3.5f;
        const float outer = 6f;

        LunaDraw.Circle(handle, center, radius, color, false);
        LunaDraw.Circle(handle, center, 1f, color, true);
        LunaDraw.Line(handle, center + new Vector2(0f, -outer), center + new Vector2(0f, -radius - 1f), color);
        LunaDraw.Line(handle, center + new Vector2(0f, radius + 1f), center + new Vector2(0f, outer), color);
        LunaDraw.Line(handle, center + new Vector2(-outer, 0f), center + new Vector2(-radius - 1f, 0f), color);
        LunaDraw.Line(handle, center + new Vector2(radius + 1f, 0f), center + new Vector2(outer, 0f), color);
    }
}
