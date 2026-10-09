using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.Communications;
using Content.Server.GameTicking.Rules;
using Content.Server.Radio.EntitySystems;
using Content.Server._Mono.AlertLevel;
using Content.Shared._Mono.Company;
using Content.Shared.GameTicking.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Server._Mono.WarDeclarator;

// LuaM: war can also be declared from the faction communications consoles
// (see Content.Server/_LuaM/WarDeclaration/CommunicationsConsoleSystem.WarDeclaration.cs)
public sealed partial class ManualPortstrikeRuleSystem : GameRuleSystem<ManualPortstrikeRuleComponent>
{
    [Dependency] private WarLevelSystem _warLevelSystem = default!;
    [Dependency] private ChatSystem _chat = default!; // LuaM
    [Dependency] private CommunicationsConsoleSystem _comms = default!; // LuaM
    [Dependency] private IPrototypeManager _prototypes = default!; // LuaM
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private RadioSystem _radio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FactionWarDeclaratorComponent, UseInHandEvent>(OnWarDeclaratorUsed);
    }

    private void OnWarDeclaratorUsed(Entity<FactionWarDeclaratorComponent> ent, ref UseInHandEvent args)
    {
        if (!TryComp<CompanyComponent>(args.User, out var userCompany) || ent.Comp.Faction != userCompany.CompanyName)
        {
            _popup.PopupEntity(Loc.GetString(ent.Comp.WarDeclarationFailedMessage), ent, args.User);
            return;
        }

        var query = EntityQueryEnumerator<ManualPortstrikeRuleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.SectorStatus.TryGetValue(ent.Comp.Faction, out var warAlreadyDeclared) || warAlreadyDeclared)
                continue;

            comp.SectorStatus[ent.Comp.Faction] = true;

            var channel = _prototypes.Index(ent.Comp.Channel);
            _radio.SendRadioMessage(ent, Loc.GetString(ent.Comp.WarDeclarationMessage), channel, ent);

            var factionYetToDeclare = false;
            foreach (var factionDeclaredWar in comp.SectorStatus.Values)
            {
                if (!factionDeclaredWar)
                {
                    factionYetToDeclare = true;
                    break;
                }
            }
            if (!factionYetToDeclare)
                _warLevelSystem.SetLevel(comp.WarLevel);
        }
    }

    /// <summary>
    /// Whether an active manual portstrike rule involves the faction, and if it has already declared war.
    /// </summary>
    public bool TryGetFactionStatus(ProtoId<CompanyPrototype> faction, out bool declared)
    {
        declared = false;
        var found = false;

        var query = QueryActiveRules();
        while (query.MoveNext(out _, out _, out var comp, out _))
        {
            if (!comp.SectorStatus.TryGetValue(faction, out var status))
                continue;

            found = true;
            declared |= status;
        }

        return found;
    }

    /// <summary>
    /// Marks the faction as having declared war. Once every faction has declared, the war level is set.
    /// </summary>
    /// <param name="autoDeclareDelay">If other factions still have to declare, the time after which it happens automatically.</param>
    public bool TryDeclareWar(ProtoId<CompanyPrototype> faction, out TimeSpan? autoDeclareDelay)
    {
        var declared = false;
        autoDeclareDelay = null;

        var query = QueryActiveRules();
        while (query.MoveNext(out _, out _, out var comp, out _))
        {
            if (!comp.SectorStatus.TryGetValue(faction, out var warAlreadyDeclared) || warAlreadyDeclared)
                continue;

            comp.SectorStatus[faction] = true;
            comp.FirstDeclarationTime ??= Timing.CurTime;
            declared = true;

            if (!comp.SectorStatus.ContainsValue(false))
                _warLevelSystem.SetLevel(comp.WarLevel);
            else
            {
                autoDeclareDelay = comp.FirstDeclarationTime + comp.AutoDeclareDelay - Timing.CurTime;
                _warLevelSystem.SetPending(true);
            }
        }

        return declared;
    }

    // LuaM: declare war for the factions that did not answer in time
    protected override void ActiveTick(EntityUid uid, ManualPortstrikeRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (component.FirstDeclarationTime is not { } first ||
            Timing.CurTime < first + component.AutoDeclareDelay ||
            !component.SectorStatus.ContainsValue(false))
            return;

        foreach (var (faction, declared) in component.SectorStatus.ToArray())
        {
            if (declared)
                continue;

            component.SectorStatus[faction] = true;

            var name = _prototypes.TryIndex(faction, out var proto) ? proto.Name : faction.Id;
            var color = proto?.Color ?? Color.Red;
            _chat.DispatchGlobalAnnouncement(
                Loc.GetString("comms-console-war-declaration-auto", ("faction", name)),
                Loc.GetString("comms-console-war-declaration-auto-sender"),
                colorOverride: color);
        }

        _warLevelSystem.SetLevel(component.WarLevel);
        _comms.UpdateCommsConsoleInterface();
    }
}
