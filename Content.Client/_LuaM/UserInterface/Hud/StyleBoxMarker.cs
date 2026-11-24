using Robust.Client.Graphics;

namespace Content.Client._LuaM.UserInterface.Hud;

public sealed class StyleBoxMarker : StyleBox
{
    public Color Color { get; set; } = HudPalette.Orange;
    public Color OutlineColor { get; set; } = HudPalette.Background;
    public float Width { get; set; } = 4;

    protected override void DoDraw(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        var half = MathF.Round(Width * uiScale / 2);
        var center = MathF.Round(box.Center.X);
        handle.DrawRect(new UIBox2(center - half - 1, box.Top, center + half + 1, box.Bottom), OutlineColor);
        handle.DrawRect(new UIBox2(center - half, box.Top, center + half, box.Bottom), Color);
    }
}
