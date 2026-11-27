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

    private int _coordsCaption;
    private float _coordsFullWidth = -1;
    private float _coordsShortWidth;
    private string? _coordsFullText;
    private string? _coordsShortText;
    private int _coordsFittedLength = -1;
    private float _coordsFittedRow = -1;

    private void FitCoordsCaption()
    {
        var avail = GridPositionRow.Size.X;
        if (avail <= 0)
            return;

        // the fit only changes with the row width or the number of characters in the coordinates
        var length = GridPosition.Text?.Length ?? 0;
        if (length == _coordsFittedLength && avail == _coordsFittedRow)
            return;

        _coordsFittedLength = length;
        _coordsFittedRow = avail;

        if (_coordsFullWidth < 0)
        {
            _coordsFullText = GridPositionCaption.Text;
            _coordsShortText = Loc.GetString("shuttle-console-position-short").ToUpperInvariant();
            _coordsFullWidth = CaptionWidth(_coordsFullText);
            _coordsShortWidth = CaptionWidth(_coordsShortText);
            GridPositionCaption.Text = _coordsFullText;
        }

        GridPosition.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));
        var value = GridPosition.DesiredSize.X;
        var gap = GridPositionRow.SeparationOverride ?? 0;
        var mode = value + gap + _coordsFullWidth <= avail ? 0
            : value + gap + _coordsShortWidth <= avail ? 1
            : 2;
        if (mode == _coordsCaption)
            return;

        _coordsCaption = mode;
        GridPositionCaption.Visible = mode != 2;
        GridPositionCaption.Text = mode == 0 ? _coordsFullText : _coordsShortText;
    }

    private float CaptionWidth(string? text)
    {
        GridPositionCaption.Text = text;
        GridPositionCaption.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));
        return GridPositionCaption.DesiredSize.X;
    }

    private static string GunStyle(ShipGunType? type) => ConsoleStyles.GunStyle(type);

    private static HudStatus? GunShape(ShipGunType? type) => ConsoleStyles.GunShape(type);

    private static void AddGunShape(Button button, HudStatus? shape) => ConsoleStyles.AddGunShape(button, shape);
}
