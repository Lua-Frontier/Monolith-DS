using System.Linq;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._LuaM.UserInterface.Hud;

public sealed class HudTabRail : BoxContainer
{
    private const int ShortLength = 9;
    public const float StripHeight = 54;

    private readonly TabContainer _tabs;
    private readonly List<(Control Tab, ContainerButton Button, Label Number, Label Title)> _buttons = new();

    private readonly bool _horizontal;
    private readonly Dictionary<ContainerButton, HudIcon> _icons = new();

    public Func<Control, HudIconKind?>? IconFor { get; set; }

    public Control? Trailer { get; set; }

    public HudTabRail(TabContainer tabs, bool horizontal = false)
    {
        _tabs = tabs;
        _horizontal = horizontal;
        _tabs.TabsVisible = false;
        _tabs.OnTabChanged += _ => UpdateStyles();
        Orientation = horizontal ? LayoutOrientation.Horizontal : LayoutOrientation.Vertical;
        SeparationOverride = horizontal ? 4 : 6;
        MinWidth = horizontal ? 0 : 76;
        HorizontalExpand = horizontal;
        Margin = horizontal ? new Thickness(0, 0, 0, 8) : new Thickness(0, 0, 10, 0);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var changed = _buttons.Count != _tabs.ChildCount;
        for (var i = 0; !changed && i < _buttons.Count; i++)
        {
            var tab = _tabs.GetChild(i);
            changed = _buttons[i].Tab != tab
                      || _buttons[i].Button.Visible != TabContainer.GetTabVisible(tab)
                      || (string?) _buttons[i].Button.ToolTip != TabContainer.GetTabTitle(tab);
        }

        if (changed)
            Rebuild();
    }

    private void Rebuild()
    {
        RemoveAllChildren();
        _buttons.Clear();
        _icons.Clear();

        var shown = 0;
        for (var i = 0; i < _tabs.ChildCount; i++)
        {
            var tab = _tabs.GetChild(i);
            var index = i;
            var title = TabContainer.GetTabTitle(tab) ?? string.Empty;
            var visible = TabContainer.GetTabVisible(tab);
            if (visible)
                shown++;

            var number = new Label
            {
                Text = shown.ToString(),
                HorizontalAlignment = HAlignment.Center,
                StyleClasses = { "LuaMHudPartId" },
            };
            var shortTitle = new Label
            {
                Text = ShortTitle(title),
                Align = Label.AlignMode.Center,
                HorizontalAlignment = HAlignment.Center,
                StyleClasses = { "LuaMHudMono" },
            };

            var button = new ContainerButton
            {
                Visible = visible,
                ToolTip = title,
                MinSize = _horizontal ? new Vector2(0, StripHeight) : new Vector2(70, 56),
                HorizontalExpand = _horizontal,
            };
            if (_horizontal)
            {
                number.Visible = false;
                shortTitle.Text = title.ToUpperInvariant();
                shortTitle.StyleClasses.Clear();
                shortTitle.StyleClasses.Add("LuaMHudTab");
                var row = new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    HorizontalAlignment = HAlignment.Center,
                    VerticalAlignment = VAlignment.Center,
                    SeparationOverride = 6,
                };
                if (IconFor?.Invoke(tab) is { } kind)
                {
                    var icon = new HudIcon { Kind = kind, SetSize = new Vector2(16, 16), Stroke = 1.8f };
                    _icons[button] = icon;
                    row.AddChild(icon);
                }
                row.AddChild(shortTitle);
                row.AddChild(number);
                button.AddChild(row);
            }
            else
            {
                button.AddChild(new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    VerticalAlignment = VAlignment.Center,
                    Children = { number, shortTitle },
                });
            }
            button.OnPressed += _ => _tabs.CurrentTab = index;
            button.OnMouseEntered += _ => UpdateStyles(entering: button);
            button.OnMouseExited += _ => UpdateStyles(leaving: button);

            _buttons.Add((tab, button, number, shortTitle));
            AddChild(button);
        }

        if (Trailer != null)
        {
            Trailer.Orphan();
            AddChild(Trailer);
        }

        UpdateStyles();
    }

    private static string ShortTitle(string title)
    {
        var words = title.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(2)
            .Select(w => w.Length > ShortLength ? w[..(ShortLength - 1)] + "." : w);
        return string.Join('\n', words).ToUpperInvariant();
    }

    private void UpdateStyles(ContainerButton? entering = null, ContainerButton? leaving = null)
    {
        for (var i = 0; i < _buttons.Count; i++)
        {
            var (_, button, number, title) = _buttons[i];
            var active = i == _tabs.CurrentTab;
            var hovered = button == entering || button.IsHovered && button != leaving;

            button.StyleBoxOverride = new StyleBoxLamp(HudPalette.Fg,
                active ? StyleBoxLamp.Lit.On : StyleBoxLamp.Lit.Off,
                active ? HudPalette.Fg : hovered ? HudPalette.BorderBright : null);

            var color = active ? HudPalette.Fg : hovered ? HudPalette.Text : HudPalette.TextDim;
            number.FontColorOverride = color;
            title.FontColorOverride = color;
            if (_icons.TryGetValue(button, out var icon))
                icon.IconColor = color;
        }
    }
}
