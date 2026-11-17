using System.Numerics;
using System.Text;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client._LuaM.Vehicles.Dashboard;

public abstract class VehicleHudPanel : Control
{
    protected const float HeaderHeight = 18f;
    protected const float HeaderMiddle = HeaderHeight / 2f;
    private const float GripWidth = 14f;
    private const int TextCacheLimit = 128;
    private static readonly string[] Zeros = { "", "0", "00", "000", "0000" };

    private readonly Dictionary<string, string> _locale = new();
    private readonly Dictionary<string, string> _upper = new();
    private readonly Dictionary<(string Format, int A, int B), string> _formatted = new();
    private readonly Dictionary<(Font Font, string Text, float Width, float Scale), string> _fitted = new();

    protected static readonly Color Section = Color.FromHex("#111923F0");
    protected static readonly Color SectionBorder = Color.FromHex("#2D4757");
    protected static readonly Color Inset = Color.FromHex("#0D1219F6");
    protected static readonly Color Header = Color.FromHex("#202631F5");
    protected static readonly Color HeaderBorder = Color.FromHex("#5D6A7C");
    protected static readonly Color Accent = Color.FromHex("#4EC6E8");
    protected static readonly Color LitFill = Color.FromHex("#18344AF5");
    protected static readonly Color Off = Color.FromHex("#293844");
    protected static readonly Color OffFill = Color.FromHex("#10161FC8");
    protected static readonly Color Text = Color.FromHex("#F1F3F6");
    protected static readonly Color Muted = Color.FromHex("#7A8590");
    protected static readonly Color Warn = Color.FromHex("#F0B030");
    protected static readonly Color Danger = Color.FromHex("#E0402F");
    protected static readonly Color Good = Color.FromHex("#4FD06A");

    protected readonly Font Digits;
    protected readonly Font DigitsSmall;
    protected readonly Font Label;
    protected readonly Font TitleFont;

    protected readonly float PanelWidth;
    protected readonly float PanelHeight;
    private Vector2? _dragOffset;

    public event Action<Vector2>? Moved;

    public string Title = string.Empty;

    protected VehicleHudPanel(float width, float height)
    {
        var cache = IoCManager.Resolve<IResourceCache>();
        Digits = cache.GetFont("/Fonts/RobotoMono/RobotoMono-Bold.ttf", 22);
        DigitsSmall = cache.GetFont("/Fonts/RobotoMono/RobotoMono-Bold.ttf", 10);
        Label = cache.GetFont("/Fonts/NotoSans/NotoSans-Bold.ttf", 7);
        TitleFont = cache.GetFont("/Fonts/NotoSans/NotoSans-Bold.ttf", 9);
        PanelWidth = width;
        PanelHeight = height;
        MinSize = SetSize = new Vector2(width, height);
        MouseFilter = MouseFilterMode.Stop;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var s = UIScale;
        Box(handle, 0, 0, PanelWidth, PanelHeight, Section, SectionBorder, s);
        Box(handle, 0, 0, PanelWidth, HeaderHeight, Header, null, s);
        handle.DrawLine(new Vector2(0, HeaderHeight) * s, new Vector2(PanelWidth, HeaderHeight) * s, HeaderBorder);
        handle.DrawRect(Rect(0, 0, 3, HeaderHeight, s), Accent);

        var grip = PanelWidth - 10f;
        for (var i = 0; i < 3; i++)
        {
            handle.DrawRect(Rect(grip, HeaderMiddle - 3.5f + i * 3, 4, 1, s), _dragOffset != null ? Accent : Muted);
        }

        var titleRight = DrawHeader(handle, PanelWidth - GripWidth, s);
        DrawMiddle(handle, TitleFont, Fit(TitleFont, Upper(Title), titleRight - 6f - 9f, s), 9, HeaderMiddle, s, Accent);

        DrawBody(handle, s);
    }

    protected abstract float DrawHeader(DrawingHandleScreen handle, float right, float s);

