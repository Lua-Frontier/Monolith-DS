using Robust.Client.GameStates;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Client._LuaM.StateRecovery;

public sealed partial class StateRecoverySystem : EntitySystem
{
    [Dependency] private IClientGameStateManager _gameState = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DetachLookupSystem _detachLookup = default!;

    private static readonly TimeSpan Window = TimeSpan.FromSeconds(2);
    private const int FailuresPerWindow = 10;
    private static readonly TimeSpan MinCooldown = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxCooldown = TimeSpan.FromMinutes(5);

    [ViewVariables]
    public int Requests { get; private set; }

    private bool _stepOpen;
    private bool _purgeDeferred;
    private int _failures;
    private TimeSpan _windowStart;
    private TimeSpan _cooldown = MinCooldown;
    private TimeSpan _nextRequest;
    private TimeSpan _lastRequest;
    private TimeSpan _lastGoodStep;
    private TimeSpan _nextStallPurge;

    private static readonly TimeSpan StallTime = TimeSpan.FromSeconds(2);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PhysicsUpdateBeforeSolveEvent>(OnBeforeSolve);
        SubscribeLocalEvent<PhysicsUpdateAfterSolveEvent>(OnAfterSolve);
    }

    private void OnBeforeSolve(ref PhysicsUpdateBeforeSolveEvent ev)
    {
        if (_stepOpen)
            _failures++;

        _stepOpen = true;
    }

    private void OnAfterSolve(ref PhysicsUpdateAfterSolveEvent ev)
    {
        _stepOpen = false;
        _lastGoodStep = _timing.RealTime;
    }

    public override void FrameUpdate(float frameTime)
    {
        var now = _timing.RealTime;
        if (now - _lastGoodStep < StallTime || now < _nextStallPurge)
            return;

        _nextStallPurge = now + StallTime;
        var purged = _detachLookup.PurgeDeleted();
        if (purged > 0)
            Log.Warning($"No physics step has finished in {StallTime.TotalSeconds:0}s; removed {purged} deleted entities from lookup trees.");
    }

    public override void Update(float frameTime)
    {
        var now = _timing.RealTime;
        if (now - _windowStart < Window)
            return;

        var failures = _failures;
        _failures = 0;
        _windowStart = now;

        if (failures < FailuresPerWindow)
        {
            _purgeDeferred = false;
            if (now - _lastRequest > MaxCooldown)
                _cooldown = MinCooldown;
            return;
        }

        var purged = _detachLookup.PurgeDeleted();
        if (purged > 0)
            Log.Warning($"{failures} physics steps failed in {Window.TotalSeconds:0}s; removed {purged} deleted entities from lookup trees.");

        if (purged > 0 && !_purgeDeferred)
        {
            _purgeDeferred = true;
            return;
        }

        _purgeDeferred = false;

        if (now < _nextRequest)
            return;

        Log.Warning($"{failures} physics steps failed in {Window.TotalSeconds:0}s; requesting a full game state.");
        _gameState.RequestFullState();
        Requests++;
        _lastRequest = now;
        _nextRequest = now + _cooldown;
        _cooldown = TimeSpan.FromTicks(Math.Min(_cooldown.Ticks * 2, MaxCooldown.Ticks));
    }
}
