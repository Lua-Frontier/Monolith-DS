// Мёртвый Космос, Licensed under custom terms with restrictions on public hosting and commercial use, full text: https://raw.githubusercontent.com/dead-space-server/space-station-14-fobos/master/LICENSE.TXT

using Content.Shared.Actions;
using Content.Shared.DeadSpace.Necromorphs.Unitology;
using Content.Shared.DeadSpace.Necromorphs.Unitology.Components;
using Content.Server.Popups;
using Content.Server.DeadSpace.Necromorphs.Unitology.Components;
using Content.Shared.Zombies;
using Content.Shared.Humanoid;
using Content.Shared.DoAfter;
using System.Linq;
using Content.Server.Mind;
using Content.Server.Chat.Systems;
using Content.Shared.Chat;
using Content.Shared.Mobs.Systems;
using Content.Shared.Mindshield.Components;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Mobs;
using Robust.Shared.Containers;

namespace Content.Server.DeadSpace.Necromorphs.Unitology;

public sealed class UnitologyHeadSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedSubdermalImplantSystem _implants = default!;
    [Dependency] private readonly UnitologyEnslavedSystem _enslavedSystem = default!;
    [Dependency] private readonly UnitologyRoleSystem _roles = default!;


    private static readonly HashSet<string> HeadImplants =
    [
        "StorageImplant",
        "DnaScramblerImplant",
        "FreedomImplant",
    ];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UnitologyHeadComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<UnitologyHeadComponent, ComponentShutdown>(OnShutDown);
        SubscribeLocalEvent<UnitologyHeadComponent, MobStateChangedEvent>(OnHeadMobStateChanged);
        SubscribeLocalEvent<UnitologyHeadComponent, UnitologyHeadActionEvent>(OnHeadUnitology);
        SubscribeLocalEvent<UnitologyHeadComponent, OrderToSlaveActionEvent>(OnOrder);
        SubscribeLocalEvent<UnitologyHeadComponent, SelectTargetRecruitmentEvent>(OnSelectTargetRecruitment);
        SubscribeLocalEvent<UnitologyHeadComponent, UnitologistRecruitmentDoAfterEvent>(OnRecruitmentDoAfter);
    }

    private void OnComponentInit(EntityUid uid, UnitologyHeadComponent component, ComponentInit args)
    {
        _actionsSystem.AddAction(uid, ref component.ActionUnitologyHeadEntity, component.ActionUnitologyHead, uid);
        _actionsSystem.AddAction(uid, ref component.ActionOrderToSlaveEntity, component.ActionOrderToSlave, uid);
        _roles.GrantHead(uid);
    }

    private void OnShutDown(EntityUid uid, UnitologyHeadComponent component, ComponentShutdown args)
    {
        _actionsSystem.RemoveAction(uid, component.ActionUnitologyHeadEntity);
        _actionsSystem.RemoveAction(uid, component.ActionOrderToSlaveEntity);
        _actionsSystem.RemoveAction(uid, component.ActionSelectTargetRecruitmentEntity);
    }

    private void OnHeadMobStateChanged(EntityUid uid, UnitologyHeadComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead && args.NewMobState != MobState.Invalid)
            return;

        var enslavedQuery = EntityQueryEnumerator<UnitologyEnslavedComponent>();
        while (enslavedQuery.MoveNext(out var enslavedUid, out var enslavedComp))
        {
            if (enslavedComp.HeadUnitolog == uid)
                _enslavedSystem.FreeSlave(enslavedUid, enslavedComp);
        }
    }

    private void OnSelectTargetRecruitment(EntityUid uid, UnitologyHeadComponent component, SelectTargetRecruitmentEvent args)
    {
        if (args.Handled)
            return;

        if (args.Target == uid)
            return;

        var target = args.Target;

        if (!HasComp<HumanoidAppearanceComponent>(target)
        || HasComp<MindShieldComponent>(target)
        || HasComp<UnitologyHeadComponent>(target)
        || HasComp<UnitologyComponent>(target)
        || HasComp<UnitologyEnslavedComponent>(target)
        || !_mobState.IsAlive(target)
        || !_mindSystem.TryGetMind(target, out _, out _))
        {
            _popup.PopupEntity(Loc.GetString("Цель не подходит для вербовки."), uid, uid);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, uid, component.VerbDuration, new UnitologistRecruitmentDoAfterEvent(), uid, target: target)
        {
            Hidden = true,
            Broadcast = false,
            BreakOnDamage = true,
            BreakOnMove = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DistanceThreshold = 1
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        Random random = new Random();
        int index = random.Next(component.WordsArray.Length);
        string message = component.WordsArray[index];

        _chat.TrySendInGameICMessage(uid, message, InGameICChatType.Speak, true);

        args.Handled = true;
    }

    private void OnRecruitmentDoAfter(EntityUid uid, UnitologyHeadComponent component, UnitologistRecruitmentDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;

        var target = args.Args.Target;

        if (!_mindSystem.TryGetMind(target.Value, out _, out _))
            return;

        _roles.GrantRegular(target.Value);
    }

    private void OnOrder(EntityUid uid, UnitologyHeadComponent component, OrderToSlaveActionEvent args)
    {
        if (args.Handled)
            return;

        if (args.Target == uid)
            return;

        var target = args.Target;

        if (!HasComp<UnitologyEnslavedComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("Цель должна быть подчинена!"), uid, uid);
            return;
        }

        if (!HasComp<StunSlaveComponent>(target))
        {
            AddComp<StunSlaveComponent>(target);
            _popup.PopupEntity(Loc.GetString("Цель парализованна."), uid, uid);
        }
        else
        {
            RemComp<StunSlaveComponent>(target);
            _popup.PopupEntity(Loc.GetString("Цель может двигаться."), uid, uid);
        }

        args.Handled = true;

    }
    private void OnHeadUnitology(EntityUid uid, UnitologyHeadComponent component, UnitologyHeadActionEvent args)
    {
        if (args.Handled)
            return;

        if (args.Target == uid)
            return;

        var target = args.Target;
        if (!IsCanTransfer(uid, target))
            return;

        args.Handled = true;

        TransferHeadImplants(uid, target);

        var slaves = EntityQueryEnumerator<UnitologyEnslavedComponent>();
        while (slaves.MoveNext(out var slaveUid, out var slave))
        {
            if (slave.HeadUnitolog == uid)
            {
                slave.HeadUnitolog = target;
                Dirty(slaveUid, slave);
            }
        }

        RemComp<UnitologyHeadComponent>(uid);
        _roles.GrantRegular(uid);
        AddComp<UnitologyHeadComponent>(target);
    }

    private void TransferHeadImplants(EntityUid oldHead, EntityUid newHead)
    {
        if (!TryComp<ImplantedComponent>(oldHead, out var oldImplanted))
            return;

        var newImplanted = EnsureComp<ImplantedComponent>(newHead);
        var existing = newImplanted.ImplantContainer.ContainedEntities
            .Select(implant => Prototype(implant))
            .Where(proto => proto != null)
            .Select(proto => proto!.ID)
            .ToHashSet();

        foreach (var implant in oldImplanted.ImplantContainer.ContainedEntities.ToArray())
        {
            var prototype = Prototype(implant);
            if (prototype == null || !HeadImplants.Contains(prototype.ID))
                continue;

            if (existing.Contains(prototype.ID))
            {
                _implants.ForceRemove(oldHead, implant);
                continue;
            }

            if (!_containers.Remove(implant, oldImplanted.ImplantContainer, force: true))
                continue;

            _containers.Insert(implant, newImplanted.ImplantContainer);
            existing.Add(prototype.ID);
        }
    }

    private bool IsCanTransfer(EntityUid uid, EntityUid target)
    {
        if (!HasComp<UnitologyComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("Цель должна быть юнитологом!"), uid, uid);
            return false;
        }

        if (HasComp<UnitologyHeadComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("Цель уже обладает вашими знаниями и положением!"), uid, uid);
            return false;
        }

        if (HasComp<UnitologyEnslavedComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("Цель не может быть порабощенным!"), uid, uid);
            return false;
        }

        if (HasComp<ZombieComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("Цель не может быть выбрана!"), uid, uid);
            return false;
        }

        return true;
    }
}
