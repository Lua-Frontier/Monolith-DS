using Content.Client._LuaM.UserInterface.Hud;

namespace Content.Client.Lobby.UI;

public sealed partial class CharacterPickerButton
{
    public void SetSlotCode(int slot)
    {
        CodeLabel.Text = $"A{slot + 1:00}";
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();

        if (ChildCount == 0)
            return;

        var pressed = DrawMode == DrawModeEnum.Pressed;
        var hovered = DrawMode == DrawModeEnum.Hover;
        StyleBoxOverride = new StyleBoxHud(
            pressed ? HudPalette.OrangeDark : hovered ? HudPalette.PanelHover : HudPalette.Panel,
            pressed ? HudPalette.Orange : hovered ? HudPalette.BorderBright : HudPalette.Border,
            pressed ? HudPalette.Orange : null)
        {
        };

        CodeLabel.FontColorOverride = pressed ? HudPalette.Orange : HudPalette.Cyan;
        DescriptionLabel.FontColorOverride = pressed ? HudPalette.Orange : HudPalette.Text;
        Ring.SetColor(pressed ? HudPalette.Orange : HudPalette.BorderBright);
    }
}
