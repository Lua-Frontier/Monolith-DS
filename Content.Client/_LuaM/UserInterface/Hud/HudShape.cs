using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._LuaM.UserInterface.Hud;

public enum HudStatus : byte
{
    Nominal,
    Advisory,
    Caution,
    Warning,
    Offline,
}

public static class HudStatusColors
{
    public static Color Of(HudStatus status) => status switch
    {
        HudStatus.Nominal => HudPalette.Good,
        HudStatus.Advisory => HudPalette.Advisory,
        HudStatus.Caution => HudPalette.Caution,
        HudStatus.Warning => HudPalette.Bad,
        _ => HudPalette.Offline,
    };

    public static Color TextOf(HudStatus status) => status == HudStatus.Offline ? HudPalette.TextDim : Of(status);
}

public sealed class HudShape : Control
{
    private readonly List<Vector2> _tris = new();
    private HudStatus _status;

    public HudStatus Status
    {
        get => _status;
        set => _status = value;
    }

    public HudShape()
    {
        MouseFilter = MouseFilterMode.Ignore;
        SetSize = new Vector2(10, 10);
        VerticalAlignment = VAlignment.Center;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var s = MathF.Min(PixelSize.X, PixelSize.Y);
        var o = new Vector2((PixelSize.X - s) / 2f, (PixelSize.Y - s) / 2f);
        var color = HudStatusColors.Of(_status);
        _tris.Clear();

        switch (_status)
        {
            case HudStatus.Nominal:
            {
                var c = o + new Vector2(s / 2f, s / 2f);
                const int segments = 20;
                for (var i = 0; i < segments; i++)
                {
                    var a0 = MathF.Tau * i / segments;
                    var a1 = MathF.Tau * (i + 1) / segments;
                    _tris.Add(c);
                    _tris.Add(c + new Vector2(MathF.Cos(a0), MathF.Sin(a0)) * s / 2f);
                    _tris.Add(c + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * s / 2f);
                }
                break;
            }
            case HudStatus.Advisory:
                handle.DrawRect(new UIBox2(o, o + new Vector2(s, s)), color);
                return;
            case HudStatus.Caution:
                _tris.Add(o + new Vector2(s / 2f, 0));
                _tris.Add(o + new Vector2(s, s));
                _tris.Add(o + new Vector2(0, s));
                break;
            case HudStatus.Warning:
                _tris.Add(o + new Vector2(s / 2f, 0));
                _tris.Add(o + new Vector2(s, s / 2f));
                _tris.Add(o + new Vector2(s / 2f, s));
                _tris.Add(o + new Vector2(s / 2f, 0));
                _tris.Add(o + new Vector2(s / 2f, s));
                _tris.Add(o + new Vector2(0, s / 2f));
                break;
            default:
                StyleBoxHud.DrawFrame(handle, new UIBox2(o, o + new Vector2(s, s)), MathF.Max(1, MathF.Round(1.5f * UIScale)), color);
                return;
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _tris, StyleBoxHud.Flat(color));
    }
}
