using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._LuaM.UserInterface.Hud;

public sealed class HudSquare : Control
{
    public const string StylePropertyColor = "luam-square-color";

    public Color? ColorOverride { get; set; }

    public HudSquare()
    {
        MouseFilter = MouseFilterMode.Ignore;
        SetSize = new Vector2(9, 9);
        VerticalAlignment = VAlignment.Center;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var color = ColorOverride
                    ?? (TryGetStyleProperty<Color>(StylePropertyColor, out var styled) ? styled : HudPalette.Fg);
        var s = MathF.Min(PixelSize.X, PixelSize.Y);
        var o = new Vector2(MathF.Round((PixelSize.X - s) / 2f), MathF.Round((PixelSize.Y - s) / 2f));
        handle.DrawRect(new UIBox2(o, o + new Vector2(s, s)), color);
    }
}
