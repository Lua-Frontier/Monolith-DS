using Content.Server.RoundEnd;
using Content.Shared._LuaM.RoundEnd;
using Robust.Shared.Enums;
using Robust.Shared.Player;

namespace Content.Server._LuaM.RoundEnd;

/// <summary>
/// Tells clients when the round ends once the round end countdown is called, for the HUD timer.
/// </summary>
public sealed partial class RoundEndCountdownSystem : EntitySystem
{
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private ISharedPlayerManager _player = default!;

    private TimeSpan? _lastSent;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundEndSystemChangedEvent>(OnRoundEndChanged);
        _player.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _player.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnRoundEndChanged(RoundEndSystemChangedEvent ev)
    {
        // Also raised when the call cooldown ends, which doesn't move the countdown.
        if (_roundEnd.ExpectedCountdownEnd == _lastSent)
            return;

        _lastSent = _roundEnd.ExpectedCountdownEnd;
        RaiseNetworkEvent(new RoundEndCountdownEvent(_lastSent), Filter.Broadcast());
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus == SessionStatus.InGame)
            RaiseNetworkEvent(new RoundEndCountdownEvent(_roundEnd.ExpectedCountdownEnd), args.Session);
    }
}
