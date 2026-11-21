using Content.Shared._LuaM.Performance;
using Content.Shared.GameTicking;
using Robust.Shared.Configuration;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Content.Server._LuaM.Maps;

public sealed partial class GradualMapDeletionSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedMapSystem _map = default!;

    private readonly Dictionary<EntityUid, Queue<EntityUid>> _pending = new();
    private readonly List<EntityUid> _maps = new();
    private TimeSpan _budget;

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(_cfg, PerformanceCVars.MapDeletionBudgetMs, v => _budget = TimeSpan.FromMilliseconds(v), true);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _pending.Clear());
    }

    public bool IsDeleting(EntityUid map)
    {
        return _pending.ContainsKey(map);
    }

    public void QueueDelete(EntityUid map)
    {
        if (TerminatingOrDeleted(map) || _pending.ContainsKey(map))
            return;

        if (_budget <= TimeSpan.Zero)
        {
            QueueDel(map);
            return;
        }

        _map.SetPaused(map, true);

        var children = new Queue<EntityUid>();
        var enumerator = Transform(map).ChildEnumerator;
        while (enumerator.MoveNext(out var child))
        {
            children.Enqueue(child);
        }

        _pending.Add(map, children);
    }

    public override void Update(float frameTime)
    {
        if (_pending.Count == 0)
            return;

        var started = Stopwatch.GetTimestamp();
        _maps.Clear();
        _maps.AddRange(_pending.Keys);

        foreach (var map in _maps)
        {
            if (!_pending.TryGetValue(map, out var children))
                continue;

            if (TerminatingOrDeleted(map))
            {
                _pending.Remove(map);
                continue;
            }

            while (children.TryDequeue(out var child))
            {
                if (!TerminatingOrDeleted(child))
                    Del(child);

                if (Stopwatch.GetElapsedTime(started) >= _budget)
                    break;
            }

            if (children.Count == 0)
            {
                Del(map);
                _pending.Remove(map);
            }

            if (Stopwatch.GetElapsedTime(started) >= _budget)
                break;
        }
    }
}
