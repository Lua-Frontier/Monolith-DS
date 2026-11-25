using System.Numerics;
using Robust.Client.Graphics;

namespace Content.Client._LuaM.UserInterface.Hud;

public sealed class StyleBoxLamp : StyleBox
{
    public Color CaseColor { get; set; } = HudPalette.Border;
    public Color FaceColor { get; set; } = HudPalette.Background;
    public Color RimColor { get; set; } = Color.Black;
    public Color? RingColor { get; set; }
    public Color? InnerGlow { get; set; }
    public float InnerGlowSize { get; set; } = 12;
    public Color? OuterGlow { get; set; }
    public float OuterGlowSize { get; set; } = 8;

    private readonly DrawVertexUV2DColor[] _band = new DrawVertexUV2DColor[24];

    private static readonly float[] GlowSteps = { 0f, 0.12f, 0.28f, 0.5f, 0.75f, 1f };

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
                FaceColor = Mix(lamp, HudPalette.Background, 0.07f);
                RimColor = Color.Black.WithAlpha(0.8f);
                InnerGlow = lamp.WithAlpha(0.07f);
                break;
            case Lit.On:
                FaceColor = Mix(lamp, HudPalette.Background, 0.16f);
                RimColor = Mix(lamp, Color.Black, 0.55f);
                InnerGlow = lamp.WithAlpha(0.22f);
                break;
            case Lit.Fill:
                FaceColor = lamp;
                RimColor = Mix(Color.Black, lamp, 0.35f);
                InnerGlow = Color.White.WithAlpha(0.12f);
                OuterGlow = lamp.WithAlpha(0.1f);
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
            Band(handle, new UIBox2(box.Left - g, box.Top - g, box.Right + g, box.Bottom + g), box, halo.WithAlpha(0), halo);
        }

        if (RingColor is { } ring)
        {
            var off = 3 * px;
            StyleBoxHud.DrawFrame(handle, new UIBox2(box.Left - off - px, box.Top - off - px, box.Right + off + px, box.Bottom + off + px), 2 * px, ring);
        }

        handle.DrawRect(box, Color.Black);
        StyleBoxHud.DrawFrame(handle, box, px, CaseColor);

        var face = new UIBox2(box.Left + 3 * px, box.Top + 3 * px, box.Right - 3 * px, box.Bottom - 3 * px);
        if (face.Width <= 0 || face.Height <= 0)
            return;

        handle.DrawRect(face, FaceColor);

        if (InnerGlow is { } glow)
        {
            var reach = MathF.Min(face.Width, face.Height) / 2f;
            var edge = glow.A * 0.8f;
            var floor = glow.A * 0.35f;
            float Alpha(float t) => floor + (edge - floor) * (1 - t) * (1 - t);

            for (var i = 0; i < GlowSteps.Length - 1; i++)
            {
                var d0 = reach * GlowSteps[i];
                var d1 = reach * GlowSteps[i + 1];
                Band(handle,
                    new UIBox2(face.Left + d0, face.Top + d0, face.Right - d0, face.Bottom - d0),
                    new UIBox2(face.Left + d1, face.Top + d1, face.Right - d1, face.Bottom - d1),
                    glow.WithAlpha(Alpha(GlowSteps[i])),
                    glow.WithAlpha(Alpha(GlowSteps[i + 1])));
            }

            var core = reach * GlowSteps[^1];
            if (face.Width - 2 * core > 0)
                handle.DrawRect(new UIBox2(face.Left + core, face.Top + core, face.Right - core, face.Bottom - core), glow.WithAlpha(floor));
        }

        StyleBoxHud.DrawFrame(handle, face, px, RimColor);
        handle.DrawRect(new UIBox2(face.Left + px, face.Top + px, face.Right - px, face.Top + 2 * px), Color.White.WithAlpha(0.05f));
    }

    private readonly Vector2[] _o = new Vector2[4];
    private readonly Vector2[] _i = new Vector2[4];

    private void Band(DrawingHandleScreen handle, UIBox2 outer, UIBox2 inner, Color outerColor, Color innerColor)
    {
        var oc = outerColor;
        var ic = innerColor;
        var o = _o;
        var i = _i;
        o[0] = outer.TopLeft; o[1] = outer.TopRight; o[2] = outer.BottomRight; o[3] = outer.BottomLeft;
        i[0] = inner.TopLeft; i[1] = inner.TopRight; i[2] = inner.BottomRight; i[3] = inner.BottomLeft;

        var n = 0;
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

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, Texture.White, _band);
    }
}
