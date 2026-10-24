using Content.Server.Administration.Systems;
using Content.Server.DeadSpace.Necromorphs.Unitology;
using Content.Server.GameTicking.Rules;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server._Mono.Radar;
using Content.Shared.DeadSpace.Necromorphs.Unitology.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Humanoid;
using Content.Shared.Actions;
using Content.Shared._LuaM.TheCircle.FrontierWar;
using Content.Shared.Mobs.Systems;
using Content.Shared.Station;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server._LuaM.TheCircle.FrontierWar;

public sealed partial class CircleFrontierWarSystem : GameRuleSystem<CircleFrontierWarComponent>
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private RejuvenateSystem _rejuvenate = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedStationSpawningSystem _stationSpawning = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CircleFrontierWarParticipantComponent, MobStateChangedEvent>(OnParticipantMobStateChanged);
        SubscribeLocalEvent<CircleFrontierWarGateComponent, GetVerbsEvent<AlternativeVerb>>(OnGateVerbs);
        SubscribeLocalEvent<CircleFrontierWarGateComponent, InteractHandEvent>(OnGateInteract);
        SubscribeLocalEvent<CircleFrontierWarPendingRespawnComponent, GettingInteractedWithAttemptEvent>(OnPendingRespawnInteracted);
        SubscribeLocalEvent<CircleFrontierWarPendingRespawnComponent, PullAttemptEvent>(OnPendingRespawnPulled);
        SubscribeLocalEvent<CircleFrontierWarCollapsibleRockComponent, EntityTerminatingEvent>(OnCollapsibleRockTerminating);
        SubscribeLocalEvent<CircleFrontierWarObeliskComponent, EntityTerminatingEvent>(OnObeliskTerminating);
        SubscribeLocalEvent<CircleFrontierWarParticipantComponent, CircleFrontierWarAcceptDreadnoughtEvent>(OnAcceptDreadnought);
        SubscribeLocalEvent<CircleFrontierWarParticipantComponent, CircleFrontierWarToggleNavigationEvent>(OnToggleNavigation);
    }

    protected override void Started(EntityUid uid,
        CircleFrontierWarComponent component,
        GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        component.Stage = CircleFrontierWarStage.WaitingForCircle;
        component.NextStageTime = Timing.CurTime + component.CircleArrivalDelay;
        component.CrewReinforcements = component.StartingReinforcements;
        component.CircleReinforcements = component.StartingReinforcements;
        component.NextScoreTick = TimeSpan.MaxValue;
        component.ForcedAwakeningAt = Timing.CurTime + component.ForcedAwakeningDelay;
        component.NextCollapseAt = TimeSpan.MaxValue;
        Dirty(uid, component);
    }

    protected override void ActiveTick(EntityUid uid,
        CircleFrontierWarComponent component,
        GameRuleComponent gameRule,
        float frameTime)
    {
        component.CaptureAccumulator += frameTime;
        ProcessRespawns(component);
        ProcessForcedAwakening(component);
        ProcessDreadnoughts(component);
        if (component.Stage is CircleFrontierWarStage.BattlefieldHidden or CircleFrontierWarStage.BattlefieldRevealed &&
            component.CaptureAccumulator >= 1f)
        {
            if (component.Stage == CircleFrontierWarStage.BattlefieldRevealed)
                UpdateCapturePoints(TimeSpan.FromSeconds(component.CaptureAccumulator));
            EnforceBattlefieldBoundary(component);
            component.CaptureAccumulator = 0f;
        }

        if (component.Stage == CircleFrontierWarStage.BattlefieldRevealed && Timing.CurTime >= component.NextScoreTick)
        {
            DrainReinforcements(component);
            TryOfferDreadnought(component, CircleFrontierWarSide.Crew);
            TryOfferDreadnought(component, CircleFrontierWarSide.Circle);
            component.NextScoreTick = Timing.CurTime + component.CapturePointDrainInterval;
            SynchronizeScores(component);
            Dirty(uid, component);
        }

        if (component.Stage == CircleFrontierWarStage.BattlefieldRevealed && Timing.CurTime >= component.NextCollapseAt)
        {
            CollapseDugPaths(component);
            component.NextCollapseAt = Timing.CurTime + component.CollapseInterval;
        }

        if (component.Stage == CircleFrontierWarStage.BattlefieldRevealed && CheckVictory(component, out var winningSide))
        {
            ChatManager.DispatchServerAnnouncement(Loc.GetString(winningSide == CircleFrontierWarSide.Crew
                ? "circle-frontier-war-crew-victory"
                : "circle-frontier-war-circle-victory"));
            component.Stage = CircleFrontierWarStage.Finished;
            GameTicker.EndGameRule(uid, gameRule);
            return;
        }

        if (Timing.CurTime < component.NextStageTime)
            return;

        switch (component.Stage)
        {
            case CircleFrontierWarStage.WaitingForCircle:
                SpawnCircleTeam(component);
                ChatManager.DispatchServerAnnouncement(Loc.GetString("circle-frontier-war-circle-arrival"));
                component.Stage = CircleFrontierWarStage.Infiltration;
                component.NextStageTime = Timing.CurTime + component.BattlefieldRevealDelay;
                break;
            case CircleFrontierWarStage.Infiltration:
                if (!TryBeginBattlefieldGeneration(uid, component))
                {
                    component.NextStageTime = Timing.CurTime + TimeSpan.FromMinutes(1);
                    break;
                }

                component.Stage = CircleFrontierWarStage.BattlefieldHidden;
                component.NextStageTime = TimeSpan.MaxValue;
                break;
            case CircleFrontierWarStage.BattlefieldHidden:
                ChatManager.DispatchServerAnnouncement(Loc.GetString("circle-frontier-war-obelisk-awakened"));
                _audio.PlayGlobal(component.BattlefieldRevealSound, Filter.Broadcast(), true);
                component.Stage = CircleFrontierWarStage.BattlefieldRevealed;
                component.NextStageTime = TimeSpan.MaxValue;
                component.NextScoreTick = Timing.CurTime + component.CapturePointDrainInterval;
                component.NextCollapseAt = Timing.CurTime + component.CollapseInterval;
                RevealParticipantObjectives();
                RevealBattlefieldOnRadar();
                break;
            case CircleFrontierWarStage.BattlefieldRevealed:
            case CircleFrontierWarStage.Finished:
                return;
        }

        Dirty(uid, component);
    }

    private void ProcessForcedAwakening(CircleFrontierWarComponent rule)
    {
        if (rule.ForcedAwakeningTriggered || Timing.CurTime < rule.ForcedAwakeningAt)
            return;

        rule.ForcedAwakeningTriggered = true;
        var candidates = new List<EntityUid>();
        foreach (var session in _players.NetworkedSessions)
        {
            if (session.Status != SessionStatus.InGame || session.AttachedEntity is not { } player ||
                !HasComp<HumanoidAppearanceComponent>(player) || IsCircle(player) ||
                !TryComp<MobStateComponent>(player, out var mob) || mob.CurrentState != MobState.Alive ||
                !_mind.TryGetMind(player, out _, out _))
                continue;

            candidates.Add(player);
        }

        RobustRandom.Shuffle(candidates);
        var amount = (int) Math.Ceiling(candidates.Count * 0.5);
        for (var i = 0; i < amount; i++)
            EnsureComp<UnitologyEnslavedComponent>(candidates[i]);

        ChatManager.DispatchServerAnnouncement(Loc.GetString("circle-frontier-war-forced-awakening"));
    }

    private void OnCollapsibleRockTerminating(Entity<CircleFrontierWarCollapsibleRockComponent> ent,
        ref EntityTerminatingEvent args)
    {
        if (TryGetActiveRule(out _, out var rule) && rule.Stage == CircleFrontierWarStage.BattlefieldRevealed)
            rule.DugRockPositions.Add(Transform(ent.Owner).Coordinates);
    }

    private void CollapseDugPaths(CircleFrontierWarComponent rule)
    {
        if (rule.DugRockPositions.Count == 0)
            return;

        var remaining = new List<EntityCoordinates>();
        foreach (var coordinates in rule.DugRockPositions)
        {
            var occupied = false;
            var participants = EntityQueryEnumerator<CircleFrontierWarParticipantComponent, TransformComponent>();
            while (participants.MoveNext(out _, out _, out var transform))
            {
                if (coordinates.TryDistance(EntityManager, transform.Coordinates, out var distance) && distance < 0.75f)
                {
                    occupied = true;
                    break;
                }
            }

            if (occupied)
                remaining.Add(coordinates);
            else
                Spawn(rule.CollapseRock, coordinates);
        }

        rule.DugRockPositions = remaining;
        ChatManager.DispatchServerAnnouncement(Loc.GetString("circle-frontier-war-collapse"));
    }

    private void OnObeliskTerminating(Entity<CircleFrontierWarObeliskComponent> ent, ref EntityTerminatingEvent args)
    {
        if (TryGetActiveRule(out _, out var rule))
            rule.ObeliskDestroyed = true;
    }

    private bool CheckVictory(CircleFrontierWarComponent rule, out CircleFrontierWarSide winningSide)
    {
        var crewAlive = false;
        var circleAlive = false;
        var participants = EntityQueryEnumerator<CircleFrontierWarParticipantComponent, MobStateComponent>();
        while (participants.MoveNext(out _, out var participant, out var mob))
        {
            if (mob.CurrentState != MobState.Alive)
                continue;

            if (participant.Side == CircleFrontierWarSide.Crew)
                crewAlive = true;
            else if (participant.Side == CircleFrontierWarSide.Circle)
                circleAlive = true;
        }

        if (rule.CircleReinforcements == 0 && !circleAlive && rule.ObeliskDestroyed)
        {
            winningSide = CircleFrontierWarSide.Crew;
            return true;
        }

        if (rule.CrewReinforcements == 0 && !crewAlive)
        {
            winningSide = CircleFrontierWarSide.Circle;
            return true;
        }

        winningSide = CircleFrontierWarSide.None;
        return false;
    }

    private void UpdateCapturePoints(TimeSpan elapsed)
    {
        var points = EntityQueryEnumerator<CircleFrontierWarCapturePointComponent, TransformComponent>();
        while (points.MoveNext(out _, out var point, out var pointTransform))
        {
            var crew = 0;
            var circle = 0;
            var participants = EntityQueryEnumerator<CircleFrontierWarParticipantComponent, MobStateComponent, TransformComponent>();
            while (participants.MoveNext(out _, out var participant, out var mobState, out var participantTransform))
            {
                if (mobState.CurrentState != MobState.Alive ||
                    !pointTransform.Coordinates.TryDistance(EntityManager, participantTransform.Coordinates, out var distance) ||
                    distance > point.Radius)
                    continue;

                if (participant.Side == CircleFrontierWarSide.Circle)
                    circle++;
                else if (participant.Side == CircleFrontierWarSide.Crew)
                    crew++;
            }

            var capturing = crew > 0 && circle == 0
                ? CircleFrontierWarSide.Crew
                : circle > 0 && crew == 0
                    ? CircleFrontierWarSide.Circle
                    : CircleFrontierWarSide.None;

            if (capturing == CircleFrontierWarSide.None || capturing == point.ControllingSide)
            {
                point.CapturingSide = CircleFrontierWarSide.None;
                point.Progress = TimeSpan.Zero;
                continue;
            }

            if (point.CapturingSide != capturing)
            {
                point.CapturingSide = capturing;
                point.Progress = TimeSpan.Zero;
            }

            point.Progress += elapsed;
            if (point.Progress < point.CaptureTime)
                continue;

            point.ControllingSide = capturing;
            point.CapturingSide = CircleFrontierWarSide.None;
            point.Progress = TimeSpan.Zero;
            ChatManager.DispatchServerAnnouncement(Loc.GetString("circle-frontier-war-point-captured",
                ("point", point.Id),
                ("side", Loc.GetString(capturing == CircleFrontierWarSide.Circle
                    ? "circle-frontier-war-side-circle"
                    : "circle-frontier-war-side-crew"))));
        }
    }

    private void DrainReinforcements(CircleFrontierWarComponent rule)
    {
        var points = EntityQueryEnumerator<CircleFrontierWarCapturePointComponent>();
        while (points.MoveNext(out _, out var point))
        {
            if (point.ControllingSide == CircleFrontierWarSide.Circle)
                DrainSide(rule, CircleFrontierWarSide.Crew, rule.CapturePointDrain);
            else if (point.ControllingSide == CircleFrontierWarSide.Crew)
                DrainSide(rule, CircleFrontierWarSide.Circle, rule.CapturePointDrain);
        }
    }

    private void DrainSide(CircleFrontierWarComponent rule, CircleFrontierWarSide side, int amount)
    {
        if (rule.ActiveDreadnoughts.ContainsKey(side))
            return;

        if (side == CircleFrontierWarSide.Circle)
            rule.CircleReinforcements = Math.Max(0, rule.CircleReinforcements - amount);
        else
            rule.CrewReinforcements = Math.Max(0, rule.CrewReinforcements - amount);
    }

    private void OnParticipantMobStateChanged(Entity<CircleFrontierWarParticipantComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        var rules = EntityQueryEnumerator<CircleFrontierWarComponent, GameRuleComponent>();
        while (rules.MoveNext(out var ruleUid, out var rule, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(ruleUid, gameRule) || rule.Stage != CircleFrontierWarStage.BattlefieldRevealed)
                continue;

            if (!HasComp<CircleFrontierWarDreadnoughtComponent>(ent.Owner))
                DrainSide(rule, ent.Comp.Side, rule.KillDrain);

            if (!HasComp<CircleFrontierWarPendingRespawnComponent>(ent.Owner))
            {
                EnsureComp<CircleFrontierWarPendingRespawnComponent>(ent.Owner);
                _transform.AnchorEntity(ent.Owner);
                rule.RespawnQueue[ent.Owner] = Timing.CurTime + rule.RespawnDelay;
            }

            Dirty(ruleUid, rule);
            break;
        }
    }

    private void ProcessRespawns(CircleFrontierWarComponent rule)
    {
        if (rule.RespawnQueue.Count == 0)
            return;

        var completed = new List<EntityUid>();
        foreach (var (uid, respawnAt) in rule.RespawnQueue)
        {
            if (Deleted(uid))
            {
                completed.Add(uid);
                continue;
            }

            if (Timing.CurTime < respawnAt ||
                !TryComp<CircleFrontierWarParticipantComponent>(uid, out var participant) ||
                GetReinforcements(rule, participant.Side) <= 0)
                continue;

            var destination = FindSideSpawn(participant.Side);
            if (destination == null)
                continue;

            _rejuvenate.PerformRejuvenate(uid);
            _transform.Unanchor(uid);
            _transform.SetCoordinates(uid, destination.Value);
            RemComp<CircleFrontierWarPendingRespawnComponent>(uid);
            completed.Add(uid);
        }

        foreach (var uid in completed)
            rule.RespawnQueue.Remove(uid);
    }

    private static int GetReinforcements(CircleFrontierWarComponent rule, CircleFrontierWarSide side)
    {
        return side == CircleFrontierWarSide.Circle
            ? rule.CircleReinforcements
            : rule.CrewReinforcements;
    }

    private void OnPendingRespawnInteracted(Entity<CircleFrontierWarPendingRespawnComponent> ent,
        ref GettingInteractedWithAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnPendingRespawnPulled(Entity<CircleFrontierWarPendingRespawnComponent> ent, ref PullAttemptEvent args)
    {
        args.Cancelled = true;
    }

    public void EnrollParticipant(EntityUid uid)
    {
        var participant = EnsureComp<CircleFrontierWarParticipantComponent>(uid);
        participant.Side = HasComp<UnitologyComponent>(uid) || HasComp<UnitologyEnslavedComponent>(uid)
            ? CircleFrontierWarSide.Circle
            : CircleFrontierWarSide.Crew;
        Dirty(uid, participant);
        EnsureComp<CircleFrontierWarNavigationComponent>(uid);
        var navigation = Comp<CircleFrontierWarNavigationComponent>(uid);
        navigation.Side = (byte) participant.Side;
        navigation.ObjectivesVisible = TryGetActiveRule(out _, out var activeRule) &&
                                       activeRule.Stage == CircleFrontierWarStage.BattlefieldRevealed;
        if (activeRule != null)
        {
            navigation.CrewReinforcements = activeRule.CrewReinforcements;
            navigation.CircleReinforcements = activeRule.CircleReinforcements;
        }
        Dirty(uid, navigation);
        _actions.AddAction(uid,
            ref participant.NavigationActionEntity,
            activeRule?.NavigationAction ?? "ActionCircleFrontierWarToggleNavigation");
    }

    private void RevealParticipantObjectives()
    {
        var query = EntityQueryEnumerator<CircleFrontierWarNavigationComponent>();
        while (query.MoveNext(out var uid, out var navigation))
        {
            navigation.ObjectivesVisible = true;
            Dirty(uid, navigation);
        }
    }

    private void RevealBattlefieldOnRadar()
    {
        var query = EntityQueryEnumerator<CircleFrontierWarBattlefieldAnchorComponent, RadarBlipComponent>();
        while (query.MoveNext(out var uid, out _, out var radar))
        {
            radar.Enabled = true;
            Dirty(uid, radar);
        }
    }

    private void SynchronizeScores(CircleFrontierWarComponent rule)
    {
        var query = EntityQueryEnumerator<CircleFrontierWarNavigationComponent>();
        while (query.MoveNext(out var uid, out var navigation))
        {
            navigation.CrewReinforcements = rule.CrewReinforcements;
            navigation.CircleReinforcements = rule.CircleReinforcements;
            Dirty(uid, navigation);
        }
    }

    private void OnToggleNavigation(Entity<CircleFrontierWarParticipantComponent> ent,
        ref CircleFrontierWarToggleNavigationEvent args)
    {
        if (args.Handled || !TryComp<CircleFrontierWarNavigationComponent>(ent.Owner, out var navigation))
            return;

        args.Handled = true;
        navigation.Visible = !navigation.Visible;
        Dirty(ent.Owner, navigation);
    }

    private void TryOfferDreadnought(CircleFrontierWarComponent rule, CircleFrontierWarSide side)
    {
        if (rule.DreadnoughtOffered.Contains(side) || GetReinforcements(rule, side) > rule.StartingReinforcements * rule.DreadnoughtThreshold)
            return;

        var candidates = new List<EntityUid>();
        var query = EntityQueryEnumerator<CircleFrontierWarParticipantComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var participant, out var mob))
        {
            if (participant.Side == side && mob.CurrentState == MobState.Alive)
                candidates.Add(uid);
        }

        if (candidates.Count == 0)
            return;

        rule.DreadnoughtOffered.Add(side);
        var selected = RobustRandom.Pick(candidates);
        var participantComp = Comp<CircleFrontierWarParticipantComponent>(selected);
        _actions.AddAction(selected, ref participantComp.DreadnoughtOfferActionEntity, rule.DreadnoughtOfferAction);
        _popup.PopupEntity(Loc.GetString("circle-frontier-war-dreadnought-offer"), selected, selected, PopupType.LargeCaution);
    }

    private void OnAcceptDreadnought(Entity<CircleFrontierWarParticipantComponent> ent,
        ref CircleFrontierWarAcceptDreadnoughtEvent args)
    {
        if (args.Handled || !TryGetActiveRule(out _, out var rule) ||
            rule.ActiveDreadnoughts.ContainsKey(ent.Comp.Side) ||
            !TryComp<MobStateComponent>(ent.Owner, out var mob) || mob.CurrentState != MobState.Alive)
            return;

        args.Handled = true;
        _actions.RemoveAction(ent.Owner, ent.Comp.DreadnoughtOfferActionEntity);
        ent.Comp.DreadnoughtOfferActionEntity = null;
        _stationSpawning.EquipStartingGear(ent.Owner, rule.DreadnoughtGear);

        var dreadnought = EnsureComp<CircleFrontierWarDreadnoughtComponent>(ent.Owner);
        dreadnought.Side = ent.Comp.Side;
        dreadnought.DiesAt = Timing.CurTime + rule.DreadnoughtLifetime;
        rule.ActiveDreadnoughts[ent.Comp.Side] = ent.Owner;
        rule.DreadnoughtSavedScores[ent.Comp.Side] = GetReinforcements(rule, ent.Comp.Side);
        _popup.PopupEntity(Loc.GetString("circle-frontier-war-dreadnought-accepted"), ent.Owner, ent.Owner, PopupType.Large);
    }

    private void ProcessDreadnoughts(CircleFrontierWarComponent rule)
    {
        var query = EntityQueryEnumerator<CircleFrontierWarDreadnoughtComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var dreadnought, out var mob))
        {
            if (mob.CurrentState != MobState.Dead && Timing.CurTime < dreadnought.DiesAt)
                continue;

            rule.ActiveDreadnoughts.Remove(dreadnought.Side);
            if (rule.DreadnoughtSavedScores.Remove(dreadnought.Side, out var score))
            {
                if (dreadnought.Side == CircleFrontierWarSide.Circle)
                    rule.CircleReinforcements = score;
                else
                    rule.CrewReinforcements = score;
            }

            if (mob.CurrentState != MobState.Dead)
                _mobState.ChangeMobState(uid, MobState.Dead, mob);

            RemCompDeferred<CircleFrontierWarDreadnoughtComponent>(uid);
        }
    }

    private void OnGateVerbs(Entity<CircleFrontierWarGateComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || HasComp<CircleFrontierWarParticipantComponent>(args.User))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("circle-frontier-war-enter-verb"),
            Message = Loc.GetString("circle-frontier-war-enter-warning"),
            ConfirmationPopup = true,
            Act = () => TryEnterBattlefield(user),
        });
    }

    private void OnGateInteract(Entity<CircleFrontierWarGateComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || HasComp<CircleFrontierWarParticipantComponent>(args.User) ||
            !IsCircle(args.User))
            return;

        args.Handled = TryEnterBattlefield(args.User);
    }

    private bool TryEnterBattlefield(EntityUid user)
    {
        if (!TryGetActiveRule(out _, out var rule) ||
            (rule.Stage != CircleFrontierWarStage.BattlefieldRevealed &&
             !(rule.Stage == CircleFrontierWarStage.BattlefieldHidden && IsCircle(user))))
            return false;

        var side = IsCircle(user) ? CircleFrontierWarSide.Circle : CircleFrontierWarSide.Crew;
        var destination = FindSideSpawn(side);

        if (destination == null)
            return false;

        RemoveForbiddenEquipment(user, rule);
        EnrollParticipant(user);
        _transform.SetCoordinates(user, destination.Value);
        return true;
    }

    private void RemoveForbiddenEquipment(EntityUid user, CircleFrontierWarComponent rule)
    {
        if (rule.ForbiddenEquipment.Count == 0)
            return;

        var descendants = new List<EntityUid>();
        CollectDescendants(user, descendants);
        foreach (var item in descendants)
        {
            if (MetaData(item).EntityPrototype?.ID is { } prototype && rule.ForbiddenEquipment.Contains(prototype))
                QueueDel(item);
        }
    }

    private void CollectDescendants(EntityUid parent, List<EntityUid> result)
    {
        var children = Transform(parent).ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            result.Add(child);
            CollectDescendants(child, result);
        }
    }

    private void EnforceBattlefieldBoundary(CircleFrontierWarComponent rule)
    {
        var arenas = EntityQueryEnumerator<CircleFrontierWarBattlefieldAnchorComponent, TransformComponent>();
        if (!arenas.MoveNext(out _, out _, out var arenaTransform))
            return;

        var participants = EntityQueryEnumerator<CircleFrontierWarParticipantComponent, TransformComponent>();
        while (participants.MoveNext(out var uid, out var participant, out var transform))
        {
            if (transform.Coordinates.TryDistance(EntityManager, arenaTransform.Coordinates, out var distance) &&
                distance <= rule.BattlefieldRadius)
                continue;

            var destination = FindSideSpawn(participant.Side);

            if (destination != null)
                _transform.SetCoordinates(uid, destination.Value);
        }
    }

    private EntityCoordinates? FindSideSpawn(CircleFrontierWarSide side)
    {
        if (side == CircleFrontierWarSide.Circle)
        {
            var spawns = EntityQueryEnumerator<CircleFrontierWarCircleSpawnComponent, TransformComponent>();
            if (spawns.MoveNext(out _, out _, out var transform))
                return transform.Coordinates;
        }
        else
        {
            var spawns = EntityQueryEnumerator<CircleFrontierWarCrewSpawnComponent, TransformComponent>();
            if (spawns.MoveNext(out _, out _, out var transform))
                return transform.Coordinates;
        }

        return null;
    }

    private bool TryGetActiveRule(out EntityUid uid, out CircleFrontierWarComponent component)
    {
        var rules = EntityQueryEnumerator<CircleFrontierWarComponent, GameRuleComponent>();
        while (rules.MoveNext(out var ruleUid, out var ruleComponent, out var gameRule))
        {
            if (GameTicker.IsGameRuleActive(ruleUid, gameRule))
            {
                uid = ruleUid;
                component = ruleComponent;
                return true;
            }
        }

        uid = default;
        component = default!;
        return false;
    }

    private bool IsCircle(EntityUid uid)
    {
        return HasComp<UnitologyComponent>(uid) || HasComp<UnitologyEnslavedComponent>(uid);
    }

    private void SpawnCircleTeam(CircleFrontierWarComponent component)
    {
        var anchors = EntityQueryEnumerator<CircleFrontierWarCircleAnchorComponent, TransformComponent>();
        if (!anchors.MoveNext(out _, out _, out var transform))
        {
            Log.Warning("Circle Frontier War could not spawn its team: no CircleFrontierWarCircleAnchor exists.");
            return;
        }

        foreach (var (prototype, count) in component.CircleGhostRoles)
        {
            for (var i = 0; i < count; i++)
            {
                var offset = RobustRandom.NextVector2(0.5f, 3f);
                Spawn(prototype, transform.Coordinates.Offset(offset));
            }
        }
    }

}
