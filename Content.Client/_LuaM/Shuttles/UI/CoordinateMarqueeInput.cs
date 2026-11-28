using System.Numerics;
using Content.Client._LuaM.UserInterface;
using Content.Client._LuaM.UserInterface.Hud;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;

namespace Content.Client.Shuttles.UI;

public sealed class CoordinateMarqueeInput : Control
{
    private readonly LineEdit _input;
    private readonly MarqueeLabel _display;
    private string _text = string.Empty;
    private bool _editing;
    private bool _hovered;

    public event Action<LineEdit.LineEditEventArgs>? OnTextChanged;
    public event Action<LineEdit.LineEditEventArgs>? OnTextEntered;

    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            _display.Text = value;
            if (_editing)
                _input.Text = value;
        }
    }

    public CoordinateMarqueeInput()
    {
        RectClipContent = true;

        _input = new LineEdit
        {
            HorizontalExpand = true,
            StyleClasses = { ConsoleStyles.Input },
        };
        _display = new MarqueeLabel
        {
            IdleAlign = Label.AlignMode.Left,
            FontOverride = LobbyHudStyles.MonoFont(IoCManager.Resolve<IResourceCache>(), 11),
            FontColorOverride = HudPalette.Text,
            MouseFilter = MouseFilterMode.Ignore,
        };

        _input.OnTextChanged += args =>
        {
            _text = args.Text;
            _display.Text = _text;
            OnTextChanged?.Invoke(args);
        };
        _input.OnTextEntered += args => OnTextEntered?.Invoke(args);
        _input.OnFocusEnter += _ =>
        {
            _editing = true;
            _display.Visible = false;
            _input.Text = _text;
            _input.CursorPosition = _input.Text.Length;
        };
        _input.OnFocusExit += args =>
        {
            _text = args.Text;
            _display.Text = _text;
            _editing = false;
            _input.Text = string.Empty;
            _display.Visible = true;
            _display.Hovered = _hovered;
        };
        _input.OnMouseEntered += _ =>
        {
            _hovered = true;
            _display.Hovered = !_editing;
        };
        _input.OnMouseExited += _ =>
        {
            _hovered = false;
            _display.Hovered = false;
        };

        AddChild(_input);
        AddChild(_display);
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        _input.Measure(availableSize);
        return _input.DesiredSize;
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var full = UIBox2.FromDimensions(Vector2.Zero, finalSize);
        _input.Arrange(full);
        _display.Arrange(UIBox2.FromDimensions(new Vector2(7f, 0f), new Vector2(Math.Max(0f, finalSize.X - 14f), finalSize.Y)));
        return finalSize;
    }
}