    protected abstract void DrawBody(DrawingHandleScreen handle, float s);

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function != EngineKeyFunctions.UIClick || args.RelativePosition.Y > HeaderHeight * UIScale)
            return;

        _dragOffset = args.PointerLocation.Position / UIScale - Position;
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function != EngineKeyFunctions.UIClick || _dragOffset == null)
            return;

        _dragOffset = null;
        Moved?.Invoke(Position);
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        if (_dragOffset is not { } offset || Parent == null)
            return;

        var max = Vector2.Max(Parent.Size - Size, Vector2.Zero);
        LayoutContainer.SetPosition(this, Vector2.Clamp(args.GlobalPosition - offset, Vector2.Zero, max));
    }

    protected float StatusLight(DrawingHandleScreen handle, float right, string text, Color color, float s)
    {
        var textX = right - Measure(Label, text, s) / s;
        var dotX = textX - 9f;
        handle.DrawRect(Rect(dotX, HeaderMiddle - 3f, 6, 6, s), color);
        DrawMiddle(handle, Label, text, textX, HeaderMiddle, s, color);
        return dotX;
    }

    protected float LeadingZeroDigits(DrawingHandleScreen handle, int value, int width, float x, float middle, float s, Color color)
    {
        width = Math.Clamp(width, 1, Zeros.Length - 1);
        var digits = Format("{0}", Math.Clamp(value, 0, (int) MathF.Pow(10, width) - 1));
        var dim = DrawMiddle(handle, Digits, Zeros[Math.Max(0, width - digits.Length)], x, middle, s, Off);
        return dim + DrawMiddle(handle, Digits, digits, x + dim, middle, s, color);
    }

    protected string Locale(string id)
    {
        if (!_locale.TryGetValue(id, out var text))
            _locale[id] = text = Loc.GetString(id);

        return text;
    }

    protected string Upper(string text)
    {
        if (_upper.TryGetValue(text, out var upper))
            return upper;

        if (_upper.Count >= TextCacheLimit)
            _upper.Clear();

        upper = text.ToUpperInvariant();
        _upper[text] = upper;
        return upper;
    }

    protected string Format(string format, int a, int b = 0)
    {
        var key = (format, a, b);
        if (_formatted.TryGetValue(key, out var text))
            return text;

        if (_formatted.Count >= TextCacheLimit)
            _formatted.Clear();

        text = string.Format(format, a, b);
        _formatted[key] = text;
        return text;
    }

    protected float IndicatorWidth(string text, float s)
    {
        return MathF.Max(36f, MathF.Ceiling(Width(Label, text, s)) + 10f);
    }

    protected void Indicator(DrawingHandleScreen handle, float x, float y, float w, float h, string text, Color? color, float s)
    {
        var on = color is { } c;
        Box(handle, x, y, w, h, on ? LitFill : OffFill, on ? color : Off, s);
        var fitted = Fit(Label, text, w - 4f, s);
        var textW = Width(Label, fitted, s);
        DrawMiddle(handle, Label, fitted, x + (w - textW) / 2f, y + h / 2f, s, on ? color!.Value : Muted);
    }

    protected static void Segments(DrawingHandleScreen handle, float x, float y, float width, float height, int count, float fill, float s, Func<float, Color> colorAt)
    {
        var gap = 2f;
        var w = (width - gap * (count - 1)) / count;
        var lit = (int) MathF.Ceiling(fill * count - 0.001f);
        for (var i = 0; i < count; i++)
        {
            var t = (i + 1f) / count;
            var rect = Rect(x + i * (w + gap), y, w, height, s);
            if (i < lit)
            {
                var color = colorAt(t);
                handle.DrawRect(rect, color);
                handle.DrawRect(new UIBox2(rect.TopLeft - Vector2.One * s, rect.BottomRight + Vector2.One * s), color.WithAlpha(0.25f), false);
            }
            else
            {
                handle.DrawRect(rect, Off.WithAlpha(0.5f));
            }
        }
    }

    protected static void Box(DrawingHandleScreen handle, float x, float y, float w, float h, Color fill, Color? border, float s)
    {
        var rect = Rect(x, y, w, h, s);
        handle.DrawRect(rect, fill);
        if (border is { } b)
            handle.DrawRect(rect, b, false);
    }

    protected static UIBox2 Rect(float x, float y, float w, float h, float s)
    {
        return new UIBox2(new Vector2(x, y) * s, new Vector2(x + w, y + h) * s);
    }

    // -- Текст ставится по середине заглавных букв, а не строки: иначе он съезжает вниз в рамках
    protected static float DrawMiddle(DrawingHandleScreen handle, Font font, string text, float x, float middle, float s, Color color)
    {
        if (text.Length == 0)
            return 0f;

        handle.DrawString(font, new Vector2(x * s, middle * s - CapMiddle(font, s)), text, s, color);
        return Measure(font, text, s) / s;
    }

    protected static float DrawMiddleRight(DrawingHandleScreen handle, Font font, string text, float right, float middle, float s, Color color)
    {
        var width = Measure(font, text, s) / s;
        DrawMiddle(handle, font, text, right - width, middle, s, color);
        return right - width;
    }

    protected static void DrawMiddleCentered(DrawingHandleScreen handle, Font font, string text, float center, float middle, float s, Color color)
    {
        DrawMiddle(handle, font, text, center - Measure(font, text, s) / s / 2f, middle, s, color);
    }

    protected string Fit(Font font, string text, float maxWidth, float s)
    {
        if (maxWidth <= 0f)
            return string.Empty;

        var key = (font, text, maxWidth, s);
        if (_fitted.TryGetValue(key, out var fitted))
            return fitted;

        if (_fitted.Count >= TextCacheLimit)
            _fitted.Clear();

        fitted = Truncate(font, text, maxWidth, s);
        _fitted[key] = fitted;
        return fitted;
    }

    private static string Truncate(Font font, string text, float maxWidth, float s)
    {
        if (Measure(font, text, s) / s <= maxWidth)
            return text;

        var limit = maxWidth * s - Measure(font, "…", s);
        var width = 0f;
        var length = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (font.TryGetCharMetrics(rune, s, out var metrics))
                width += metrics.Advance;

            if (width > limit)
                break;

            length += rune.Utf16SequenceLength;
        }

        return length == 0 ? string.Empty : text[..length].TrimEnd() + "…";
    }

    protected static float Width(Font font, string text, float s)
    {
        return Measure(font, text, s) / s;
    }

    private static float CapMiddle(Font font, float s)
    {
        if (font.TryGetCharMetrics(new Rune('H'), s, out var metrics))
            return font.GetAscent(s) - metrics.BearingY + metrics.Height / 2f;

        return font.GetHeight(s) / 2f;
    }

    private static float Measure(Font font, string text, float s)
    {
        var width = 0f;
        foreach (var rune in text.EnumerateRunes())
        {
            if (font.TryGetCharMetrics(rune, s, out var metrics))
                width += metrics.Advance;
        }

        return width;
    }
}
