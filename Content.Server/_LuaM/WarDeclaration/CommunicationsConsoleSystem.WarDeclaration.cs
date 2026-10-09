using Content.Server.GameTicking;
using Content.Server._LuaM.WarDeclaration;
using Content.Server._Mono.WarDeclarator;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Communications;
using Content.Shared.Database;
using Content.Shared._LuaM.WarDeclaration;
using Content.Shared._Mono.Company;
using Robust.Shared.Timing;

namespace Content.Server.Communications;

// LuaM: war declaration from the faction communications consoles
public sealed partial class CommunicationsConsoleSystem
{
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ManualPortstrikeRuleSystem _manualPortstrike = default!;

    private void InitializeWarDeclaration()
    {
        SubscribeLocalEvent<CommunicationsConsoleComponent, CommunicationsConsoleDeclareWarMessage>(OnDeclareWarMessage);
    }

    private void FillWarDeclarationState(EntityUid uid, CommunicationsConsoleInterfaceState state)
    {
        if (!TryComp<ConsoleWarDeclaratorComponent>(uid, out var declarator) ||
            !_manualPortstrike.TryGetFactionStatus(declarator.Faction, out var declared))
            return;

        state.ShowWarDeclaration = true;
        state.WarDeclared = declared;
        state.WarDeclarationAvailableAt = _timing.CurTime + GetWarDeclarationTimeLeft(declarator);
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

        _chatSystem.DispatchGlobalAnnouncement(msg, title, announcementSound: comp.Sound, colorOverride: comp.Color);
        _adminLogger.Add(LogType.Action, LogImpact.Extreme, $"{ToPrettyString(mob):player} has declared war for {declarator.Faction}: {reason}");

        UpdateCommsConsoleInterface();
    }
}
