using Content.Shared._Mono.Economy;
using Content.Shared._Mono.Economy.Components;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Stacks;

namespace Content.Server._NF.Shipyard.Systems;

/// <summary>
/// Cash in the shipyard console: spesos put into its cash slot pay for a vessel before the bank account does.
/// </summary>
public sealed partial class ShipyardSystem
{
    [Dependency] private SharedCreditReceiverSystem _creditReceiver = default!;

    /// <summary>
    /// The cash in the console's cash slot. Read straight from the stack, so it is right even while
    /// the credit receiver has not caught up with a container change yet.
    /// </summary>
    private int GetConsoleCash(EntityUid console)
    {
        if (!TryComp<CreditReceiverComponent>(console, out var receiver)
            || !_creditReceiver.TryGetCashSlot(console, out var slot)
            || !TryComp<StackComponent>(slot.ContainerSlot?.ContainedEntity, out var stack)
            || stack.StackTypeId != receiver.CurrencyStackType)
            return 0;

        return stack.Count;
    }

    private bool IsConsoleCashSlot(EntityUid console, string containerId)
    {
        return TryComp<CreditReceiverComponent>(console, out var receiver) && receiver.CashSlotName == containerId;
    }

    /// <summary>
    /// Pays the price with the cash in the console first and takes the rest from the bank account.
    /// The change stays in the console.
    /// </summary>
    private bool TryPayWithCashAndBank(EntityUid console, EntityUid player, BankAccountComponent bank, int price, out int paidCash)
    {
        paidCash = 0;

        var cashPart = Math.Min(GetConsoleCash(console), price);
        var bankPart = price - cashPart;
        if (bankPart > bank.Balance)
            return false;

        if (bankPart > 0 && !_bank.TryBankWithdraw(player, bankPart))
            return false;

        if (cashPart > 0 && !_creditReceiver.TryCashPayment(console, cashPart, out _))
        {
            if (bankPart > 0)
                _bank.TryBankDeposit(player, bankPart, tax: false);
            return false;
        }

        paidCash = cashPart;
        return true;
    }
}
