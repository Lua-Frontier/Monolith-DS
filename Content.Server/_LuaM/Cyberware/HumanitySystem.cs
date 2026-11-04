using Content.Server.Administration.Logs;
using Content.Server.Mind;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared._LuaM.Cyberware;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Database;
using Content.Shared.IdentityManagement;
using Content.Shared.Humanoid;
using Content.Shared.Implants.Components;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._LuaM.Cyberware;

/// <summary>
/// Counts the humanity spent on cyberware and makes overloaded creatures fall into cyberpsychosis
/// when they take physical damage.
/// </summary>
public sealed partial class HumanitySystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private HTNSystem _htn = default!;
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private NPCSystem _npc = default!;
    [Dependency] private NpcFactionSystem _npcFaction = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly ProtoId<HTNCompoundPrototype> PsychosisRootTask = "SimpleHostileCompound";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanityComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<HumanityComponent, CyberwareInstalledEvent>(OnCyberwareInstalled);
        SubscribeLocalEvent<CyberpsychosisComponent, MobStateChangedEvent>(OnPsychosisMobStateChanged);
        SubscribeLocalEvent<CyberpsychosisComponent, ComponentShutdown>(OnPsychosisShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CyberpsychosisComponent>();
        while (query.MoveNext(out var uid, out var psychosis))
        {
            if (_timing.CurTime >= psychosis.EndTime)
                RemCompDeferred<CyberpsychosisComponent>(uid);
        }
    }

    /// <summary>
    /// Sums the humanity cost of all cyberware in the body: parts, organs and subdermal implants.
    /// </summary>
    public float GetUsedHumanity(EntityUid uid)
    {
        var used = 0f;

        foreach (var part in _body.GetBodyChildren(uid))
        {
            used += GetCost(part.Id);
        }

        foreach (var organ in _body.GetBodyOrgans(uid))
        {
            used += GetCost(organ.Id);
        }

        if (TryComp<ImplantedComponent>(uid, out var implanted) && implanted.ImplantContainer != null)
        {
            foreach (var implant in implanted.ImplantContainer.ContainedEntities)
            {
                used += GetCost(implant);
            }
        }

        return used;
    }

    /// <summary>
    /// Humanity limit of the creature: its base limit, the species bonus, traits and head chips.
    /// </summary>
    public float GetMaxHumanity(Entity<HumanityComponent> ent)
    {
        // No chrome at all, chips don't help either.
        if (HasComp<ChromeIntoleranceComponent>(ent))
            return 0f;

        var max = ent.Comp.MaxHumanity;

        if (TryComp<HumanoidAppearanceComponent>(ent, out var humanoid)
            && ent.Comp.SpeciesBonus.TryGetValue(humanoid.Species, out var speciesBonus))
            max += speciesBonus;

        if (TryComp<ChromeLoyaltyComponent>(ent, out var loyalty))
            max += loyalty.Bonus;

        foreach (var chip in _body.GetBodyOrganEntityComps<HumanityChipComponent>(ent.Owner))
        {
            max += chip.Comp1.Bonus;
        }

        return max;
    }

    private float GetCost(EntityUid uid)
    {
        if (!TryComp<CyberwareHumanityComponent>(uid, out var cyberware))
            return 0f;

        var cost = cyberware.Cost;
        foreach (var upgrade in cyberware.Upgrades.Values)
        {
            cost += upgrade;
        }

        return cost;
    }

    private void OnDamageChanged(Entity<HumanityComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta == null)
            return;

        if (HasComp<CyberpsychosisComponent>(ent) || _timing.CurTime < ent.Comp.NextPsychosis || !_mobState.IsAlive(ent))
            return;

        if (!_proto.TryIndex(ent.Comp.TriggerGroup, out var group)
            || !args.DamageDelta.TryGetDamageInGroup(group, out var physical)
            || physical <= 0)
            return;

        var over = GetUsedHumanity(ent) - GetMaxHumanity(ent);
        if (over <= 0)
            return;

        if (!_random.Prob(Math.Min(1f, over * ent.Comp.ChancePerPoint)))
            return;

        StartPsychosis(ent);
    }

    private void OnCyberwareInstalled(Entity<HumanityComponent> ent, ref CyberwareInstalledEvent args)
    {
        var max = GetMaxHumanity(ent);
        var left = max - GetUsedHumanity(ent);
        if (left < 0)
            _popup.PopupEntity(Loc.GetString("humanity-installed-over", ("left", left), ("max", max)), ent, ent, PopupType.LargeCaution);
        else
            _popup.PopupEntity(Loc.GetString("humanity-installed", ("left", left), ("max", max)), ent, ent, PopupType.Medium);
    }

    private void StartPsychosis(Entity<HumanityComponent> ent)
    {
        var uid = ent.Owner;
        var psychosis = AddComp<CyberpsychosisComponent>(uid);
        psychosis.EndTime = _timing.CurTime + ent.Comp.PsychosisDuration;

        // Popups go out before the player is moved out of the body, so they still see them.
        _popup.PopupEntity(Loc.GetString("cyberpsychosis-start"), uid, uid, PopupType.LargeCaution);
        _popup.PopupEntity(Loc.GetString("cyberpsychosis-start-others", ("user", Identity.Entity(uid, EntityManager))),
            uid,
            Filter.PvsExcept(uid),
            true,
            PopupType.MediumCaution);

        // The player keeps their mind in the body but only watches while the NPC fights.
        if (_mind.TryGetMind(uid, out var mindId, out var mind) && mind.VisitingEntity == null && mind.UserId != null)
        {
            var viewer = Spawn(ent.Comp.ViewerProto, Transform(uid).Coordinates);
            _transform.SetParent(viewer, uid);
            psychosis.Viewer = viewer;
            psychosis.Mind = mindId;
            _mind.Visit(mindId, viewer, mind);
        }

        var faction = EnsureComp<NpcFactionMemberComponent>(uid);
        psychosis.OldFactions.UnionWith(faction.Factions);
        _npcFaction.ClearFactions((uid, faction), false);
        _npcFaction.AddFaction((uid, faction), ent.Comp.PsychosisFaction);

        psychosis.AddedNpc = !EnsureComp<HTNComponent>(uid, out var htn);
        if (!psychosis.AddedNpc)
            psychosis.OldRootTask = htn.RootTask;

        htn.RootTask = new HTNCompoundTask { Task = PsychosisRootTask };
        htn.Blackboard.SetValue(NPCBlackboard.Owner, uid);
        _npc.WakeNPC(uid, htn);
        _htn.Replan(htn);

        _adminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(uid):player} fell into cyberpsychosis");
    }

    private void OnPsychosisMobStateChanged(Entity<CyberpsychosisComponent> ent, ref MobStateChangedEvent args)
    {
        // Going down brings the creature back to its senses.
        if (args.NewMobState is MobState.Critical or MobState.Dead)
            RemCompDeferred<CyberpsychosisComponent>(ent);
    }

    private void OnPsychosisShutdown(Entity<CyberpsychosisComponent> ent, ref ComponentShutdown args)
    {
        var uid = ent.Owner;

        if (!TerminatingOrDeleted(uid))
        {
            if (TryComp<HTNComponent>(uid, out var htn))
            {
                if (ent.Comp.AddedNpc)
                {
                    RemComp<HTNComponent>(uid);
                }
                else if (ent.Comp.OldRootTask != null)
                {
                    htn.RootTask = ent.Comp.OldRootTask;
                    _htn.Replan(htn);
                }
            }

            if (TryComp<NpcFactionMemberComponent>(uid, out var faction))
            {
                _npcFaction.ClearFactions((uid, faction), false);
                _npcFaction.AddFactions((uid, faction), ent.Comp.OldFactions);
            }

            if (TryComp<HumanityComponent>(uid, out var humanity))
                humanity.NextPsychosis = _timing.CurTime + humanity.PsychosisCooldown;
        }

        // Give the body back to the player.
        if (ent.Comp.Mind is { } mindId
            && TryComp<MindComponent>(mindId, out var mind)
            && mind.VisitingEntity != null
            && mind.VisitingEntity == ent.Comp.Viewer)
        {
            _mind.UnVisit(mindId, mind);
        }

        if (ent.Comp.Viewer is { } viewer && !TerminatingOrDeleted(viewer))
            QueueDel(viewer);

        if (!TerminatingOrDeleted(uid))
            _popup.PopupEntity(Loc.GetString("cyberpsychosis-end"), uid, uid, PopupType.Large);
    }
}
