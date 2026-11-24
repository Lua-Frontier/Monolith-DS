using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._LuaM.UserInterface.Hud;

public sealed class HudColorSection : BoxContainer
{
    private readonly ContainerButton _header;
    private readonly PanelContainer _swatch;
    private readonly Label _hex;
    private readonly Label _arrow;
    private readonly BoxContainer _body;
    private readonly Func<Color?> _getColor;
    private Color? _shown;

    public HudColorSection(string title, Func<Color?> getColor, params Control[] content)
    {
        _getColor = getColor;
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        SeparationOverride = 4;

        _swatch = new PanelContainer { SetSize = new Vector2(26, 20), VerticalAlignment = VAlignment.Center };
        _hex = new Label { StyleClasses = { "LuaMHudMonoDim" }, VAlign = Label.VAlignMode.Center };
        _arrow = new Label { Text = "▼", StyleClasses = { "LuaMHudMonoDim" }, VAlign = Label.VAlignMode.Center };
        _header = new ContainerButton { HorizontalExpand = true, MinHeight = 32 };
        _header.AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 10,
            Margin = new Thickness(8, 0),
            Children =
            {
                _swatch,
                new Label { Text = title, StyleClasses = { "LuaMHudBody" }, VAlign = Label.VAlignMode.Center, HorizontalExpand = true },
                _hex,
                _arrow,
            },
        });
        _header.MouseFilter = MouseFilterMode.Ignore;
        _arrow.Visible = false;

        _body = new BoxContainer { Orientation = LayoutOrientation.Vertical, Visible = true, Margin = new Thickness(4, 2, 4, 6) };
        foreach (var control in content)
        {
            control.Orphan();
            _body.AddChild(control);
        }

        AddChild(_header);
        AddChild(_body);
        UpdateHeader();
    }

    public void SetOpen(bool open)
    {
        _body.Visible = open;
        UpdateHeader();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        var color = _getColor();
        if (color == _shown)
            return;

        _shown = color;
        _swatch.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = color ?? Color.Transparent,
            BorderColor = Color.Black,
            BorderThickness = new Thickness(1),
        };
        _hex.Text = color is { } c ? c.ToHexNoAlpha().ToUpperInvariant() : string.Empty;
    }

    private void UpdateHeader(bool? hover = null)
    {
        var open = _body.Visible;
        var hovered = hover ?? _header.IsHovered;
        _header.StyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = open ? HudPalette.PanelRaised : hovered ? HudPalette.PanelHover : HudPalette.Panel,
            BorderColor = HudPalette.Border,
            BorderThickness = new Thickness(1),
        };
        _arrow.Text = open ? "▲" : "▼";
    }
}
