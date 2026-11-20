using Content.Server._LuaM.Atmos;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Components;
using Robust.Shared.Timing;

namespace Content.Server.Atmos.Monitor.Systems;

// -- Без метки режима две связанные сигнализации, сменившие режим в один тик, бесконечно пересылают его друг другу.
public sealed partial class AirAlarmSystem
{
    [Dependency] private IGameTiming _stampTiming = default!;

    private const string ModeStampTick = "luam_air_alarm_mode_tick";
    private const string ModeStampOrigin = "luam_air_alarm_mode_origin";
    private const string ModeStampSequence = "luam_air_alarm_mode_sequence";

    private void StampLocalMode(EntityUid uid, string origin)
    {
        if (!TryComp<DeviceNetworkComponent>(uid, out var net) || net.Address != origin)
            return;

        var stamp = EnsureComp<AirAlarmModeStampComponent>(uid);
        var tick = _stampTiming.CurTick.Value;
        stamp.Sequence = stamp.Tick == tick && stamp.Origin == origin ? stamp.Sequence + 1 : 0;
        stamp.Tick = tick;
        stamp.Origin = origin;
    }

    private void WriteModeStamp(EntityUid uid, NetworkPayload payload)
    {
        if (!TryComp<AirAlarmModeStampComponent>(uid, out var stamp))
            return;

        payload[ModeStampTick] = stamp.Tick;
        payload[ModeStampOrigin] = stamp.Origin;
        payload[ModeStampSequence] = stamp.Sequence;
    }

    private bool TryAcceptModeStamp(EntityUid uid, NetworkPayload payload)
    {
        if (!payload.TryGetValue(ModeStampTick, out uint tick)
            || !payload.TryGetValue(ModeStampOrigin, out string? origin)
            || !payload.TryGetValue(ModeStampSequence, out uint sequence))
            return false;

        var stamp = EnsureComp<AirAlarmModeStampComponent>(uid);
        var order = tick != stamp.Tick
            ? tick.CompareTo(stamp.Tick)
            : string.CompareOrdinal(origin, stamp.Origin) is var byOrigin and not 0
                ? byOrigin
                : sequence.CompareTo(stamp.Sequence);
        if (order <= 0)
            return false;

        stamp.Tick = tick;
        stamp.Origin = origin;
        stamp.Sequence = sequence;
        return true;
    }
}
