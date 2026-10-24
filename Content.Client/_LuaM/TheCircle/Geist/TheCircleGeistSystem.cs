// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Shared._LuaM.TheCircle.Geist;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._LuaM.TheCircle.Geist;

public sealed class TheCircleGeistSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    private GeistModeMenu? _menu;
    private RichTextLabel? _arrivalMessage;
    private TimeSpan _arrivalMessageStarted;

    private static readonly TimeSpan ArrivalMessageDuration = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ArrivalMessageFadeDuration = TimeSpan.FromSeconds(0.5);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TheCircleGeistComponent, OpenGeistInvisibilityMenuEvent>(OnOpenMenu);
        SubscribeNetworkEvent<GeistStationArrivalEvent>(_ => ShowArrivalMessage());
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_arrivalMessage == null)
            return;

        var elapsed = _timing.CurTime - _arrivalMessageStarted;
        if (elapsed >= ArrivalMessageDuration)
        {
            ClearArrivalMessage();
            return;
        }

        var fadeIn = Math.Clamp((float) (elapsed / ArrivalMessageFadeDuration), 0f, 1f);
        var fadeOut = Math.Clamp((float) ((ArrivalMessageDuration - elapsed) / ArrivalMessageFadeDuration), 0f, 1f);
        var alpha = Math.Min(SmoothStep(fadeIn), SmoothStep(fadeOut));
        _arrivalMessage.ModulateSelfOverride = Color.White.WithAlpha(alpha);
    }

    private void OnOpenMenu(Entity<TheCircleGeistComponent> ent, ref OpenGeistInvisibilityMenuEvent args)
    {
        if (args.Handled || args.Performer != ent.Owner || ent.Comp.ActiveMode != null)
            return;

        args.Handled = true;
        _menu?.Close();
        _menu = new GeistModeMenu();
        var buttons = new List<GeistModeMenuOption>();

        foreach (var option in ent.Comp.Modes)
        {
            var readyAt = option.Mode switch
            {
                GeistInvisibilityMode.Escape => ent.Comp.EscapeReadyAt,
                GeistInvisibilityMode.Phase => ent.Comp.PhaseReadyAt,
                _ => ent.Comp.StationaryReadyAt,
            };
            var remaining = readyAt - _timing.CurTime;
            var tooltip = Loc.GetString(option.Tooltip);
            if (remaining > TimeSpan.Zero)
                tooltip += "\n" + Loc.GetString("geist-invisibility-mode-cooldown", ("seconds", Math.Ceiling(remaining.TotalSeconds)));

            buttons.Add(new GeistModeMenuOption(
                option.Mode,
                tooltip,
                remaining > TimeSpan.Zero,
                mode =>
                {
                    if (readyAt <= _timing.CurTime)
                        SelectMode(ent.Owner, mode);
                }));
        }

        _menu.SetModes(buttons);
        _menu.OpenCentered();
    }

    private void SelectMode(EntityUid geist, GeistInvisibilityMode mode)
    {
        RaiseNetworkEvent(new SelectGeistInvisibilityModeEvent(GetNetEntity(geist), mode));
        _menu?.Close();
    }

    private void ShowArrivalMessage()
    {
        _arrivalMessage?.Orphan();

        var messages = new List<string>();
        for (var number = 1; Loc.TryGetString($"geist-station-arrival-{number}", out var localized); number++)
        {
            messages.Add(localized);
        }

        if (messages.Count == 0)
            return;

        var text = _random.Pick(messages);
        var message = new RichTextLabel
        {
            HorizontalAlignment = Control.HAlignment.Center,
            VerticalAlignment = Control.VAlignment.Center,
            MouseFilter = Control.MouseFilterMode.Ignore,
            ModulateSelfOverride = Color.Transparent,
        };
        message.SetMessage(FormattedMessage.FromMarkup($"[bold]{text}[/bold]"));

        _arrivalMessage = message;
        _arrivalMessageStarted = _timing.CurTime;
        _ui.WindowRoot.AddChild(message);
        LayoutContainer.SetAnchorPreset(message, LayoutContainer.LayoutPreset.Center);
    }

    private static float SmoothStep(float value) => value * value * (3f - 2f * value);

    private void ClearArrivalMessage()
    {
        _arrivalMessage?.Orphan();
        _arrivalMessage = null;
    }

    public override void Shutdown()
    {
        ClearArrivalMessage();
        base.Shutdown();
    }
}

internal sealed record GeistModeMenuOption(
    GeistInvisibilityMode Mode,
    string Tooltip,
    bool Disabled,
    Action<GeistInvisibilityMode> Select);

/// <summary>
/// Compact mode picker compatible with Monolith's older radial-menu API.
/// </summary>
internal sealed class GeistModeMenu : DefaultWindow
{
    private readonly BoxContainer _buttons;

    public GeistModeMenu()
    {
        Title = "Geist";
        MinSize = new Vector2(280, 120);
        _buttons = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
        };
        Contents.AddChild(_buttons);
    }

    public void SetModes(IEnumerable<GeistModeMenuOption> modes)
    {
        _buttons.RemoveAllChildren();
        foreach (var mode in modes)
        {
            var button = new Button
            {
                Text = mode.Tooltip,
                ToolTip = mode.Tooltip,
                Disabled = mode.Disabled,
            };
            button.OnPressed += _ =>
            {
                mode.Select(mode.Mode);
                Close();
            };
            _buttons.AddChild(button);
        }
    }
}
