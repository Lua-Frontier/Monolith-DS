using System.Numerics;
using Content.Client._LuaM.UserInterface.Hud;
using Content.Client.GameTicking.Managers;
using Content.Client.Stylesheets;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.Shuttles.UI;

/// <summary>
/// Header meta of the console: nominal lamp, sector name and the shift clock.
/// </summary>
public sealed partial class ShuttleConsoleWindow
{
    [Dependency] private IGameTiming _timing = default!;

    private Label? _sectorClock;
    private ClientGameTicker? _ticker;
    private string _sectorName = string.Empty;
    private int _shownSecond = -1;

    private void InitializeSector()
    {
        var title = FindControl<Label>("WindowTitle");
        if (title.Parent is not { } row)
            return;

        _sectorName = Loc.GetString("shuttle-console-sector").ToUpperInvariant();
        _sectorClock = new Label
        {
            VAlign = Label.VAlignMode.Center,
            FontOverride = LobbyHudStyles.MonoFont(IoCManager.Resolve<IResourceCache>(), 10),
            FontColorOverride = HudPalette.TextDim,
        };

        var meta = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            Children =
            {
                new HudShape
                {
                    Status = HudStatus.Nominal,
                    SetSize = new Vector2(9, 9),
                    VerticalAlignment = VAlignment.Center,
                },
                _sectorClock,
            },
        };

        row.AddChild(meta);
        meta.SetPositionInParent(title.GetPositionInParent() + 1);
        UpdateSectorClock();
    }

    private void UpdateSectorClock()
    {
        if (_sectorClock == null)
            return;

        _ticker ??= _entManager.System<ClientGameTicker>();
        var shift = _timing.CurTime - _ticker.RoundStartTimeSpan;
        if (shift < TimeSpan.Zero)
            shift = TimeSpan.Zero;

        var second = (int) shift.TotalSeconds;
        if (second == _shownSecond)
            return;

        _shownSecond = second;
        _sectorClock.Text = $"{_sectorName} · {(int) shift.TotalHours:00}:{shift.Minutes:00}:{shift.Seconds:00}";
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        UpdateSectorClock();
    }
}
