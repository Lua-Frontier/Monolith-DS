using Content.Server.Worldgen.Prototypes;
using Content.Shared.GameTicking;
using Robust.Shared.Timing;

namespace Content.Server.Worldgen.Systems.GC;

public sealed partial class GCQueueSystem
{
    [Dependency] private IGameTiming _gcTiming = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _queuedAt = new();

    private void InitializeMinAge()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _queuedAt.Clear());
    }

    private void OnQueued(EntityUid uid, GCQueuePrototype proto)
    {
        if (proto.MinAge > TimeSpan.Zero)
            _queuedAt[uid] = _gcTiming.CurTime;
    }

    private bool AlreadyQueued(EntityUid uid, GCQueuePrototype proto)
    {
        return proto.MinAge > TimeSpan.Zero && _queuedAt.ContainsKey(uid);
    }

    private static bool CanOverflow(Queue<EntityUid> queue, GCQueuePrototype proto)
    {
        return proto.MinAge > TimeSpan.Zero && queue.Count < proto.Depth * 2;
    }

    private bool TooYoung(Queue<EntityUid> queue, GCQueuePrototype proto)
    {
        return proto.MinAge > TimeSpan.Zero &&
               queue.Count <= proto.Depth &&
               queue.TryPeek(out var oldest) &&
               _queuedAt.TryGetValue(oldest, out var at) &&
               _gcTiming.CurTime - at < proto.MinAge;
    }

    private void OnDequeued(EntityUid uid)
    {
        _queuedAt.Remove(uid);
    }
}
