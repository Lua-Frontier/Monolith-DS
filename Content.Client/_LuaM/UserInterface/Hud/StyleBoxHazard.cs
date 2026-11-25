using System.Numerics;
using Robust.Client.Graphics;

namespace Content.Client._LuaM.UserInterface.Hud;

public sealed class StyleBoxHazard : StyleBox
{
    public Color StripeColor { get; set; } = HudPalette.Caution;
    public Color GapColor { get; set; } = HudPalette.Void;
    public Color FaceColor { get; set; } = HudPalette.Background;
    public float Band { get; set; } = 6;
    public float Stripe { get; set; } = 8;
    public float FaceStripeAlpha { get; set; } = 0.1f;

    private readonly List<Vector2> _tris = new();
    private readonly List<Vector2> _poly = new();
    private readonly List<Vector2> _next = new();

    public StyleBoxHazard()
    {
        SetContentMarginOverride(Margin.Vertical, 8);
        SetContentMarginOverride(Margin.Horizontal, 12);
    }

    protected override void DoDraw(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        box = new UIBox2(MathF.Round(box.Left), MathF.Round(box.Top), MathF.Round(box.Right), MathF.Round(box.Bottom));
        var px = MathF.Max(1, MathF.Round(uiScale));
        var band = MathF.Round(Band * uiScale);

        handle.DrawRect(box, GapColor);
        var face = new UIBox2(box.Left + px, box.Top + band, box.Right - px, box.Bottom - band);
        handle.DrawRect(face, FaceColor);

        Stripes(handle, new UIBox2(box.Left, box.Top, box.Right, box.Top + band), StripeColor, uiScale);
        Stripes(handle, new UIBox2(box.Left, box.Bottom - band, box.Right, box.Bottom), StripeColor, uiScale);
        if (FaceStripeAlpha > 0)
            Stripes(handle, face, StripeColor.WithAlpha(FaceStripeAlpha), uiScale);

        StyleBoxHud.DrawFrame(handle, box, px, StripeColor);
    }

    private void Stripes(DrawingHandleScreen handle, UIBox2 rect, Color color, float uiScale)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
            return;

        var w = Stripe * uiScale;
        _tris.Clear();
        var start = rect.Left - rect.Height;
        for (var c = start; c < rect.Right; c += w * 2)
        {
            _poly.Clear();
            _poly.Add(new Vector2(rect.Left, rect.Top));
            _poly.Add(new Vector2(rect.Right, rect.Top));
            _poly.Add(new Vector2(rect.Right, rect.Bottom));
            _poly.Add(new Vector2(rect.Left, rect.Bottom));
            Clip(v => v.X - (rect.Bottom - v.Y) - c);
            Clip(v => c + w - (v.X - (rect.Bottom - v.Y)));
            for (var i = 1; i + 1 < _poly.Count; i++)
            {
                _tris.Add(_poly[0]);
                _tris.Add(_poly[i]);
                _tris.Add(_poly[i + 1]);
            }
        }

        if (_tris.Count > 0)
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _tris, StyleBoxHud.Flat(color));
    }

    private void Clip(Func<Vector2, float> inside)
    {
        _next.Clear();
        for (var i = 0; i < _poly.Count; i++)
        {
            var a = _poly[i];
            var b = _poly[(i + 1) % _poly.Count];
            var da = inside(a);
            var db = inside(b);
            if (da >= 0)
                _next.Add(a);
            if (da >= 0 != db >= 0)
                _next.Add(a + (b - a) * (da / (da - db)));
        }

        _poly.Clear();
        _poly.AddRange(_next);
    }
}
