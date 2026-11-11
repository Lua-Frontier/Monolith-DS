using Content.Server._NF.Bank;
using Content.Server._NF.SectorServices;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Shared._LuaM.Bank;
using Content.Shared._NF.Bank;
using Content.Shared._NF.Bank.BUI;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Administration;
using Content.Shared.Database;
using Robust.Shared.Player;

namespace Content.Server._LuaM.Bank;

/// <summary>
/// Lets admins see the faction station accounts from the admin panel and add or take money,
/// without going to the station console.
/// </summary>
public sealed class AdminSectorBankSystem : EntitySystem
{
    /// <summary>
    /// Needed to see the accounts.
    /// </summary>
    private const AdminFlags ViewFlag = AdminFlags.Admin;

    /// <summary>
    /// Needed to change them, the same as spawning money.
    /// </summary>
    private const AdminFlags ChangeFlag = AdminFlags.Spawn;

    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private BankSystem _bank = default!;
    [Dependency] private SectorServiceSystem _sectorService = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<AdminSectorBankRequestEvent>(OnRequest);
        SubscribeNetworkEvent<AdminSectorBankChangeEvent>(OnChange);
    }

    private void OnRequest(AdminSectorBankRequestEvent ev, EntitySessionEventArgs args)
    {
        if (!_admin.HasAdminFlag(args.SenderSession, ViewFlag))
            return;

        SendState(args.SenderSession, null);
    }

    private void OnChange(AdminSectorBankChangeEvent ev, EntitySessionEventArgs args)
    {
        var player = args.SenderSession;
        if (!_admin.HasAdminFlag(player, ViewFlag))
            return;

        var error = TryChange(player, ev.Account, ev.Amount);
        SendState(player, error);
    }

    /// <summary>
    /// Changes the account, returns why it failed or null if it went through.
    /// </summary>
    private string? TryChange(ICommonSession player, SectorBankAccount account, int amount)
    {
        if (!_admin.HasAdminFlag(player, ChangeFlag))
            return Loc.GetString("luam-admin-bank-error-no-access");

        if (amount == 0 || amount == int.MinValue)
            return Loc.GetString("luam-admin-bank-error-amount");

        if (!_bank.TryGetBalance(account, out var balance))
            return Loc.GetString("luam-admin-bank-error-no-account");

        // the balance is an int, don't let it overflow
        if (amount > 0 && amount > int.MaxValue - balance)
            return Loc.GetString("luam-admin-bank-error-amount");

        var name = Loc.GetString($"ledger-tab-{account}");

        if (amount > 0)
        {
            if (!_bank.TrySectorDeposit(account, amount, LedgerEntryType.AdminDeposit))
                return Loc.GetString("luam-admin-bank-error-failed");

            _adminLogger.Add(LogType.ATMUsage, LogImpact.High,
                $"{player.Name} added {amount} to the {account} station account from the admin panel, balance {balance} > {balance + amount}");
            _chat.SendAdminAnnouncement(Loc.GetString("luam-admin-bank-announce-deposit",
                ("admin", player.Name),
                ("account", name),
                ("amount", BankSystemExtensions.ToSpesoString(amount))));
            return null;
        }

        var withdraw = -amount;
        if (withdraw > balance)
            return Loc.GetString("luam-admin-bank-error-insufficient", ("balance", BankSystemExtensions.ToSpesoString(balance)));

        if (!_bank.TrySectorWithdraw(account, withdraw, LedgerEntryType.AdminWithdrawal))
            return Loc.GetString("luam-admin-bank-error-failed");

        _adminLogger.Add(LogType.ATMUsage, LogImpact.High,
            $"{player.Name} took {withdraw} from the {account} station account from the admin panel, balance {balance} > {balance - withdraw}");
        _chat.SendAdminAnnouncement(Loc.GetString("luam-admin-bank-announce-withdraw",
            ("admin", player.Name),
            ("account", name),
            ("amount", BankSystemExtensions.ToSpesoString(withdraw))));
        return null;
    }

    private void SendState(ICommonSession player, string? error)
    {
        var accounts = new List<AdminSectorBankAccountState>();
        if (TryComp<SectorBankComponent>(_sectorService.GetServiceEntity(), out var bank))
        {
            foreach (var (account, info) in bank.Accounts)
            {
                accounts.Add(new AdminSectorBankAccountState(account, info.Balance, info.IncreasePerSecond));
            }
        }
        else
        {
            error ??= Loc.GetString("luam-admin-bank-error-no-bank");
        }

        RaiseNetworkEvent(new AdminSectorBankStateEvent(accounts, error), player);
    }
}
