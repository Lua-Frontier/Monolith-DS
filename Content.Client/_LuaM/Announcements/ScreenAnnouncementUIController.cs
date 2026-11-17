using System.Numerics;
using Content.Client.Gameplay;
using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Content.Client._LuaM.Hud;
using Content.Client.UserInterface.Screens;
using Content.Shared._LuaM.Announcements;
using Content.Shared._LuaM.CCVar;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client._LuaM.Announcements;

public sealed partial class ScreenAnnouncementUIController : UIController,
    IOnStateExited<GameplayState>, IOnStateExited<LobbyState>
{
    [Dependency] private IConfigurationManager _cfg = default!;
    private readonly ScreenAnnouncementQueue _queue = new();
    private LayoutContainer? _layer;
    private ScreenAnnouncementCard? _card;
    private ScreenAnnouncementEvent? _displayed;
    public bool EditingLayout { get; set; }

    public void Enqueue(ScreenAnnouncementEvent announcement)
    {
        if (_cfg.GetCVar(LuaMCVars.ScreenAnnouncementsEnabled))
            _queue.Enqueue(announcement);
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (EditingLayout)
            return;
        if (!_cfg.GetCVar(LuaMCVars.ScreenAnnouncementsEnabled))
        {
            Clear();
            return;
        }
        // A layout preview hides this layer along with other windows; do not consume its reading time.
        if (_layer is { Visible: false })
            return;
        if (UIManager.ActiveScreen is not InGameScreen and not LobbyGui)
        {
            if (_card != null)
                _card.Visible = false;
            return;
        }
        var reading = false;
        if (_card is { Visible: true } previous && _queue.Elapsed >= previous.RevealDuration)
        {
            var mouse = UIManager.MousePositionScaled;
            reading = previous.Root?.Window?.Id == mouse.Window
                && UIBox2.FromDimensions(previous.GlobalPosition, previous.Size).Contains(mouse.Position);
        }
        _queue.Advance(args.DeltaSeconds, pauseForReading: reading);
        if (_queue.Current is not { } current)
        {
            if (_card != null)
                _card.Visible = false;
            _displayed = null;
            return;
        }
        if (_layer == null)
        {
            _layer = new LayoutContainer { MouseFilter = Control.MouseFilterMode.Ignore };
            UIManager.WindowRoot.AddChild(_layer);
            LayoutContainer.SetAnchorPreset(_layer, LayoutContainer.LayoutPreset.Wide);
            _card = new ScreenAnnouncementCard();
            _layer.AddChild(_card);
        }
        var card = _card!;
        card.MaxWidth = float.PositiveInfinity;
        card.MaxHeight = Math.Max(1, _layer.Size.Y - 24);
        card.PresentationScale = _cfg.GetCVar(LuaMCVars.ScreenAnnouncementScale);
        if (_displayed != current)
        {
            card.SetAnnouncement(current);
            _displayed = current;
        }
        card.Visible = true;
        card.SetElapsed(_queue.Elapsed);
        // The portrait/card appears immediately. Only departure fades; hover cannot undo it.
        card.Modulate = Color.White.WithAlpha(_queue.Opacity);
        var anchor = new Vector2(_cfg.GetCVar(LuaMCVars.ScreenAnnouncementX), _cfg.GetCVar(LuaMCVars.ScreenAnnouncementY));
        LayoutContainer.SetPosition(card, HudLayout.Position(_layer.Size, card.Size, anchor,
            (HudHorizontalAlignment) _cfg.GetCVar(LuaMCVars.ScreenAnnouncementAlignment), allowHorizontalOverflow: true));
    }

    public void Clear()
    {
        _queue.Clear();
        _displayed = null;
        _layer?.Orphan();
        _layer = null;
        _card = null;
    }

    public void OnStateExited(GameplayState state) => Clear();
    public void OnStateExited(LobbyState state) => Clear();
}
