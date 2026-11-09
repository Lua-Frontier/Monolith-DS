using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._LuaM.Yupi;

public sealed class UsspBankBackground : Control
{
    private const int GradientBands = 32;
    private const float StripeWidth = 22f;
    private const float StarSpacing = 46f;
    private const float StarRadius = 5f;

    private static readonly Color TopColor = Color.FromHex("#5E1212");
    private static readonly Color BottomColor = Color.FromHex("#240505");
    private static readonly Color Gold = Color.FromHex("#E8C25A");

    private readonly Vector2[] _stripe = new Vector2[6];
    private readonly Vector2[] _star = new Vector2[30];

    public UsspBankBackground()
    {
        RectClipContent = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var size = PixelSize;
        if (size.X <= 0 || size.Y <= 0)
            return;

        var band = size.Y / (float) GradientBands;
        for (var i = 0; i < GradientBands; i++)
        {
            var color = Color.InterpolateBetween(TopColor, BottomColor, i / (float) (GradientBands - 1));
            handle.DrawRect(new UIBox2(0, i * band, size.X, (i + 1) * band + 1), color);
        }

        var stripe = StripeWidth * UIScale;
        var stripeColor = Gold.WithAlpha(0.025f);
        for (float x = -size.Y; x < size.X; x += stripe * 2)
        {
            _stripe[0] = new Vector2(x, size.Y);
            _stripe[1] = new Vector2(x + stripe, size.Y);
            _stripe[2] = new Vector2(x + stripe + size.Y, 0);
            _stripe[3] = _stripe[0];
            _stripe[4] = _stripe[2];
            _stripe[5] = new Vector2(x + size.Y, 0);
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _stripe, stripeColor);
        }

        var spacing = StarSpacing * UIScale;
        var starColor = Gold.WithAlpha(0.07f);
        var row = 0;
        for (var y = spacing / 2; y < size.Y + spacing; y += spacing, row++)
        {
            var shift = row % 2 == 0 ? 0f : spacing / 2;
            for (var x = shift; x < size.X + spacing; x += spacing)
                DrawStar(handle, new Vector2(x, y), StarRadius * UIScale, starColor);
        }

        DrawStar(handle, new Vector2(size.X / 2f, size.Y * 0.45f), MathF.Min(size.X, size.Y) * 0.32f, Gold.WithAlpha(0.045f));

        var edge = Color.Black.WithAlpha(0.06f);
        for (var i = 0; i < 6; i++)
        {
            var inset = i * 4 * UIScale;
            handle.DrawRect(new UIBox2(0, 0, size.X, inset + 4 * UIScale), edge);
            handle.DrawRect(new UIBox2(0, size.Y - inset - 4 * UIScale, size.X, size.Y), edge);
        }
    }

    private void DrawStar(DrawingHandleScreen handle, Vector2 center, float radius, Color color)
    {
        var inner = radius * 0.4f;
        for (var i = 0; i < 5; i++)
        {
            var outerAngle = -MathF.PI / 2 + i * MathF.Tau / 5;
            var leftAngle = outerAngle - MathF.PI / 5;
            var rightAngle = outerAngle + MathF.PI / 5;
            var outer = center + new Vector2(MathF.Cos(outerAngle), MathF.Sin(outerAngle)) * radius;
            var left = center + new Vector2(MathF.Cos(leftAngle), MathF.Sin(leftAngle)) * inner;
            var right = center + new Vector2(MathF.Cos(rightAngle), MathF.Sin(rightAngle)) * inner;
            var o = i * 6;
            _star[o] = center;
            _star[o + 1] = left;
            _star[o + 2] = outer;
            _star[o + 3] = center;
            _star[o + 4] = outer;
            _star[o + 5] = right;
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _star, color);
    }
}
