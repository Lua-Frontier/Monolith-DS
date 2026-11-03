using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._LuaM.SectorAnomaly;

public abstract partial class SectorAnomalyRuleSystem<T> : StationEventSystem<T> where T : SectorAnomalyRuleComponent
{
    [Dependency] private SharedPopupSystem _popup = default!;

    protected const float EffectInterval = 10f;

    protected override void Added(EntityUid uid, T component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        if (IsAnotherAnomalyAdded(uid))
        {
            component.Blocked = true;
            Sawmill.Info($"{ToPrettyString(uid)} blocked by another sector anomaly");
            GameTicker.EndGameRule(uid, gameRule);
            return;
        }

        base.Added(uid, component, gameRule, args);
    }

    protected override void Started(EntityUid uid, T component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!MapSystem.TryGetMap(GameTicker.DefaultMap, out var map))
            return;

        component.Map = map;
        component.EffectTimer = EffectInterval;
        AnomalyStarted(component, map.Value);
    }

    protected override void Ended(EntityUid uid, T component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        if (component.Blocked)
            return;

        base.Ended(uid, component, gameRule, args);

        if (component.Map is not { } map || TerminatingOrDeleted(map))
            return;

        AnomalyEnded(component, map);
    }

    protected override void ActiveTick(EntityUid uid, T component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (component.Map is not { } map)
            return;

        component.EffectTimer -= frameTime;
        if (component.EffectTimer > 0f)
            return;

        component.EffectTimer += EffectInterval;
        AnomalyTick(component, map);

        if (component.Messages.Count == 0)
            return;

        var chance = component.MessageChancePerSecond * EffectInterval;
        var players = EntityQueryEnumerator<ActorComponent, MobStateComponent, TransformComponent>();
        while (players.MoveNext(out var player, out _, out var mobState, out var xform))
        {
            if (xform.MapUid == map && mobState.CurrentState == MobState.Alive && RobustRandom.Prob(chance))
                _popup.PopupEntity(Loc.GetString(RobustRandom.Pick(component.Messages)), player, player);
        }
    }

    protected abstract void AnomalyStarted(T component, EntityUid map);

    protected abstract void AnomalyEnded(T component, EntityUid map);

    protected virtual void AnomalyTick(T component, EntityUid map)
    {
    }

    private bool IsAnotherAnomalyAdded(EntityUid uid)
    {
        return IsAdded<SectorDarknessRuleComponent>(uid) || IsAdded<SectorBrightnessRuleComponent>(uid);
    }

    private bool IsAdded<TRule>(EntityUid uid) where TRule : IComponent
    {
        var query = EntityQueryEnumerator<TRule, GameRuleComponent>();
        while (query.MoveNext(out var rule, out _, out var gameRule))
        {
            if (rule != uid && GameTicker.IsGameRuleAdded(rule, gameRule))
                return true;
        }

        return false;
    }
}
