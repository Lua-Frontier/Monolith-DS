using Robust.Shared.Configuration;

namespace Content.Shared._LuaM.Performance;

[CVarDefs]
public sealed class PerformanceCVars
{
    public static readonly CVarDef<bool> DeviceNetGuardEnabled =
        CVarDef.Create("luam.device_net_guard.enabled", true, CVar.SERVERONLY);

    public static readonly CVarDef<float> DeviceNetGuardTickBudgetMs =
        CVarDef.Create("luam.device_net_guard.tick_budget_ms", 8f, CVar.SERVERONLY);

    public static readonly CVarDef<int> DeviceNetGuardMinPacketsPerTick =
        CVarDef.Create("luam.device_net_guard.min_packets_per_tick", 256, CVar.SERVERONLY);

    public static readonly CVarDef<int> DeviceNetGuardMaxBacklog =
        CVarDef.Create("luam.device_net_guard.max_backlog", 200000, CVar.SERVERONLY);

    public static readonly CVarDef<float> SlowTickThresholdMs =
        CVarDef.Create("luam.slow_tick.threshold_ms", 250f, CVar.SERVERONLY);

    public static readonly CVarDef<int> SlowTickTop =
        CVarDef.Create("luam.slow_tick.top", 20, CVar.SERVERONLY);

    public static readonly CVarDef<bool> SlowTickProfile =
        CVarDef.Create("luam.slow_tick.profile", true, CVar.SERVERONLY);

    public static readonly CVarDef<float> ChunkUnloadGrace =
        CVarDef.Create("luam.worldgen.unload_grace", 10f, CVar.SERVERONLY);

    public static readonly CVarDef<float> MapDeletionBudgetMs =
        CVarDef.Create("luam.map_deletion.budget_ms", 4f, CVar.SERVERONLY);

    public static readonly CVarDef<bool> IdleHelmsSkipWorldgen =
        CVarDef.Create("luam.worldgen.idle_helms_skip", true, CVar.SERVERONLY);

    public static readonly CVarDef<float> ThrowSweepSpeed =
        CVarDef.Create("luam.throw_sweep_speed", 6f, CVar.SERVERONLY);

    public static readonly CVarDef<float> ImpactThrowMax =
        CVarDef.Create("luam.impact_throw_max", 12f, CVar.SERVERONLY);

    public static readonly CVarDef<bool> RepairDataToolOnly =
        CVarDef.Create("luam.net.repair_data_tool_only", true, CVar.SERVERONLY);

    public static readonly CVarDef<bool> MicroExplosions =
        CVarDef.Create("luam.battle_load.micro_explosions", true, CVar.SERVERONLY);

    public static readonly CVarDef<float> MicroExplosionMaxTotal =
        CVarDef.Create("luam.battle_load.micro_max_total", 10f, CVar.SERVERONLY);

    public static readonly CVarDef<float> MicroExplosionMaxTile =
        CVarDef.Create("luam.battle_load.micro_max_tile", 2f, CVar.SERVERONLY);

    public static readonly CVarDef<float> ExplosionMergeDistance =
        CVarDef.Create("luam.battle_load.merge_distance", 3f, CVar.SERVERONLY);

    public static readonly CVarDef<float> ExplosionMergeMaxTotal =
        CVarDef.Create("luam.battle_load.merge_max_total", 150f, CVar.SERVERONLY);
}
