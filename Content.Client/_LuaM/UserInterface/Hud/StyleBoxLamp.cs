using System.Numerics;
using Robust.Client.Graphics;

namespace Content.Client._LuaM.UserInterface.Hud;

public sealed class StyleBoxLamp : StyleBox
{
    public Color CaseColor { get; set; } = HudPalette.Border;
    public Color FaceColor { get; set; } = HudPalette.Background;
    public Color? RingColor { get; set; }
    public Color? InnerGlow { get; set; }
    public Color? OuterGlow { get; set; }
    public float OuterGlowSize { get; set; } = 12;

    private const int HaloSteps = 6;
    private readonly DrawVertexUV2DColor[] _band = new DrawVertexUV2DColor[24 * HaloSteps];

    public StyleBoxLamp()
    {
        SetContentMarginOverride(Margin.Vertical, 6);
        SetContentMarginOverride(Margin.Horizontal, 12);
    }

    public StyleBoxLamp(Color lamp, Lit lit, Color? caseColor = null, bool ring = false) : this()
    {
        CaseColor = caseColor ?? (lit == Lit.Fill ? lamp : HudPalette.Border);
        switch (lit)
        {
            case Lit.Off:
                FaceColor = Mix(lamp, HudPalette.Background, 0.04f);
                InnerGlow = lamp.WithAlpha(0.03f);
                break;
            case Lit.On:
                FaceColor = Mix(lamp, HudPalette.Background, 0.16f);
                InnerGlow = lamp.WithAlpha(0.11f);
                break;
            case Lit.Fill:
                FaceColor = lamp;
                InnerGlow = Color.White.WithAlpha(0.06f);
                OuterGlow = lamp.WithAlpha(0.05f);
                break;
        }

        if (ring)
            RingColor = lamp;
    }

    public enum Lit : byte
    {
        Off,
        On,
        Fill,
    }

    public static Color Mix(Color a, Color b, float amount)
    {
        return new Color(
            b.R + (a.R - b.R) * amount,
            b.G + (a.G - b.G) * amount,
            b.B + (a.B - b.B) * amount,
            1f);
    }

    protected override void DoDraw(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        box = new UIBox2(MathF.Round(box.Left), MathF.Round(box.Top), MathF.Round(box.Right), MathF.Round(box.Bottom));
        var px = MathF.Max(1, MathF.Round(uiScale));

        if (OuterGlow is { } halo)
        {
            var g = OuterGlowSize * uiScale;
            for (var i = 0; i < HaloSteps; i++)
            {
                var t0 = (float) i / HaloSteps;
                var t1 = (float) (i + 1) / HaloSteps;
                var inner = new UIBox2(box.Left - g * t0, box.Top - g * t0, box.Right + g * t0, box.Bottom + g * t0);
                var outer = new UIBox2(box.Left - g * t1, box.Top - g * t1, box.Right + g * t1, box.Bottom + g * t1);
                Band(i * 24, outer, inner, halo.WithAlpha(halo.A * (1 - t1) * (1 - t1)), halo.WithAlpha(halo.A * (1 - t0) * (1 - t0)));
            }

            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, Texture.White, _band);
        }

        if (RingColor is { } ring)
        {
            var off = 3 * px;
            StyleBoxHud.DrawFrame(handle, new UIBox2(box.Left - off - px, box.Top - off - px, box.Right + off + px, box.Bottom + off + px), 2 * px, ring);
        }

        handle.DrawRect(box, Color.Black);
        StyleBoxHud.DrawFrame(handle, box, px, CaseColor);

        var face = new UIBox2(box.Left + 2 * px, box.Top + 2 * px, box.Right - 2 * px, box.Bottom - 2 * px);
        if (face.Width <= 0 || face.Height <= 0)
            return;

        handle.DrawRect(face, FaceColor);
        if (InnerGlow is { } glow)
            handle.DrawRect(face, glow.WithAlpha(glow.A * 0.5f));
    }

    private readonly Vector2[] _o = new Vector2[4];
    private readonly Vector2[] _i = new Vector2[4];

    private void Band(int n, UIBox2 outer, UIBox2 inner, Color outerColor, Color innerColor)
    {
        var oc = outerColor;
        var ic = innerColor;
        var o = _o;
        var i = _i;
        o[0] = outer.TopLeft; o[1] = outer.TopRight; o[2] = outer.BottomRight; o[3] = outer.BottomLeft;
        i[0] = inner.TopLeft; i[1] = inner.TopRight; i[2] = inner.BottomRight; i[3] = inner.BottomLeft;

        for (var side = 0; side < 4; side++)
        {
            var next = (side + 1) % 4;
            _band[n++] = new DrawVertexUV2DColor(o[side], Vector2.Zero, oc);
            _band[n++] = new DrawVertexUV2DColor(o[next], Vector2.Zero, oc);
            _band[n++] = new DrawVertexUV2DColor(i[next], Vector2.Zero, ic);
            _band[n++] = new DrawVertexUV2DColor(o[side], Vector2.Zero, oc);
            _band[n++] = new DrawVertexUV2DColor(i[next], Vector2.Zero, ic);
            _band[n++] = new DrawVertexUV2DColor(i[side], Vector2.Zero, ic);
        }
    }
}
