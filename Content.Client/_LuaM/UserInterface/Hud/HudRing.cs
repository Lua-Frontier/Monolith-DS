using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._LuaM.UserInterface.Hud;

public sealed class HudRing : Control
{
    private const int Segments = 32;
    private readonly Label _label;
    private readonly Vector2[] _points = new Vector2[Segments * 2 + 2];

    public Color RingColor { get; set; } = HudPalette.BorderBright;

    public float Fill { get; set; } = 1;

    public HudRing()
    {
        SetSize = new Vector2(40, 40);
        _label = new Label
        {
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            StyleClasses = { "LuaMHudMono" },
        };
        AddChild(_label);
    }

    public string? Text
    {
        get => _label.Text;
        set => _label.Text = value;
    }

    public void SetColor(Color color)
    {
        RingColor = color;
        _label.FontColorOverride = color;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var size = PixelSize;
        var center = new Vector2(size.X, size.Y) / 2;
        var outer = MathF.Min(size.X, size.Y) / 2 - 1;
        var inner = outer - 3 * UIScale;

        DrawArc(handle, center, outer, inner, 1, RingColor.WithAlpha(0.25f));
        DrawArc(handle, center, outer, inner, Math.Clamp(Fill, 0, 1), RingColor);

        if (string.IsNullOrEmpty(_label.Text))
            handle.DrawCircle(center, 4 * UIScale, RingColor);
    }

    private void DrawArc(DrawingHandleScreen handle, Vector2 center, float outer, float inner, float fill, Color color)
    {
        if (fill <= 0)
            return;

        var count = Math.Max(1, (int) MathF.Ceiling(Segments * fill));
        var n = 0;
        for (var i = 0; i <= count; i++)
        {
            var angle = -MathF.PI / 2 + MathF.Tau * fill * i / count;
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            _points[n++] = center + dir * outer;
            _points[n++] = center + dir * inner;
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, _points.AsSpan(0, n), color);
    }
}
