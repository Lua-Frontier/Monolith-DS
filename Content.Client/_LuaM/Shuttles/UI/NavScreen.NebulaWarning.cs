using Content.Shared._Lua.SpaceHazards;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Client.Shuttles.UI;

/// <summary>
/// Warns the pilot when the shuttle enters the dense part of a dangerous nebula.
/// </summary>
public sealed partial class NavScreen
{
    private static readonly TimeSpan NebulaWarningCheckInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan NebulaWarningDuration = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan NebulaWarningCooldown = TimeSpan.FromSeconds(15);
    private static readonly SoundSpecifier NebulaWarningSound = new SoundPathSpecifier("/Audio/Effects/double_beep.ogg");
    private static readonly Color NebulaWarningColor = Color.FromHex("#FF5A4A");

    private PanelContainer? _nebulaWarning;
    private TimeSpan _nextNebulaWarningCheck;
    private TimeSpan _nextNebulaWarning;
    private TimeSpan _nebulaWarningHideAt;

    private void UpdateNebulaWarning()
    {
        var now = _timing.RealTime;
        if (_nebulaWarning is { Visible: true } && now >= _nebulaWarningHideAt)
            _nebulaWarning.Visible = false;

        if (now < _nextNebulaWarningCheck)
            return;

        _nextNebulaWarningCheck = now + NebulaWarningCheckInterval;

        // -- Сервер сам вешает NebulaPresence на грид в плотной части опасной туманности, клиенту достаточно проверить компонент
        if (now < _nextNebulaWarning ||
            !_entManager.TryGetComponent(_shuttleEntity, out NebulaPresenceComponent? presence) ||
            presence.ActiveWeathers.Count == 0)
        {
            return;
        }

        _nextNebulaWarning = now + NebulaWarningCooldown;
        _nebulaWarningHideAt = now + NebulaWarningDuration;
        GetNebulaWarning().Visible = true;
        _entManager.System<SharedAudioSystem>().PlayGlobal(NebulaWarningSound, Filter.Local(), false, AudioParams.Default.WithVolume(-8f));
    }

    private PanelContainer GetNebulaWarning()
    {
        if (_nebulaWarning != null)
            return _nebulaWarning;

        _nebulaWarning = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.Black.WithAlpha(0.6f),
                BorderColor = NebulaWarningColor,
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = 12,
                ContentMarginRightOverride = 12,
                ContentMarginTopOverride = 4,
                ContentMarginBottomOverride = 4,
            },
            MouseFilter = MouseFilterMode.Ignore,
            Visible = false,
        };
        _nebulaWarning.AddChild(new Label
        {
            Text = Loc.GetString("shuttle-console-nebula-warning"),
            FontColorOverride = NebulaWarningColor,
            HorizontalAlignment = HAlignment.Center,
        });

        if (NavRadar.Parent is LayoutContainer layout)
        {
            layout.AddChild(_nebulaWarning);
            LayoutContainer.SetAnchorPreset(_nebulaWarning, LayoutContainer.LayoutPreset.CenterBottom);
            LayoutContainer.SetGrowHorizontal(_nebulaWarning, LayoutContainer.GrowDirection.Both);
            LayoutContainer.SetGrowVertical(_nebulaWarning, LayoutContainer.GrowDirection.Begin);
            LayoutContainer.SetMarginBottom(_nebulaWarning, -60);
        }

        return _nebulaWarning;
    }
}
