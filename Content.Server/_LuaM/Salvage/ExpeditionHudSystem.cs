using System.Numerics;
using Content.Server.Salvage.Expeditions;
using Content.Server.Salvage.Expeditions.Structure;
using Content.Shared._LuaM.Salvage;
using Content.Shared._NF.CCVar;
using Content.Shared.Cargo.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Server._LuaM.Salvage;

/// <summary>
/// Keeps <see cref="ExpeditionHudComponent"/> of expedition maps up to date. Called by the salvage system when the
/// expedition is generated, when its objectives change and when it is finished early.
/// </summary>
public sealed partial class ExpeditionHudSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    /// <summary>
    /// Fills the HUD info once the expedition map and its objectives are spawned.
    /// </summary>
    public void Setup(EntityUid mapUid)
    {
        if (!TryComp<SalvageExpeditionComponent>(mapUid, out var expedition))
            return;

        var hud = EnsureComp<ExpeditionHudComponent>(mapUid);
        var objectives = GetObjectives(mapUid);

        hud.EndTime = expedition.EndTime;
        hud.MissionType = expedition.MissionParams.MissionType;
        hud.ObjectiveTotal = objectives?.Count ?? 0;
        hud.ObjectiveRemaining = hud.ObjectiveTotal;
        // All objectives of a mission are the same structure or a single megafauna.
        hud.ObjectiveName = objectives is { Count: > 0 } ? Name(objectives[0]) : null;
        hud.Completed = expedition.Completed;
        hud.DungeonDirection = expedition.DungeonLocation != Vector2.Zero ? expedition.DungeonLocation.GetDir() : null;
        hud.Reward = GetReward(expedition);
        Dirty(mapUid, hud);
    }

    public void SetObjectives(EntityUid mapUid, int remaining, bool completed)
    {
        if (!TryComp<ExpeditionHudComponent>(mapUid, out var hud))
            return;

        hud.ObjectiveRemaining = remaining;
        hud.Completed = completed;
        Dirty(mapUid, hud);
    }

    public void SetEndTime(EntityUid mapUid, TimeSpan endTime)
    {
        if (!TryComp<ExpeditionHudComponent>(mapUid, out var hud))
            return;

        hud.EndTime = endTime;
        Dirty(mapUid, hud);
    }

    private List<EntityUid>? GetObjectives(EntityUid mapUid)
    {
        if (TryComp<SalvageStructureExpeditionComponent>(mapUid, out var structure))
            return structure.Structures;

        if (TryComp<SalvageEliminationExpeditionComponent>(mapUid, out var elimination))
            return elimination.Megafauna;

        return null;
    }

    /// <summary>
    /// Expedition rewards are cases of credits, each worth its static price.
    /// </summary>
    private int GetReward(SalvageExpeditionComponent expedition)
    {
        if (!_cfg.GetCVar(NFCCVars.SalvageExpeditionRewardsEnabled))
            return 0;

        var reward = 0.0;
        foreach (var id in expedition.Rewards)
        {
            if (_proto.TryIndex<EntityPrototype>(id, out var proto)
                && proto.TryGetComponent<StaticPriceComponent>(out var price, _factory))
                reward += price.Price;
        }

        return (int) reward;
    }
}
