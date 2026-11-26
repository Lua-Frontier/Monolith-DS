using Robust.Client.Graphics;

namespace Content.Client._LuaM.UserInterface.Hud;

public sealed class StyleBoxThin : StyleBox
{
    public Color Background { get; set; } = Color.Transparent;
    public Color Border { get; set; } = HudPalette.Border;
    public Color? Strip { get; set; }
    public bool Glow { get; set; }
    public bool Sunken { get; set; }

    public StyleBoxThin(bool strip)
    {
        SetContentMarginOverride(Margin.Vertical, 5);
        SetContentMarginOverride(Margin.Horizontal, 10);
        if (strip)
            SetContentMarginOverride(Margin.Left, 20);
    }

    protected override void DoDraw(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        box = new UIBox2(MathF.Round(box.Left), MathF.Round(box.Top), MathF.Round(box.Right), MathF.Round(box.Bottom));
        var px = MathF.Max(1, MathF.Round(uiScale));

        if (Background.A > 0)
            handle.DrawRect(box, Background);
        if (Sunken)
            handle.DrawRect(new UIBox2(box.Left + px, box.Top + px, box.Right - px, box.Top + 3 * px), Color.Black.WithAlpha(0.6f));

        StyleBoxHud.DrawFrame(handle, box, px, Border);

        if (Strip is not { } strip)
            return;

        var h = MathF.Round(12 * uiScale);
        var top = MathF.Round(box.Top + (box.Height - h) / 2f);
        var lamp = new UIBox2(box.Left + 9 * px, top, box.Left + 13 * px, top + h);
        if (Glow)
        {
            for (var i = 5; i >= 1; i--)
                handle.DrawRect(new UIBox2(lamp.Left - i * px, lamp.Top - i * px, lamp.Right + i * px, lamp.Bottom + i * px), strip.WithAlpha(0.011f));
        }
        handle.DrawRect(lamp, strip);
        StyleBoxHud.DrawFrame(handle, lamp, px, Color.Black);
    }
}
