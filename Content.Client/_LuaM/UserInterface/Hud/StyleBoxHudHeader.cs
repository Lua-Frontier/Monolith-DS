using System.Numerics;
using Robust.Client.Graphics;

namespace Content.Client._LuaM.UserInterface.Hud;

public sealed class StyleBoxHudHeader : StyleBox
{
    public Color BackgroundColor { get; set; } = HudPalette.PanelRaised;
    public Color LineColor { get; set; } = HudPalette.LineDim;
    public Color? AccentColor { get; set; }
    public float Inset { get; set; } = 3;
    public float Chamfer { get; set; } = 6;

    private readonly List<Vector2> _tris = new();

    protected override void DoDraw(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        var inset = MathF.Round(Inset * uiScale);
        var l = MathF.Round(box.Left) + inset;
        var t = MathF.Round(box.Top) + inset;
        var r = MathF.Round(box.Right) - inset;
        var b = MathF.Round(box.Bottom);
        if (r <= l || b <= t)
            return;

        var c = MathF.Min(MathF.Round(Chamfer * uiScale), b - t);
        _tris.Clear();
        var p0 = new Vector2(l, t);
        var p1 = new Vector2(r - c, t);
        var p2 = new Vector2(r, t + c);
        var p3 = new Vector2(r, b);
        var p4 = new Vector2(l, b);
        _tris.Add(p0); _tris.Add(p1); _tris.Add(p2);
        _tris.Add(p0); _tris.Add(p2); _tris.Add(p3);
        _tris.Add(p0); _tris.Add(p3); _tris.Add(p4);
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _tris, StyleBoxHud.Flat(BackgroundColor));

        var px = MathF.Max(1, MathF.Round(uiScale));
        handle.DrawRect(new UIBox2(l, b - px, r, b), LineColor);

        if (AccentColor is { } accent)
            handle.DrawRect(new UIBox2(l, t, l + 2 * px, b - px), accent);
    }
}
