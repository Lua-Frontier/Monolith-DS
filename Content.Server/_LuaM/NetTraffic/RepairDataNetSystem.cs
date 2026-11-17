using Content.Shared._Mono.ShipRepair.Components;
using Content.Shared._LuaM.Performance;
using Robust.Shared.Configuration;
using Robust.Shared.GameStates;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._LuaM.NetTraffic;

// -- Снимок ремонта (каждый тайл и фикстура грида) уходит только тем, кто держит ремонтный инструмент.
public sealed partial class RepairDataNetSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(2);

    private bool _toolOnly;

    private readonly Dictionary<ICommonSession, TimeSpan> _putAway = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShipRepairDataComponent, ComponentGetStateAttemptEvent>(OnGetStateAttempt);
        SubscribeLocalEvent<RepairDataEyeComponent, ComponentStartup>(OnToolUserStartup);
        SubscribeLocalEvent<RepairDataEyeComponent, ComponentShutdown>(OnToolUserShutdown);
        Subs.CVar(_cfg, PerformanceCVars.RepairDataToolOnly, OnToolOnlyChanged, true);
    }

    private void OnGetStateAttempt(Entity<ShipRepairDataComponent> ent, ref ComponentGetStateAttemptEvent args)
    {
        if (!_toolOnly || args.Cancelled || args.Player is not { } player)
            return;

        if (!HasComp<RepairDataEyeComponent>(player.AttachedEntity) && !InGrace(player))
            args.Cancelled = true;
    }

    private void OnToolUserStartup(Entity<RepairDataEyeComponent> ent, ref ComponentStartup args)
    {
        if (!_toolOnly || !TryComp<ActorComponent>(ent, out var actor))
            return;

        var inGrace = InGrace(actor.PlayerSession);
        _putAway.Remove(actor.PlayerSession);
        if (!inGrace)
            ResendAll();
    }

    private void OnToolUserShutdown(Entity<RepairDataEyeComponent> ent, ref ComponentShutdown args)
    {
        if (!_toolOnly || !TryComp<ActorComponent>(ent, out var actor))
            return;

        var now = _timing.CurTime;
        foreach (var (session, at) in _putAway)
        {
            if (now - at >= Grace)
                _putAway.Remove(session);
        }

        _putAway[actor.PlayerSession] = now;
    }

    private bool InGrace(ICommonSession session)
    {
        return _putAway.TryGetValue(session, out var at) && _timing.CurTime - at < Grace;
    }

    private void OnToolOnlyChanged(bool value)
    {
        _toolOnly = value;
        _putAway.Clear();
        if (!value)
            ResendAll();
    }

    private void ResendAll()
    {
        var query = EntityQueryEnumerator<ShipRepairDataComponent>();
        while (query.MoveNext(out var uid, out var data))
        {
            Dirty(uid, data);
        }
    }
}
