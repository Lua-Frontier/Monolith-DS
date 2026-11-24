using System.Numerics;
using Robust.Client.Graphics;

namespace Content.Client._LuaM.UserInterface.Hud;

public sealed class StyleBoxHud : StyleBox
{
    public const float WindowHeader = 32;
    public const float WindowFrame = 1;
    private const float FrameGap = 2;
    private const float HeaderInset = 3;

    public Color BackgroundColor { get; set; } = HudPalette.Panel;
    public Color BorderColor { get; set; } = HudPalette.Border;
    public Color InnerColor { get; set; } = HudPalette.LineDim;
    public Color HeaderColor { get; set; } = HudPalette.PanelRaised;
    public Color? AccentColor { get; set; }
    public Color? TopAccentColor { get; set; }
    public Color? BracketColor { get; set; }
    public float BorderThickness { get; set; } = 1;
    public float HeaderHeight { get; set; }
    public float AccentWidth { get; set; } = 2;
    public bool Double { get; set; }

    public float Chamfer { get; set; }

    public bool ChamferTopLeft { get; set; } = true;
    public bool ChamferBottomRight { get; set; } = true;

    private readonly List<Vector2> _tris = new();
    private readonly Vector2[] _points = new Vector2[6];
    private readonly Vector2[] _outer = new Vector2[6];
    private readonly Vector2[] _inner = new Vector2[6];

    public StyleBoxHud()
    {
        SetContentMarginOverride(Margin.All, 6);
    }

    public StyleBoxHud(Color background, Color border, Color? accent = null) : this()
    {
        BackgroundColor = background;
        BorderColor = border;
        AccentColor = accent;
    }

    public static StyleBoxHud Window(Color? background = null, Color? frame = null, float padding = 10)
    {
        var box = new StyleBoxHud(background ?? HudPalette.Panel, frame ?? HudPalette.Border)
        {
            HeaderHeight = WindowHeader,
            Double = true,
            Chamfer = 8,
        };
        box.SetContentMarginOverride(Margin.All, HeaderInset + padding);
        box.SetContentMarginOverride(Margin.Top, 0);
        return box;
    }

    public static StyleBoxHud Frame(Color? background = null, Color? frame = null, float padding = 10)
    {
        var box = new StyleBoxHud(background ?? HudPalette.Panel, frame ?? HudPalette.Border)
        {
            Double = true,
            Chamfer = 8,
        };
        box.SetContentMarginOverride(Margin.All, HeaderInset + padding);
        return box;
    }

    protected override void DoDraw(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        box = new UIBox2(MathF.Round(box.Left), MathF.Round(box.Top), MathF.Round(box.Right), MathF.Round(box.Bottom));
        var c = MathF.Round(Chamfer * uiScale);
        var t = MathF.Max(1, MathF.Round(BorderThickness * uiScale));
        var gap = MathF.Round(FrameGap * uiScale);

        if (BackgroundColor.A > 0)
            Fill(handle, box, c, t, BackgroundColor);
        Ring(handle, box, c, 0, t, BorderColor);
        if (Double)
            Ring(handle, box, c, t + gap, 1, InnerColor);

        if (HeaderHeight > 0)
        {
            var inset = MathF.Round(HeaderInset * uiScale);
            var h = MathF.Round(HeaderHeight * uiScale);
            var head = new UIBox2(box.Left + inset, box.Top + inset, box.Right - inset, box.Top + h);
            var hc = MathF.Max(0, c - MathF.Round(2.3f * uiScale));
            _tris.Clear();
            AddPolygon(head, hc, 0, cutBottom: false);
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _tris, Flat(HeaderColor));
            handle.DrawRect(new UIBox2(head.Left, head.Bottom - 1, head.Right, head.Bottom), InnerColor);
        }

        if (AccentColor is { } accent)
        {
            var top = HeaderHeight > 0 ? box.Top + MathF.Round(HeaderInset * uiScale) : box.Top + t;
            var bottom = HeaderHeight > 0 ? box.Top + MathF.Round(HeaderHeight * uiScale) - 1 : box.Bottom - t;
            handle.DrawRect(new UIBox2(box.Left + t, top, box.Left + t + AccentWidth * uiScale, bottom), accent);
        }

        if (TopAccentColor is { } topAccent)
            handle.DrawRect(new UIBox2(box.Left + t, box.Top + t, box.Right - t - c, box.Top + t + 2 * uiScale), topAccent);

