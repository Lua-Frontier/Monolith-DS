using Stopwatch = System.Diagnostics.Stopwatch;
using System.Runtime.InteropServices;
using System.Text;
using Content.Shared._LuaM.Performance;
using Robust.Server.Player;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Profiling;
using Robust.Shared.Timing;

namespace Content.Server._LuaM.Performance;

/// <summary>
/// Logs a breakdown of every server tick slower than <see cref="PerformanceCVars.SlowTickThresholdMs"/>:
/// where the time went (systems, game state, input), GC pauses and the slowest profiler entries of that tick.
/// </summary>
public sealed partial class SlowTickReportSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private ProfManager _prof = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;

    private const int MinBufferSize = 65536;

    private readonly Dictionary<int, Entry> _totals = new();
    private readonly List<KeyValuePair<int, Entry>> _sorted = new();
    private readonly StringBuilder _sb = new();
    private readonly Dictionary<EntityUid, int> _awakeByParent = new();
    private readonly List<KeyValuePair<EntityUid, int>> _awakeSorted = new();

    private double _thresholdSeconds;
    private int _top;
    private bool _profile;

    private bool _started;
    private long _tickStart;
    private long _tickEnd;
    private long _frameStart;
    private long _logStart;
    private TimeSpan _pauseStart;
    private int _gen0;
    private int _gen1;
    private int _gen2;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, PerformanceCVars.SlowTickThresholdMs, v =>
        {
            _thresholdSeconds = v / 1000.0;
            UpdateProfiler();
        }, true);
        Subs.CVar(_cfg, PerformanceCVars.SlowTickTop, v => _top = v, true);
        Subs.CVar(_cfg, PerformanceCVars.SlowTickProfile, v =>
        {
            _profile = v;
            UpdateProfiler();
        }, true);
    }

    private void UpdateProfiler()
    {
        if (!_profile || _thresholdSeconds <= 0)
            return;

        if (_cfg.GetCVar(CVars.ProfBufferSize) < MinBufferSize)
            _cfg.SetCVar(CVars.ProfBufferSize, MinBufferSize);

        if (!_prof.IsEnabled)
            _cfg.SetCVar(CVars.ProfEnabled, true);
    }

    public void PreEngine()
    {
        if (_thresholdSeconds <= 0)
        {
            _started = false;
            return;
        }

        var now = Stopwatch.GetTimestamp();
        if (_started)
        {
            var total = Stopwatch.GetElapsedTime(_tickStart, now);
            if (total.TotalSeconds >= _thresholdSeconds)
                Report(now, total);
        }

        _started = true;
        _tickStart = now;
        _tickEnd = now;
        _frameStart = 0;
        _logStart = _prof.Buffer.LogWriteOffset;
        _pauseStart = GC.GetTotalPauseDuration();
        _gen0 = GC.CollectionCount(0);
        _gen1 = GC.CollectionCount(1);
        _gen2 = GC.CollectionCount(2);
    }

    public void PostEngine()
    {
        if (_started)
            _tickEnd = Stopwatch.GetTimestamp();
    }

    public void FramePreEngine()
    {
        if (!_started)
            return;

        if (_timing.Paused)
        {
            _started = false;
            return;
        }

        if (_frameStart == 0)
            _frameStart = Stopwatch.GetTimestamp();
    }

    private void Report(long now, TimeSpan total)
    {
        var systems = Stopwatch.GetElapsedTime(_tickStart, _tickEnd);
        TimeSpan state;
        TimeSpan rest;
        if (_frameStart > _tickEnd)
        {
            state = Stopwatch.GetElapsedTime(_tickEnd, _frameStart);
            rest = Stopwatch.GetElapsedTime(_frameStart, now);
        }
        else
        {
            state = Stopwatch.GetElapsedTime(_tickEnd, now);
            rest = TimeSpan.Zero;
        }

        var pause = GC.GetTotalPauseDuration() - _pauseStart;

        _sb.Clear();
        _sb.Append($"Slow tick {_timing.CurTick.Value - 1}: {total.TotalMilliseconds:F0} ms");
        _sb.Append($" (tick {systems.TotalMilliseconds:F0}, after tick {state.TotalMilliseconds:F0}, next frame {rest.TotalMilliseconds:F0})");
        _sb.Append($", GC pause {pause.TotalMilliseconds:F0} ms");
        _sb.Append($" gen0/1/2 +{GC.CollectionCount(0) - _gen0}/+{GC.CollectionCount(1) - _gen1}/+{GC.CollectionCount(2) - _gen2}");
        _sb.Append($", heap {GC.GetTotalMemory(false) / (1024 * 1024)} MB");
        _sb.Append($", {EntityManager.EntityCount} entities, {_players.PlayerCount} players");

        AppendPhysics();
        AppendProfile();
        Log.Warning(_sb.ToString());
    }

    private void AppendPhysics()
    {
        _awakeByParent.Clear();
        foreach (var (_, _, xform) in _physics.AwakeBodies)
        {
            var parent = xform.GridUid ?? xform.MapUid ?? EntityUid.Invalid;
            CollectionsMarshal.GetValueRefOrAddDefault(_awakeByParent, parent, out _)++;
        }

        _sb.Append($"\n  awake bodies {_physics.AwakeBodies.Count}");
        _awakeSorted.Clear();
        _awakeSorted.AddRange(_awakeByParent);
        _awakeSorted.Sort((a, b) => b.Value.CompareTo(a.Value));
        for (var i = 0; i < _awakeSorted.Count && i < 3; i++)
        {
            var (parent, count) = _awakeSorted[i];
            _sb.Append($", {count} on {(Exists(parent) ? ToPrettyString(parent) : "nullspace")}");
        }
    }

    private void AppendProfile()
    {
        if (!_prof.IsEnabled || _top <= 0)
            return;

        var buffer = _prof.Buffer;
        var end = buffer.LogWriteOffset;
        var start = Math.Max(_logStart, end - buffer.LogBuffer.LongLength);

        _totals.Clear();
        for (var i = start; i < end; i++)
        {
            ref var log = ref buffer.Log(i);
            ProfValue value;
            int id;
            switch (log.Type)
            {
                case ProfLogType.Value:
                    value = log.Value.Value;
                    id = log.Value.StringId;
                    break;
                case ProfLogType.GroupEnd when log.GroupEnd.StartIndex >= start:
                    value = log.GroupEnd.Value;
                    id = log.GroupEnd.StringId;
                    break;
                default:
                    continue;
            }

            if (value.Type != ProfValueType.TimeAllocSample)
                continue;

            ref var entry = ref CollectionsMarshal.GetValueRefOrAddDefault(_totals, id, out _);
            entry.Time += value.TimeAllocSample.Time;
            entry.Alloc += value.TimeAllocSample.Alloc;
            entry.Count++;
        }

        _sorted.Clear();
        _sorted.AddRange(_totals);
        _sorted.Sort((a, b) => b.Value.Time.CompareTo(a.Value.Time));

        if (start > _logStart)
            _sb.Append($"\n  profiler buffer overflowed, only the last {end - start} entries; raise prof.buffer_size");

        for (var i = 0; i < _sorted.Count && i < _top; i++)
        {
            var (id, entry) = _sorted[i];
            _sb.Append(i == 0 ? "\n  " : " | ");
            _sb.Append($"{entry.Time * 1000:F1} ms  {_prof.GetString(id)}");
            if (entry.Count > 1)
                _sb.Append($" x{entry.Count}");
            if (entry.Alloc > 1024 * 1024)
                _sb.Append($"  alloc {entry.Alloc / (1024 * 1024)} MB");
        }
    }

    private struct Entry
    {
        public double Time;
        public long Alloc;
        public int Count;
    }
}
