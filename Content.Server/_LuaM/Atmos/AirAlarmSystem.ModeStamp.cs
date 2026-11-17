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

    private void StampLocalMode(EntityUid uid, string origin)
    {
        if (!TryComp<DeviceNetworkComponent>(uid, out var net) || net.Address != origin)
            return;

        var stamp = EnsureComp<AirAlarmModeStampComponent>(uid);
        stamp.Tick = _stampTiming.CurTick.Value;
        stamp.Origin = origin;
    }

    private void WriteModeStamp(EntityUid uid, NetworkPayload payload)
    {
        if (!TryComp<AirAlarmModeStampComponent>(uid, out var stamp))
            return;

        payload[ModeStampTick] = stamp.Tick;
        payload[ModeStampOrigin] = stamp.Origin;
    }

    private bool TryAcceptModeStamp(EntityUid uid, NetworkPayload payload)
    {
        if (!payload.TryGetValue(ModeStampTick, out uint tick) || !payload.TryGetValue(ModeStampOrigin, out string? origin))
            return false;

        var stamp = EnsureComp<AirAlarmModeStampComponent>(uid);
        if (tick < stamp.Tick || tick == stamp.Tick && string.CompareOrdinal(origin, stamp.Origin) <= 0)
            return false;

        stamp.Tick = tick;
        stamp.Origin = origin;
        return true;
    }
}
