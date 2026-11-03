using Content.Server.GameTicking.Rules;
using Content.Server.StationEvents;
using Content.Shared.GameTicking.Components;
using Robust.Shared.Random;

namespace Content.Server._LuaM.SectorAnomaly;

public sealed partial class SectorAnomalySchedulerSystem : GameRuleSystem<SectorAnomalySchedulerComponent>
{
    [Dependency] private EventManagerSystem _event = default!;

    protected override void Started(EntityUid uid, SectorAnomalySchedulerComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        component.NextAttempt = null;
        if (!RobustRandom.Prob(component.Chance))
            return;

        component.NextAttempt = Timing.CurTime + TimeSpan.FromSeconds(component.StartTime.Next(RobustRandom));
        component.Deadline = Timing.CurTime + TimeSpan.FromSeconds(component.StartTime.Max);
    }

    protected override void ActiveTick(EntityUid uid, SectorAnomalySchedulerComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (component.NextAttempt is not { } next || Timing.CurTime < next || !_event.EventsEnabled)
            return;

        if (TryRunAnomaly(component))
        {
            component.NextAttempt = null;
            return;
        }

        next += component.RetryDelay;
        component.NextAttempt = next <= component.Deadline ? next : null;
    }

    private bool TryRunAnomaly(SectorAnomalySchedulerComponent component)
    {
        if (!_event.TryBuildLimitedEvents(component.Anomalies, _event.AvailableEvents(), out var anomalies) ||
            _event.FindEvent(anomalies) is not { } anomaly)
        {
            return false;
        }

        GameTicker.AddGameRule(anomaly);
        return true;
    }
}