        if (BracketColor is { } bracket)
            DrawBrackets(handle, box, uiScale, bracket);
    }

    private void Fill(DrawingHandleScreen handle, UIBox2 box, float chamfer, float inset, Color color)
    {
        _tris.Clear();
        AddPolygon(box, chamfer, inset, cutBottom: true);
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _tris, Flat(color));
    }

    private void Ring(DrawingHandleScreen handle, UIBox2 box, float chamfer, float inset, float width, Color color)
    {
        var outer = Polygon(box, chamfer, inset, _outer);
        var inner = Polygon(box, chamfer, inset + width, _inner);
        if (outer == null || inner == null)
            return;

        _tris.Clear();
        for (var i = 0; i < 6; i++)
        {
            var j = (i + 1) % 6;
            _tris.Add(outer[i]);
            _tris.Add(outer[j]);
            _tris.Add(inner[j]);
            _tris.Add(outer[i]);
            _tris.Add(inner[j]);
            _tris.Add(inner[i]);
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _tris, Flat(color));
    }

    private static Vector2[]? Polygon(UIBox2 box, float chamfer, float inset, Vector2[] p)
    {
        var l = box.Left + inset;
        var t = box.Top + inset;
        var r = box.Right - inset;
        var b = box.Bottom - inset;
        if (r <= l || b <= t)
            return null;

        var c = chamfer > 0 ? MathF.Max(0, chamfer - inset * 0.414f) : 0;
        p[0] = new Vector2(l, t);
        p[1] = new Vector2(r - c, t);
        p[2] = new Vector2(r, t + c);
        p[3] = new Vector2(r, b);
        p[4] = new Vector2(l + c, b);
        p[5] = new Vector2(l, b - c);
        return p;
    }

    private void AddPolygon(UIBox2 box, float chamfer, float inset, bool cutBottom)
    {
        var l = box.Left + inset;
        var t = box.Top + inset;
        var r = box.Right - inset;
        var b = box.Bottom - inset;
        if (r <= l || b <= t)
            return;

        var c = chamfer > 0 ? MathF.Max(0, chamfer - inset * 0.414f) : 0;
        var p = _points;
        p[0] = new Vector2(l, t);
        p[1] = new Vector2(r - c, t);
        p[2] = new Vector2(r, t + c);
        p[3] = new Vector2(r, b);
        p[4] = new Vector2(cutBottom ? l + c : l, b);
        p[5] = new Vector2(l, cutBottom ? b - c : b);

        for (var i = 1; i < 5; i++)
        {
            _tris.Add(p[0]);
            _tris.Add(p[i]);
            _tris.Add(p[i + 1]);
        }
    }

    public static Color Flat(Color color) => Color.ToSrgb(color);

    public static void DrawBrackets(DrawingHandleScreen handle, UIBox2 box, float uiScale, Color color)
    {
        var off = 3 * uiScale;
        var len = 8 * uiScale;
        var w = 2 * uiScale;
        var o = new UIBox2(box.Left - off, box.Top - off, box.Right + off, box.Bottom + off);
        handle.DrawRect(new UIBox2(o.Left, o.Top, o.Left + len, o.Top + w), color);
        handle.DrawRect(new UIBox2(o.Left, o.Top, o.Left + w, o.Top + len), color);
        handle.DrawRect(new UIBox2(o.Right - len, o.Top, o.Right, o.Top + w), color);
        handle.DrawRect(new UIBox2(o.Right - w, o.Top, o.Right, o.Top + len), color);
        handle.DrawRect(new UIBox2(o.Left, o.Bottom - w, o.Left + len, o.Bottom), color);
        handle.DrawRect(new UIBox2(o.Left, o.Bottom - len, o.Left + w, o.Bottom), color);
        handle.DrawRect(new UIBox2(o.Right - len, o.Bottom - w, o.Right, o.Bottom), color);
        handle.DrawRect(new UIBox2(o.Right - w, o.Bottom - len, o.Right, o.Bottom), color);
    }

    public static void DrawFrame(DrawingHandleScreen handle, UIBox2 box, float t, Color color)
    {
        handle.DrawRect(new UIBox2(box.Left, box.Top, box.Right, box.Top + t), color);
        handle.DrawRect(new UIBox2(box.Left, box.Bottom - t, box.Right, box.Bottom), color);
        handle.DrawRect(new UIBox2(box.Left, box.Top + t, box.Left + t, box.Bottom - t), color);
        handle.DrawRect(new UIBox2(box.Right - t, box.Top + t, box.Right, box.Bottom - t), color);
    }
}
