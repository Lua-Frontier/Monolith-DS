using Content.Server._NF.Bank;
using Content.Server.CartridgeLoader;
using Content.Shared._LuaM.Yupi;
using Content.Shared._NF.Bank.Components;
using Content.Shared.CartridgeLoader;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Player;

namespace Content.Server._LuaM.Yupi;

public sealed partial class YupiCartridgeSystem : EntitySystem
{
    [Dependency] private BankSystem _bank = default!;
    [Dependency] private CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private YupiSystem _yupi = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<YupiCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<YupiCartridgeComponent, CartridgeMessageEvent>(OnMessage);
    }

    private void OnUiReady(Entity<YupiCartridgeComponent> ent, ref CartridgeUiReadyEvent args)
    {
        UpdateUi(args.Loader);
    }

    private void OnMessage(Entity<YupiCartridgeComponent> ent, ref CartridgeMessageEvent args)
    {
        if (args is not YupiTransferMessage message)
            return;

        var loader = GetEntity(args.LoaderUid);

        if (!TryGetHolder(loader, out var holder) || holder != args.Actor)
            return;

        var result = _yupi.TryTransfer(holder, message.TargetCode, message.Amount, out var target, out var commission, out var received);
        if (result == YupiTransferResult.Success)
        {
            var net = message.Amount - commission;
            _popup.PopupEntity(Loc.GetString("yupi-transfer-sent",
                    ("amount", YupiRules.FormatCredits(message.Amount)),
                    ("code", _yupi.GetOrCreateCode(target)),
                    ("commission", YupiRules.FormatCredits(commission)),
                    ("net", YupiRules.FormatCredits(net))),
                holder,
                holder);

            var savings = net - received;
            _popup.PopupEntity(Loc.GetString("yupi-transfer-received",
                    ("amount", YupiRules.FormatCredits(received)),
                    ("code", _yupi.GetOrCreateCode(holder)),
                    ("hasSavings", savings > 0 ? "yes" : "no"),
                    ("savings", YupiRules.FormatCredits(savings))),
                target,
                target);

            RefreshOpenApps(target);
        }
        else
        {
            _popup.PopupEntity(Loc.GetString(GetErrorLoc(result)), holder, holder, PopupType.SmallCaution);
        }

        UpdateUi(loader);
    }

    private void UpdateUi(EntityUid loader)
    {
        var code = string.Empty;
        var balance = 0;
        var lowRateRemaining = 0;
        var nextTransfer = TimeSpan.Zero;

        if (TryGetHolder(loader, out var holder))
        {
            code = _yupi.GetOrCreateCode(holder);
            _bank.TryGetBalance(holder, out balance);
            if (_player.TryGetSessionByEntity(holder, out var session))
            {
                lowRateRemaining = _yupi.GetLowRateRemaining(session.UserId);
                nextTransfer = _yupi.GetNextTransferTime(session.UserId);
            }
        }

        _cartridgeLoader.UpdateCartridgeUiState(loader, new YupiUiState(code, balance, lowRateRemaining, nextTransfer));
    }

    private void RefreshOpenApps(EntityUid holder)
    {
        var query = EntityQueryEnumerator<CartridgeLoaderComponent>();
        while (query.MoveNext(out var loader, out var loaderComp))
        {
            if (!HasComp<YupiCartridgeComponent>(loaderComp.ActiveProgram)
                || !TryGetHolder(loader, out var loaderHolder)
                || loaderHolder != holder)
                continue;

            UpdateUi(loader);
        }
    }

    private bool TryGetHolder(EntityUid loader, out EntityUid holder)
    {
        var current = loader;
        while (_container.TryGetContainingContainer((current, null, null), out var container))
        {
            current = container.Owner;
            if (!HasComp<BankAccountComponent>(current))
                continue;

            holder = current;
            return true;
        }

        holder = default;
        return false;
    }

    private static string GetErrorLoc(YupiTransferResult result)
    {
        return result switch
        {
            YupiTransferResult.InvalidAmount => "yupi-error-invalid-amount",
            YupiTransferResult.InvalidTarget => "yupi-error-invalid-target",
            YupiTransferResult.SelfTransfer => "yupi-error-self-transfer",
            YupiTransferResult.NoAccount => "yupi-no-account",
            YupiTransferResult.TargetUnavailable => "yupi-error-target-unavailable",
            YupiTransferResult.InsufficientFunds => "yupi-error-insufficient-funds",
            YupiTransferResult.Cooldown => "yupi-error-cooldown",
            _ => "yupi-error-disabled",
        };
    }
}
