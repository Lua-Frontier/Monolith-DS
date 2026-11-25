using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._LuaM.UserInterface.Hud;

public sealed class HudCallout : PanelContainer
{
    private readonly HudShape _shape;
    private readonly Label _lead;
    private readonly Label _text;
    private readonly BoxContainer _content;
    private HudStatus _status;

    public HudCallout()
    {
        HorizontalExpand = true;
        _shape = new HudShape();
        _lead = new Label { StyleClasses = { Content.Client.Stylesheets.LobbyHudStyles.NoteLead }, VAlign = Label.VAlignMode.Center, Visible = false };
        _text = new Label { StyleClasses = { Content.Client.Stylesheets.LobbyHudStyles.Note }, VAlign = Label.VAlignMode.Center, ClipText = true, HorizontalExpand = true, Visible = false };
        _content = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true, SeparationOverride = 6 };
        _content.AddChild(_lead);
        _content.AddChild(_text);

        AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            Children = { _shape, _content },
        });

        Status = HudStatus.Advisory;
    }

    public HudStatus Status
    {
        get => _status;
        set
        {
            _status = value;
            _shape.Status = value;
            var color = HudStatusColors.Of(value);
            _lead.FontColorOverride = HudStatusColors.TextOf(value);
            var box = new StyleBoxFlat
            {
                BackgroundColor = StyleBoxLamp.Mix(color, HudPalette.Panel, 0.06f),
                BorderColor = StyleBoxLamp.Mix(color, HudPalette.Border, 0.45f),
                BorderThickness = new Thickness(1),
            };
            box.SetContentMarginOverride(StyleBox.Margin.Horizontal, 12);
            box.SetContentMarginOverride(StyleBox.Margin.Vertical, 6);
            PanelOverride = box;
        }
    }

    public string? Lead
    {
        get => _lead.Text;
        set
        {
            _lead.Text = value;
            _lead.Visible = !string.IsNullOrEmpty(value);
        }
    }

    public string? Text
    {
        get => _text.Text;
        set
        {
            _text.Text = value;
            _text.Visible = !string.IsNullOrEmpty(value);
        }
    }

    public void SetContent(Control control)
    {
        control.Orphan();
        _text.Visible = false;
        control.HorizontalExpand = true;
        _content.AddChild(control);
    }
}
