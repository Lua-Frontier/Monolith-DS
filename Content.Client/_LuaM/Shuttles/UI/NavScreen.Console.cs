using System.Numerics;
using Content.Client._LuaM.UserInterface.Hud;
using Content.Client.Stylesheets;
using Content.Shared._Mono.ShipGuns;
using Content.Shared._NF.Shuttles.Events;
using Content.Shared.Shuttles.Components;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Shuttles.UI;

/// <summary>
/// Console rework: the drive mode switch and the weapon type colors and shapes.
/// </summary>
public sealed partial class NavScreen
{
    private void InitializeConsoleLook()
    {
        ModeSwitch.SetTexts(DampenerOff.Text ?? string.Empty, DampenerOn.Text ?? string.Empty, AnchorOn.Text ?? string.Empty);
        ModeSwitch.OnSelected += mode => SetDampenerMode(mode switch
        {
            0 => InertiaDampeningMode.Off,
            2 => InertiaDampeningMode.Anchor,
            _ => InertiaDampeningMode.Dampen,
        });

        NavDisplayLabel.FontColorOverride = HudPalette.Cream;
        AddGunShape(GunneryBallisticButton, HudStatus.Caution);
        AddGunShape(GunneryEnergyButton, HudStatus.Advisory);
        AddGunShape(GunneryMissileButton, HudStatus.Warning);
    }

    private void SyncModeSwitch()
    {
        ModeSwitch.Mode = NavRadar.DampeningMode switch
        {
            InertiaDampeningMode.Off => 0,
            InertiaDampeningMode.Anchor => 2,
            _ => 1,
        };
        ModeSwitch.AnchorLocked = AnchorOn.Disabled;
    }

    private static string GunStyle(ShipGunType? type) => ConsoleStyles.GunStyle(type);

    private static HudStatus? GunShape(ShipGunType? type) => ConsoleStyles.GunShape(type);

    private static void AddGunShape(Button button, HudStatus? shape) => ConsoleStyles.AddGunShape(button, shape);
}
