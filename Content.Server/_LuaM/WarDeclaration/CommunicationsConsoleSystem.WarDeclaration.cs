using Content.Server.GameTicking;
using Content.Server._LuaM.WarDeclaration;
using Content.Server._Mono.WarDeclarator;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Communications;
using Content.Shared.Database;
using Content.Shared._LuaM.WarDeclaration;
using Content.Shared._Mono.Company;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Communications;

// LuaM: war declaration and capitulation from the faction communications consoles
public sealed partial class CommunicationsConsoleSystem
{
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private ManualPortstrikeRuleSystem _manualPortstrike = default!;

    private void InitializeWarDeclaration()
    {
        SubscribeLocalEvent<CommunicationsConsoleComponent, CommunicationsConsoleDeclareWarMessage>(OnDeclareWarMessage);
        SubscribeLocalEvent<CommunicationsConsoleComponent, CommunicationsConsoleSurrenderMessage>(OnSurrenderMessage);

        // The console is built before the round rules start, so its first state has no war button.
        Subs.BuiEvents<CommunicationsConsoleComponent>(CommunicationsConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnConsoleUiOpened);
        });
    }

    private void OnConsoleUiOpened(EntityUid uid, CommunicationsConsoleComponent comp, BoundUIOpenedEvent args)
    {
        UpdateCommsConsoleInterface(uid, comp);
    }

    private void FillWarDeclarationState(EntityUid uid, CommunicationsConsoleInterfaceState state)
    {
        if (!TryComp<ConsoleWarDeclaratorComponent>(uid, out var declarator) ||
            !_manualPortstrike.TryGetFactionStatus(declarator.Faction, out var declared))
            return;

        state.ShowWarDeclaration = true;
        state.WarDeclared = declared;
        state.WarDeclarationAvailableAt = _timing.CurTime + GetWarDeclarationTimeLeft(declarator);

        _manualPortstrike.TryGetWarStatus(declarator.Faction, out state.WarActive, out state.WarOver);
    }

    private void OnSurrenderMessage(EntityUid uid, CommunicationsConsoleComponent comp, CommunicationsConsoleSurrenderMessage message)
    {
        if (!TryComp<ConsoleWarDeclaratorComponent>(uid, out var declarator) ||
            message.Actor is not { Valid: true } mob)
            return;

        if (!CanUse(mob, uid) ||
            !TryComp<CompanyComponent>(mob, out var company) ||
            company.CompanyName != declarator.Faction)
        {
            _popupSystem.PopupEntity(Loc.GetString("comms-console-permission-denied"), uid, mob);
            return;
        }

        if (!_manualPortstrike.TrySurrender(declarator.Faction))
            return;

        Loc.TryGetString(comp.Title, out var title);
        title ??= comp.Title;

        var faction = _prototype.TryIndex(declarator.Faction, out var proto) ? proto.Name : declarator.Faction.Id;
        _chatSystem.DispatchGlobalAnnouncement(Loc.GetString("comms-console-war-surrender", ("faction", faction)),
            title,
            announcementSound: comp.Sound,
            colorOverride: Color.White);
        _adminLogger.Add(LogType.Action, LogImpact.Extreme, $"{ToPrettyString(mob):player} has capitulated in the war for {declarator.Faction}");

        UpdateCommsConsoleInterface();
    }

    private TimeSpan GetWarDeclarationTimeLeft(ConsoleWarDeclaratorComponent declarator)
    {
        var left = declarator.MinRoundTime - _gameTicker.RoundDuration();
        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }

    private void OnDeclareWarMessage(EntityUid uid, CommunicationsConsoleComponent comp, CommunicationsConsoleDeclareWarMessage message)
    {
        if (!TryComp<ConsoleWarDeclaratorComponent>(uid, out var declarator) ||
            message.Actor is not { Valid: true } mob)
            return;

        if (!CanUse(mob, uid) ||
            !TryComp<CompanyComponent>(mob, out var company) ||
            company.CompanyName != declarator.Faction)
        {
            _popupSystem.PopupEntity(Loc.GetString("comms-console-permission-denied"), uid, mob);
            return;
        }

        if (!_manualPortstrike.TryGetFactionStatus(declarator.Faction, out var declared) || declared)
            return;

        if (GetWarDeclarationTimeLeft(declarator) > TimeSpan.Zero)
        {
            _popupSystem.PopupEntity(Loc.GetString("comms-console-war-declaration-too-early"), uid, mob);
            return;
        }

        var maxLength = _cfg.GetCVar(CCVars.ChatMaxAnnouncementLength);
        var reason = SharedChatSystem.SanitizeAnnouncement(message.Reason, maxLength);
        if (string.IsNullOrWhiteSpace(reason))
        {
            _popupSystem.PopupEntity(Loc.GetString("comms-console-war-declaration-no-reason"), uid, mob);
            return;
        }

        if (!_manualPortstrike.TryDeclareWar(declarator.Faction, out var autoDeclareDelay))
            return;

        Loc.TryGetString(comp.Title, out var title);
        title ??= comp.Title;

        var msg = Loc.GetString(declarator.WarDeclarationMessage) + "\n" +
                  Loc.GetString("comms-console-war-declaration-reason", ("reason", reason));

        if (autoDeclareDelay is { } delay)
            msg += "\n" + Loc.GetString("comms-console-war-declaration-auto-warning", ("minutes", (int) Math.Ceiling(delay.TotalMinutes)));

        _chatSystem.DispatchGlobalAnnouncement(msg, title, announcementSound: declarator.WarDeclarationSound ?? comp.Sound, colorOverride: comp.Color);
        _adminLogger.Add(LogType.Action, LogImpact.Extreme, $"{ToPrettyString(mob):player} has declared war for {declarator.Faction}: {reason}");

        UpdateCommsConsoleInterface();
    }
}
