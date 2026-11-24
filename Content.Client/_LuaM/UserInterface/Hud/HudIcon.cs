using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._LuaM.UserInterface.Hud;

public enum HudIconKind : byte
{
    Power,
    Gear,
    Help,
    Vote,
    Close,
    Dice,
    RotateLeft,
    RotateRight,
    More,
    Rules,
    Stats,
    Notes,
    Reset,
    Shirt,
    Person,
    Briefcase,
    Star,
    Flag,
    Palette,
    Book,
}

public sealed class HudIcon : Control
{
    private const float Grid = 24f;

    private readonly List<Vector2> _tris = new();
    private float _scale;
    private Vector2 _origin;

    public HudIconKind Kind { get; set; }
    public float Stroke { get; set; } = 2.2f;
    public Color? IconColor { get; set; }
    public Color HoverColor { get; set; } = HudPalette.Fg;

    public HudIcon()
    {
        MouseFilter = MouseFilterMode.Ignore;
        MinSize = new Vector2(20, 20);
        HorizontalAlignment = HAlignment.Center;
        VerticalAlignment = VAlignment.Center;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var size = MathF.Min(PixelSize.X, PixelSize.Y);
        _scale = size / Grid;
        _origin = new Vector2((PixelSize.X - size) / 2f, (PixelSize.Y - size) / 2f);
        _tris.Clear();

        BuildShape();

        var button = Parent as BaseButton;
        var color = IconColor ?? HudPalette.Text;
        if (button is { Disabled: true })
            color = HudPalette.TextDim;
        else if (button is { IsHovered: true } || button?.DrawMode == BaseButton.DrawModeEnum.Pressed)
            color = IconColor == null ? HoverColor : color;

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _tris, StyleBoxHud.Flat(color));
    }

    private void BuildShape()
    {
        switch (Kind)
        {
            case HudIconKind.Power:
                Arc(new Vector2(12, 13), 7.5f, -55, 235);
                Line(new Vector2(12, 3), new Vector2(12, 12));
                break;
            case HudIconKind.Gear:
                Arc(new Vector2(12, 12), 6.2f, 0, 360);
                Arc(new Vector2(12, 12), 2.2f, 0, 360, 1.8f);
                for (var i = 0; i < 8; i++)
                {
                    var a = MathF.PI / 4 * i;
                    var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
                    Line(new Vector2(12, 12) + dir * 6.5f, new Vector2(12, 12) + dir * 9.8f, 2.8f);
                }
                break;
            case HudIconKind.Help:
                Arc(new Vector2(12, 12), 9.5f, 0, 360, 1.8f);
                Arc(new Vector2(12, 9.6f), 3.2f, 170, 405);
                Line(new Vector2(12 + 3.2f * MathF.Cos(Deg(45)), 9.6f + 3.2f * MathF.Sin(Deg(45))), new Vector2(12, 14.2f));
                Rect(new Vector2(10.9f, 16.4f), new Vector2(13.1f, 18.6f));
                break;
            case HudIconKind.Vote:
                Box(new Vector2(4, 12), new Vector2(20, 21));
                Line(new Vector2(8, 12), new Vector2(8, 4));
                Line(new Vector2(8, 4), new Vector2(16, 4));
                Line(new Vector2(16, 4), new Vector2(16, 12));
                Line(new Vector2(9.5f, 8), new Vector2(11.5f, 10), 1.8f);
                Line(new Vector2(11.5f, 10), new Vector2(14.8f, 6), 1.8f);
                break;
            case HudIconKind.Close:
                Line(new Vector2(6, 6), new Vector2(18, 18), 2.6f);
                Line(new Vector2(18, 6), new Vector2(6, 18), 2.6f);
                break;
            case HudIconKind.Dice:
                Box(new Vector2(4, 4), new Vector2(20, 20));
                Rect(new Vector2(7.5f, 7.5f), new Vector2(10, 10));
                Rect(new Vector2(14, 14), new Vector2(16.5f, 16.5f));
                Rect(new Vector2(14, 7.5f), new Vector2(16.5f, 10));
                Rect(new Vector2(7.5f, 14), new Vector2(10, 16.5f));
                break;
            case HudIconKind.RotateLeft:
            case HudIconKind.Reset:
                Arc(new Vector2(12, 12), 7.5f, 200, 470);
                Line(new Vector2(4.2f, 4.5f), new Vector2(4.6f, 10.2f));
                Line(new Vector2(4.6f, 10.2f), new Vector2(10.2f, 9.4f));
                break;
            case HudIconKind.RotateRight:
                Arc(new Vector2(12, 12), 7.5f, -290, -20);
                Line(new Vector2(19.8f, 4.5f), new Vector2(19.4f, 10.2f));
                Line(new Vector2(19.4f, 10.2f), new Vector2(13.8f, 9.4f));
                break;
            case HudIconKind.More:
                Rect(new Vector2(4, 10.5f), new Vector2(7, 13.5f));
                Rect(new Vector2(10.5f, 10.5f), new Vector2(13.5f, 13.5f));
                Rect(new Vector2(17, 10.5f), new Vector2(20, 13.5f));
                break;
            case HudIconKind.Rules:
                Box(new Vector2(5, 3), new Vector2(19, 21));
                Line(new Vector2(8.5f, 8), new Vector2(15.5f, 8), 1.8f);
                Line(new Vector2(8.5f, 12), new Vector2(15.5f, 12), 1.8f);
                Line(new Vector2(8.5f, 16), new Vector2(13, 16), 1.8f);
                break;
            case HudIconKind.Stats:
                Line(new Vector2(3, 20.5f), new Vector2(21, 20.5f));
                Rect(new Vector2(5, 12), new Vector2(8, 19));
                Rect(new Vector2(10.5f, 5), new Vector2(13.5f, 19));
                Rect(new Vector2(16, 9), new Vector2(19, 19));
                break;
            case HudIconKind.Notes:
                Box(new Vector2(3.5f, 4), new Vector2(20.5f, 16));
                Line(new Vector2(7, 16), new Vector2(7, 20.5f));
                Line(new Vector2(7, 20.5f), new Vector2(11.5f, 16));
                Line(new Vector2(12, 7.5f), new Vector2(12, 10.5f), 2f);
                Rect(new Vector2(11, 12), new Vector2(13, 14));
                break;
            case HudIconKind.Person:
                Arc(new Vector2(12, 8), 3.8f, 0, 360);
                Arc(new Vector2(12, 21), 7.5f, 200, 340);
                break;
            case HudIconKind.Briefcase:
                Box(new Vector2(3.5f, 8), new Vector2(20.5f, 20));
                Line(new Vector2(9, 8), new Vector2(9, 4.5f));
                Line(new Vector2(9, 4.5f), new Vector2(15, 4.5f));
                Line(new Vector2(15, 4.5f), new Vector2(15, 8));
                Line(new Vector2(3.5f, 13), new Vector2(20.5f, 13), 1.6f);
                break;
            case HudIconKind.Star:
                for (var i = 0; i < 5; i++)
                {
                    var a0 = Deg(-90 + 144 * i);
                    var a1 = Deg(-90 + 144 * (i + 1));
                    Line(new Vector2(12 + 9 * MathF.Cos(a0), 12.5f + 9 * MathF.Sin(a0)), new Vector2(12 + 9 * MathF.Cos(a1), 12.5f + 9 * MathF.Sin(a1)), 1.7f);
                }
                break;
            case HudIconKind.Flag:
                Line(new Vector2(5, 3), new Vector2(5, 21.5f));
                Line(new Vector2(5, 4.5f), new Vector2(19, 4.5f));
                Line(new Vector2(19, 4.5f), new Vector2(15.5f, 8.5f));
                Line(new Vector2(15.5f, 8.5f), new Vector2(19, 12.5f));
                Line(new Vector2(19, 12.5f), new Vector2(5, 12.5f));
                break;
            case HudIconKind.Book:
                Line(new Vector2(12, 6), new Vector2(12, 20));
                Line(new Vector2(12, 6), new Vector2(4, 4.5f));
                Line(new Vector2(4, 4.5f), new Vector2(4, 18.5f));
                Line(new Vector2(4, 18.5f), new Vector2(12, 20));
                Line(new Vector2(12, 6), new Vector2(20, 4.5f));
                Line(new Vector2(20, 4.5f), new Vector2(20, 18.5f));
                Line(new Vector2(20, 18.5f), new Vector2(12, 20));
                break;
            case HudIconKind.Palette:
                Arc(new Vector2(12, 12), 8.5f, 0, 300);
                Rect(new Vector2(7.5f, 8), new Vector2(10, 10.5f));
                Rect(new Vector2(12, 6), new Vector2(14.5f, 8.5f));
                Rect(new Vector2(15.5f, 10), new Vector2(18, 12.5f));
                Rect(new Vector2(8, 13.5f), new Vector2(10.5f, 16));
                break;
            case HudIconKind.Shirt:
                Line(new Vector2(8, 4), new Vector2(3, 7.5f));
                Line(new Vector2(3, 7.5f), new Vector2(5, 12));
                Line(new Vector2(5, 12), new Vector2(7.5f, 11));
                Line(new Vector2(7.5f, 10), new Vector2(7.5f, 20.5f));
                Line(new Vector2(7.5f, 20.5f), new Vector2(16.5f, 20.5f));
                Line(new Vector2(16.5f, 20.5f), new Vector2(16.5f, 10));
                Line(new Vector2(16.5f, 11), new Vector2(19, 12));
                Line(new Vector2(19, 12), new Vector2(21, 7.5f));
                Line(new Vector2(21, 7.5f), new Vector2(16, 4));
                Line(new Vector2(16, 4), new Vector2(8, 4));
                break;
        }
    }

    private static float Deg(float degrees) => degrees * MathF.PI / 180f;

    private Vector2 P(Vector2 grid) => _origin + grid * _scale;

    private void Line(Vector2 a, Vector2 b, float? width = null)
    {
        var pa = P(a);
        var pb = P(b);
        var dir = pb - pa;
        if (dir.LengthSquared() < 0.0001f)
            return;

        var half = (width ?? Stroke) * _scale / 2f;
        dir = Vector2.Normalize(dir);
        var n = new Vector2(-dir.Y, dir.X) * half;
        pa -= dir * half;
        pb += dir * half;

        _tris.Add(pa + n);
        _tris.Add(pb + n);
        _tris.Add(pb - n);
        _tris.Add(pa + n);
        _tris.Add(pb - n);
        _tris.Add(pa - n);
    }

    private void Arc(Vector2 center, float radius, float fromDeg, float toDeg, float? width = null)
    {
        var half = (width ?? Stroke) / 2f;
        var steps = Math.Max(8, (int) (MathF.Abs(toDeg - fromDeg) / 10f));
        var c = P(center);
        for (var i = 0; i < steps; i++)
        {
            var a0 = Deg(fromDeg + (toDeg - fromDeg) * i / steps);
            var a1 = Deg(fromDeg + (toDeg - fromDeg) * (i + 1) / steps);
            var d0 = new Vector2(MathF.Cos(a0), MathF.Sin(a0));
            var d1 = new Vector2(MathF.Cos(a1), MathF.Sin(a1));
            var o0 = c + d0 * (radius + half) * _scale;
            var i0 = c + d0 * (radius - half) * _scale;
            var o1 = c + d1 * (radius + half) * _scale;
            var i1 = c + d1 * (radius - half) * _scale;
            _tris.Add(o0);
            _tris.Add(o1);
            _tris.Add(i1);
            _tris.Add(o0);
            _tris.Add(i1);
            _tris.Add(i0);
        }
    }

    private void Rect(Vector2 topLeft, Vector2 bottomRight)
    {
        var a = P(topLeft);
        var b = P(bottomRight);
        _tris.Add(a);
        _tris.Add(new Vector2(b.X, a.Y));
        _tris.Add(b);
        _tris.Add(a);
        _tris.Add(b);
        _tris.Add(new Vector2(a.X, b.Y));
    }

    private void Box(Vector2 topLeft, Vector2 bottomRight)
    {
        var t = Stroke / 2f;
        Rect(topLeft - new Vector2(t, t), new Vector2(bottomRight.X + t, topLeft.Y + t));
        Rect(new Vector2(topLeft.X - t, bottomRight.Y - t), bottomRight + new Vector2(t, t));
        Rect(new Vector2(topLeft.X - t, topLeft.Y + t), new Vector2(topLeft.X + t, bottomRight.Y - t));
        Rect(new Vector2(bottomRight.X - t, topLeft.Y + t), new Vector2(bottomRight.X + t, bottomRight.Y - t));
    }
}
