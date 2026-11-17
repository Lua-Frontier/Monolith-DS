using Content.Server.Shuttles.Components;
using Content.Server.Worldgen.Components;
using Content.Shared._LuaM.Performance;
using Content.Shared.Ghost;
using Robust.Shared.Configuration;
using Robust.Shared.Physics;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Worldgen.Systems;

public sealed partial class WorldControllerSystem
{
    private bool _idleHelmsSkip;
    private readonly HashSet<EntityUid> _activeHelmGrids = new();
    private readonly Stack<EntityUid> _pendingHelmGrids = new();
    private TimeSpan _nextChunkCountLog;
    private int _skippedHelms;

    private void InitializeIdleHelms()
    {
        _perfCfg.OnValueChanged(PerformanceCVars.IdleHelmsSkipWorldgen, v => _idleHelmsSkip = v, true);
    }

    private void CollectActiveHelmGrids()
    {
        _activeHelmGrids.Clear();
        _skippedHelms = 0;
        if (!_idleHelmsSkip)
            return;

        _pendingHelmGrids.Clear();
        var actors = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (actors.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid is { } grid && !HasComp<GhostComponent>(uid) && _activeHelmGrids.Add(grid))
                _pendingHelmGrids.Push(grid);
        }

        var consoles = EntityQueryEnumerator<ShuttleConsoleComponent, TransformComponent>();
        while (consoles.MoveNext(out _, out var console, out var xform))
        {
            if (console.SubscribedPilots.Count > 0 && xform.GridUid is { } grid && _activeHelmGrids.Add(grid))
                _pendingHelmGrids.Push(grid);
        }

        while (_pendingHelmGrids.TryPop(out var current))
        {
            if (!TryComp<JointComponent>(current, out var joints))
                continue;

            foreach (var joint in joints.GetJoints.Values)
            {
                var other = joint.BodyAUid == current ? joint.BodyBUid : joint.BodyAUid;
                if (Transform(other).GridUid is { } otherGrid && _activeHelmGrids.Add(otherGrid))
                    _pendingHelmGrids.Push(otherGrid);
            }
        }
    }

    private bool IsIdleHelm(EntityUid uid, TransformComponent xform)
    {
        if (!_idleHelmsSkip || !HasComp<ShuttleConsoleComponent>(uid))
            return false;

        if (xform.GridUid is not { } grid || _activeHelmGrids.Contains(grid))
            return false;

        _skippedHelms++;
        return true;
    }

    private void LogChunkCount()
    {
        var now = _gameTiming.CurTime;
        if (now < _nextChunkCountLog)
            return;

        _nextChunkCountLog = now + TimeSpan.FromSeconds(10);
        _sawmill.Debug($"{EntityManager.Count<LoadedChunkComponent>()} loaded chunks, {_skippedHelms} idle helms skipped.");
    }
}
