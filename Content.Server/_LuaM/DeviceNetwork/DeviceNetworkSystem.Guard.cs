using System.Linq;
using Content.Shared._LuaM.Performance;
using Content.Shared.DeviceNetwork.Events;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Content.Server.DeviceNetwork.Systems;

public sealed partial class DeviceNetworkSystem
{
    [Dependency] private IConfigurationManager _guardCfg = default!;
    [Dependency] private IGameTiming _guardTiming = default!;

    private const int StormTopSenders = 5;
    private static readonly TimeSpan StormReportInterval = TimeSpan.FromMinutes(1);

    private readonly Stopwatch _guardWatch = new();
    private TimeSpan _nextStormReport;
    private int _lastBacklog;
    private int _quietStorms;
    private long _quietShed;

    public int Backlog => _queueA.Count + _queueB.Count;

    public long ShedPackets { get; private set; }

    private bool GuardedUpdate()
    {
        if (!_guardCfg.GetCVar(PerformanceCVars.DeviceNetGuardEnabled))
            return false;

        var backlog = Backlog;
        var growing = backlog > _lastBacklog;
        _lastBacklog = backlog;

        if (growing && backlog > _guardCfg.GetCVar(PerformanceCVars.DeviceNetGuardMaxBacklog))
        {
            _lastBacklog = 0;
            ShedPackets += backlog;
            ReportStorm(backlog);
            _queueA.Clear();
            _queueB.Clear();
            return true;
        }

        var budget = TimeSpan.FromMilliseconds(_guardCfg.GetCVar(PerformanceCVars.DeviceNetGuardTickBudgetMs));
        var minPackets = Math.Max(1, _guardCfg.GetCVar(PerformanceCVars.DeviceNetGuardMinPacketsPerTick));
        var sent = 0;
        _guardWatch.Restart();

        while (_activeQueue.Count > 0)
        {
            if (sent >= minPackets && _guardWatch.Elapsed >= budget)
                return true;

            SendPacket(_activeQueue.Dequeue());
            sent++;
        }

        SwapQueues();
        return true;
    }

    private void ReportStorm(int backlog)
    {
        var now = _guardTiming.RealTime;
        if (now < _nextStormReport)
        {
            _quietStorms++;
            _quietShed += backlog;
            return;
        }

        _nextStormReport = now + StormReportInterval;

        var counts = new Dictionary<EntityUid, int>();
        CountSenders(_queueA, counts);
        CountSenders(_queueB, counts);

        var top = counts
            .OrderByDescending(pair => pair.Value)
            .Take(StormTopSenders)
            .Select(pair => $"{pair.Value}x {DescribeSender(pair.Key)}");

        var since = _quietStorms > 0
            ? $" {_quietStorms} more storms dropped {_quietShed} packets since the last report."
            : string.Empty;
        _quietStorms = 0;
        _quietShed = 0;

        Log.Error($"Device network packet storm: dropped {backlog} queued packets from {counts.Count} devices.{since} " +
                  $"Top senders: {string.Join("; ", top)}");
    }

    private static void CountSenders(Queue<DeviceNetworkPacketEvent> queue, Dictionary<EntityUid, int> counts)
    {
        foreach (var packet in queue)
        {
            counts[packet.Sender] = counts.GetValueOrDefault(packet.Sender) + 1;
        }
    }

    private string DescribeSender(EntityUid sender)
    {
        if (TerminatingOrDeleted(sender))
            return $"{sender} (deleted)";

        var grid = Transform(sender).GridUid;
        return grid is { } gridUid
            ? $"{ToPrettyString(sender)} on {ToPrettyString(gridUid)}"
            : $"{ToPrettyString(sender)} off-grid";
    }
}
