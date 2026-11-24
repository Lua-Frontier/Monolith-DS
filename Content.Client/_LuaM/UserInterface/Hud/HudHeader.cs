using Robust.Client.UserInterface.Controls;

namespace Content.Client._LuaM.UserInterface.Hud;

public sealed class HudHeader : BoxContainer
{
    private readonly Label _id;
    private readonly Label _title;
    private readonly Label _subtitle;
    private readonly HudShape _statusShape;
    private readonly Label _statusLabel;

    public HudHeader()
    {
        Orientation = LayoutOrientation.Horizontal;
        HorizontalExpand = true;
        SetHeight = StyleBoxHud.WindowHeader;
        SeparationOverride = 8;
        Margin = new Thickness(0, 2, 0, 0);

        _id = new Label { StyleClasses = { Content.Client.Stylesheets.LobbyHudStyles.Mark }, VAlign = Label.VAlignMode.Center, Visible = false };
        _title = new Label { StyleClasses = { Content.Client.Stylesheets.LobbyHudStyles.Heading }, VAlign = Label.VAlignMode.Center, HorizontalExpand = true, ClipText = true };
        _subtitle = new Label { StyleClasses = { Content.Client.Stylesheets.LobbyHudStyles.Mark }, VAlign = Label.VAlignMode.Center, Visible = false };

        _statusShape = new HudShape { Visible = false };
        _statusLabel = new Label { StyleClasses = { Content.Client.Stylesheets.LobbyHudStyles.Mark }, VAlign = Label.VAlignMode.Center, Visible = false };

        AddChild(new HudShape { Status = HudStatus.Advisory, SetSize = new System.Numerics.Vector2(9, 9) });
        AddChild(_title);
        AddChild(_subtitle);
        AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            VerticalAlignment = VAlignment.Center,
            Children = { _statusShape, _statusLabel },
        });
        AddChild(_id);
    }

    public string? Id
    {
        get => _id.Text;
        set
        {
            _id.Text = value;
            _id.Visible = !string.IsNullOrEmpty(value);
        }
    }

    public void SetStatus(HudStatus? status, string? text = null)
    {
        _statusShape.Visible = status != null;
        _statusLabel.Visible = status != null && !string.IsNullOrEmpty(text);
        if (status is not { } st)
            return;

        _statusShape.Status = st;
        _statusLabel.Text = text?.ToUpperInvariant();
        _statusLabel.FontColorOverride = HudStatusColors.TextOf(st);
    }

    public string Title
    {
        get => _title.Text ?? string.Empty;
        set => _title.Text = value.ToUpperInvariant();
    }

    public string? Subtitle
    {
        get => _subtitle.Text;
        set
        {
            _subtitle.Text = value?.ToUpperInvariant();
            _subtitle.Visible = !string.IsNullOrEmpty(value);
        }
    }
}
