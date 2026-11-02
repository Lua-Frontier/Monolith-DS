using System.Numerics;
using System.Text;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._LuaM.UserInterface;

public sealed class MarqueeLabel : Control
{
    private const string Ellipsis = "...";
    private const float ScrollSpeed = 50f;
    private const float EndPause = 0.8f;

    private readonly Label _label;

    private string _text = string.Empty;
    private float _layoutWidth = -1f;
    private float _textWidth;
    private float _offset;
    private float _pause;
    private int _direction = 1;
    private bool _hovered;

    public MarqueeLabel()
    {
        RectClipContent = true;
        MouseFilter = MouseFilterMode.Ignore;
        HorizontalExpand = true;

        _label = new Label
        {
            StyleClasses = { ContainerButton.StyleClassButton },
            VerticalAlignment = VAlignment.Center,
        };
        AddChild(_label);
    }

    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            _layoutWidth = -1f;
        }
    }

    public bool Hovered
    {
        get => _hovered;
        set
        {
            if (_hovered == value)
                return;

            _hovered = value;
            _offset = 0f;
            _direction = 1;
            _pause = EndPause;
            _layoutWidth = -1f;
        }
    }

    private bool Scrolling => _hovered && _textWidth > Width;

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!MathHelper.CloseTo(_layoutWidth, Width))
            UpdateLabel();

        if (!Scrolling)
            return;

        if (_pause > 0f)
        {
            _pause -= args.DeltaSeconds;
            return;
        }

        var max = _textWidth - Width;
        _offset = Math.Clamp(_offset + _direction * ScrollSpeed * args.DeltaSeconds, 0f, max);
        if (_offset <= 0f || _offset >= max)
        {
            _direction = -_direction;
            _pause = EndPause;
        }

        InvalidateArrange();
    }

    private void UpdateLabel()
    {
        _layoutWidth = Width;

        if (!_label.TryGetStyleProperty<Font>(Label.StylePropertyFont, out var font))
            font = UserInterfaceManager.ThemeDefaults.LabelFont;

        _textWidth = MeasureText(font, _text);

        if (Scrolling)
        {
            _label.Align = Label.AlignMode.Left;
            _label.Text = _text;
        }
        else
        {
            _label.Align = Label.AlignMode.Center;
            _label.Text = _textWidth > Width ? Truncate(font, Width) : _text;
        }

        InvalidateArrange();
    }

    private string Truncate(Font font, float width)
    {
        var available = width - MeasureText(font, Ellipsis);
        var used = 0f;
        var builder = new StringBuilder();

        foreach (var rune in _text.EnumerateRunes())
        {
            used += MeasureRune(font, rune);
            if (used > available)
                break;

            builder.Append(rune);
        }

        return builder.ToString().TrimEnd() + Ellipsis;
    }

    private float MeasureText(Font font, string text)
    {
        var width = 0f;
        foreach (var rune in text.EnumerateRunes())
            width += MeasureRune(font, rune);

        return width;
    }

    private float MeasureRune(Font font, Rune rune)
    {
        return (font.GetCharMetrics(rune, UIScale)?.Advance ?? 0) / UIScale;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        _label.Measure(new Vector2(float.PositiveInfinity, availableSize.Y));
        return new Vector2(0f, _label.DesiredSize.Y);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        _label.Arrange(Scrolling
            ? UIBox2.FromDimensions(-_offset, 0f, _textWidth, finalSize.Y)
            : UIBox2.FromDimensions(0f, 0f, finalSize.X, finalSize.Y));

        return finalSize;
    }
}
