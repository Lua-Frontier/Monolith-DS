using Content.Shared._Mono.FireControl;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.Shuttles.UI;

/// <summary>
/// Reload bar under each weapon button of the shuttle console, the same as in the gunnery console.
/// </summary>
public sealed partial class NavScreen
{
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan MinShownReload = TimeSpan.FromSeconds(0.3);
    private static readonly StyleBoxFlat ReloadReadyStyle = new(Color.FromHex("#3A7D44"));
    private static readonly StyleBoxFlat ReloadChargingStyle = new(Color.FromHex("#D98C1A"));
    private static readonly StyleBoxFlat ReloadBackgroundStyle = new(Color.FromHex("#1C1C1C"));

    private readonly Dictionary<NetEntity, ProgressBar> _reloadBars = new();
    private readonly Dictionary<NetEntity, (TimeSpan Start, TimeSpan End)> _reloads = new();

    /// <summary>
    /// Puts the weapon button in a row with its reload bar.
    /// </summary>
    private Control WithReloadBar(NetEntity weapon, Button button)
    {
        var bar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Value = 1,
            SetHeight = 4,
            Margin = new Thickness(4, 0, 4, 2),
            ForegroundStyleBoxOverride = ReloadReadyStyle,
            BackgroundStyleBoxOverride = ReloadBackgroundStyle,
        };

        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        row.AddChild(button);
        row.AddChild(bar);

        _reloadBars[weapon] = bar;
        return row;
    }

    private void UpdateReloads(FireControllableEntry[]? weapons)
    {
        _reloads.Clear();
        if (weapons == null)
            return;

        foreach (var weapon in weapons)
        {
            if (weapon is { ReloadStart: { } start, ReloadEnd: { } end })
                _reloads[weapon.NetEntity] = (start, end);
        }
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        UpdateHoloCommsButton();
        UpdateRadarMenu();
        UpdateNebulaWarning();

        if (_reloadBars.Count == 0)
            return;

        var now = _timing.CurTime;
        foreach (var (weapon, bar) in _reloadBars)
        {
            float? progress = null;
            if (_reloads.TryGetValue(weapon, out var reload)
                && now < reload.End
                && reload.End - reload.Start >= MinShownReload)
            {
                progress = Math.Clamp((float) ((now - reload.Start) / (reload.End - reload.Start)), 0f, 1f);
            }

            bar.Value = progress ?? 1f;

            var style = progress == null ? ReloadReadyStyle : ReloadChargingStyle;
            if (bar.ForegroundStyleBoxOverride != style)
                bar.ForegroundStyleBoxOverride = style;
        }
    }
}
