// SPDX-FileCopyrightText: 2026 LuaMonolith contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.DeadSpace.Necromorphs.Unitology.Components;
using Content.Server.Mind;
using Content.Shared.DeadSpace.Necromorphs.Unitology.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Systems;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.DeadSpace.Necromorphs.Unitology;

public sealed class UnitologyObeliskSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<UnitologyObeliskComponent, ComponentInit>(OnInit);
    }

    private void OnInit(Entity<UnitologyObeliskComponent> ent, ref ComponentInit args)
    {
        ent.Comp.TriggerAt = _timing.CurTime + ent.Comp.Delay;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<UnitologyObeliskComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (component.Triggered || _timing.CurTime < component.TriggerAt)
                continue;

            component.Triggered = true;
            EnslavePlayers();
        }
    }

    private void EnslavePlayers()
    {
        var candidates = new List<EntityUid>();

        foreach (var session in _players.NetworkedSessions)
        {
            if (session.Status != SessionStatus.InGame ||
                session.AttachedEntity is not { } player ||
                !HasComp<HumanoidAppearanceComponent>(player) ||
                HasComp<UnitologyComponent>(player) ||
                HasComp<UnitologyHeadComponent>(player) ||
                HasComp<UnitologyEnslavedComponent>(player) ||
                !_mobState.IsAlive(player) ||
                !_mind.TryGetMind(player, out _, out _))
            {
                continue;
            }

            candidates.Add(player);
        }

        _random.Shuffle(candidates);
        var amount = (int) Math.Ceiling(candidates.Count * 0.5);

        for (var i = 0; i < amount; i++)
            EnsureComp<UnitologyEnslavedComponent>(candidates[i]);
    }
}
