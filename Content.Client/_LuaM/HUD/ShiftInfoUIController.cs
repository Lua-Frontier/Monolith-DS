using Content.Client._LuaM.RoundEnd;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared._LuaM.Salvage;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._LuaM.HUD;

/// <summary>
/// Puts <see cref="ShiftInfoPanel"/> on the gameplay screen and feeds it the round end countdown
/// and the expedition of the map the player is on.
/// </summary>
public sealed partial class ShiftInfoUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;

    private ShiftInfoPanel? _panel;
    private bool _active;

    public override void Initialize()
    {
        base.Initialize();

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
        gameplayStateLoad.OnScreenUnload += OnScreenUnload;
    }

    public void OnStateEntered(GameplayState state)
    {
        _active = true;
    }

    public void OnStateExited(GameplayState state)
    {
        _active = false;
        if (_panel != null)
            _panel.Visible = false;
    }

    private void OnScreenLoad()
    {
        if (UIManager.ActiveScreen is not { } screen)
            return;

        _panel = new ShiftInfoPanel { Visible = false };
        screen.AddChild(_panel);
        LayoutContainer.SetAnchorPreset(_panel, LayoutContainer.LayoutPreset.CenterTop);
        LayoutContainer.SetGrowHorizontal(_panel, LayoutContainer.GrowDirection.Both);
        LayoutContainer.SetMarginTop(_panel, 8);
    }

    private void OnScreenUnload()
    {
        _panel?.Orphan();
        _panel = null;
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_active || _panel == null)
            return;

        var curTime = _timing.CurTime;
        var roundEnd = _entMan.System<RoundEndCountdownSystem>().RoundEndTime;

        _panel.UpdateRoundEnd(roundEnd - curTime);
        _panel.UpdateExpedition(GetExpedition(), curTime);
        _panel.Visible = _panel.HasContent;
    }

    /// <summary>
    /// The expedition on the map the local player is on, if any.
    /// </summary>
    private ExpeditionHudComponent? GetExpedition()
    {
        if (!_entMan.TryGetComponent<TransformComponent>(_player.LocalEntity, out var xform))
            return null;

        _entMan.TryGetComponent<ExpeditionHudComponent>(xform.MapUid, out var hud);
        return hud;
    }
}
