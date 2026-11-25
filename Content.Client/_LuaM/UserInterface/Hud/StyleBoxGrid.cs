using Robust.Client.Graphics;

namespace Content.Client._LuaM.UserInterface.Hud;

public sealed class StyleBoxGrid : StyleBox
{
    public Color BackgroundColor { get; set; } = HudPalette.Background;
    public Color LineColor { get; set; } = HudPalette.LineDim.WithAlpha(0.7f);
    public Color ScanlineColor { get; set; } = HudPalette.Fg.WithAlpha(0.035f);
    public float Step { get; set; } = 40;
    public Color? BorderColor { get; set; } = HudPalette.LineDim;

    protected override void DoDraw(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        handle.DrawRect(box, BackgroundColor);

        var step = Step * uiScale;
        if (step > 0)
        {
            for (var x = box.Left; x < box.Right; x += step)
            {
                handle.DrawLine(new(x, box.Top), new(x, box.Bottom), LineColor);
            }

            for (var y = box.Top; y < box.Bottom; y += step)
            {
                handle.DrawLine(new(box.Left, y), new(box.Right, y), LineColor);
            }
        }

        var scan = MathF.Max(3, MathF.Round(3 * uiScale));
        for (var y = ScanlineColor.A <= 0 ? box.Bottom : box.Top + scan - 1; y < box.Bottom; y += scan)
            handle.DrawRect(new UIBox2(box.Left, y, box.Right, y + 1), ScanlineColor);

        if (BorderColor is { } border)
            StyleBoxHud.DrawFrame(handle, box, MathF.Max(1, MathF.Round(uiScale)), border);
    }
}
