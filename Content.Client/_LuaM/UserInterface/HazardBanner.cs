using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._LuaM.UserInterface;

public sealed class HazardBanner : HazardBannerBackground
{
    private const string ConeTexture = "/Textures/Clothing/Head/Misc/cone.rsi/icon.png";

    public static readonly Color WarningColor = Color.FromHex("#F5C518");
    public static readonly Color AlertStripeColor = Color.FromHex("#D0453A");
    public static readonly Color AlertTextColor = Color.FromHex("#FF6B5B");

    private readonly HazardStripes _top;
    private readonly HazardStripes _bottom;
    private readonly Label _title;
    private readonly Label _hint;
    private readonly TextureRect _coneLeft;
    private readonly TextureRect _coneRight;

    public HazardBanner()
    {
        HorizontalExpand = true;
        VerticalExpand = true;

        _top = new HazardStripes { MinHeight = 18 };
        _bottom = new HazardStripes { MinHeight = 18 };
        _coneLeft = CreateCone();
        _coneRight = CreateCone();
        _title = new Label
        {
            StyleClasses = { "LabelHeadingBigger" },
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(12, 0),
        };
        _hint = new Label
        {
            HorizontalAlignment = HAlignment.Center,
            FontColorOverride = Color.FromHex("#A3ABBA"),
            Margin = new Thickness(0, 4, 0, 0),
        };

        AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            Children =
            {
                new Control { VerticalExpand = true },
                _top,
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    HorizontalExpand = true,
                    Margin = new Thickness(0, 16),
                    Children =
                    {
                        new BoxContainer
                        {
                            Orientation = BoxContainer.LayoutOrientation.Horizontal,
                            HorizontalAlignment = HAlignment.Center,
                            Children = { _coneLeft, _title, _coneRight },
                        },
                        _hint,
                    },
                },
                _bottom,
                new Control { VerticalExpand = true },
            },
        });

        SetContent(string.Empty, string.Empty, WarningColor, WarningColor, true);
    }

    public void SetContent(string title, string hint, Color stripes, Color text, bool cones)
    {
        _title.Text = title;
        _title.FontColorOverride = text;
        _hint.Text = hint;
        _hint.Visible = hint.Length > 0;
        AccentColor = stripes;
        _top.StripeColor = stripes;
        _bottom.StripeColor = stripes;
        _coneLeft.Visible = cones;
        _coneRight.Visible = cones;
    }

    public void SetWarning(string title, string hint)
    {
        SetContent(title, hint, WarningColor, WarningColor, true);
    }

    public void SetAlert(string title, string hint)
    {
        SetContent(title, hint, AlertStripeColor, AlertTextColor, false);
    }

    private static TextureRect CreateCone()
    {
        return new TextureRect
        {
            TexturePath = ConeTexture,
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            SetSize = new Vector2(48, 48),
        };
    }
}

public class HazardBannerBackground : Control
{
    private const int GradientBands = 32;
    private const float PatternWidth = 28f;

    public Color TopColor { get; set; } = Color.FromHex("#1B2230");
    public Color BottomColor { get; set; } = Color.FromHex("#0A0D13");
    public Color AccentColor { get; set; } = Color.FromHex("#F5C518");

    private readonly Vector2[] _vertices = new Vector2[6];

    public HazardBannerBackground()
    {
        RectClipContent = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var size = PixelSize;
        if (size.X <= 0 || size.Y <= 0)
            return;

        var bandHeight = size.Y / (float) GradientBands;
        for (var i = 0; i < GradientBands; i++)
        {
            var color = Color.InterpolateBetween(TopColor, BottomColor, i / (float) (GradientBands - 1));
            handle.DrawRect(new UIBox2(0, i * bandHeight, size.X, (i + 1) * bandHeight + 1), color);
        }

        var width = PatternWidth * UIScale;
        var pattern = AccentColor.WithAlpha(0.035f);
        for (float x = -size.Y; x < size.X; x += width * 2)
        {
            FillSlant(_vertices, x, 0, size.Y, width);
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _vertices, pattern);
        }

        var center = size.Y / 2f;
        var glow = AccentColor.WithAlpha(0.05f);
        for (var i = 1; i <= 4; i++)
        {
            var half = size.Y * 0.08f * i;
            handle.DrawRect(new UIBox2(0, center - half, size.X, center + half), glow);
        }
    }

    public static void FillSlant(Vector2[] vertices, float x, float top, float bottom, float width)
    {
        var height = bottom - top;
        var a = new Vector2(x, bottom);
        var b = new Vector2(x + width, bottom);
        var c = new Vector2(x + width + height, top);
        var d = new Vector2(x + height, top);
        vertices[0] = a;
        vertices[1] = b;
        vertices[2] = c;
        vertices[3] = a;
        vertices[4] = c;
        vertices[5] = d;
    }
}

public sealed class HazardStripes : Control
{
    private const float StripeWidth = 11f;

    public Color StripeColor { get; set; } = Color.FromHex("#F5C518");
    public Color BaseColor { get; set; } = Color.FromHex("#141414");

    private readonly Vector2[] _vertices = new Vector2[6];

    public HazardStripes()
    {
        RectClipContent = true;
        MinHeight = 16;
        HorizontalExpand = true;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var size = PixelSize;
        if (size.X <= 0 || size.Y <= 0)
            return;

        handle.DrawRect(new UIBox2(0, 0, size.X, size.Y), BaseColor);

        var width = StripeWidth * UIScale;
        for (var x = -size.Y - width; x < size.X; x += width * 2)
        {
            HazardBannerBackground.FillSlant(_vertices, x, 0, size.Y, width);
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _vertices, StripeColor);
        }

        var edge = Color.Black.WithAlpha(0.45f);
        var thickness = MathF.Max(1f, UIScale);
        handle.DrawRect(new UIBox2(0, 0, size.X, thickness), edge);
        handle.DrawRect(new UIBox2(0, size.Y - thickness, size.X, size.Y), edge);
    }
}
