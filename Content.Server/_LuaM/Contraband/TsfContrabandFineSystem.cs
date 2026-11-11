using Content.Server._NF.Bank;
using Content.Server.Cargo.Systems;
using Content.Server.Radio.EntitySystems;
using Content.Shared._Mono.ItemTax.Components;
using Content.Shared._NF.Bank;
using Content.Shared._NF.Bank.BUI;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Server._LuaM.Contraband;

/// <summary>
/// Fines the TSF budget for contraband that got sold, and announces every fine on the TSF channel.
/// </summary>
public sealed class TsfContrabandFineSystem : EntitySystem
{
    private static readonly ProtoId<RadioChannelPrototype> TsfChannel = "Nfsd";

    [Dependency] private BankSystem _bank = default!;
    [Dependency] private PricingSystem _pricing = default!;
    [Dependency] private RadioSystem _radio = default!;

    /// <summary>
    /// The TSF fine for these items: price times their negative TSF tax, times the multiplier.
    /// Items without a negative TSF tax add nothing.
    /// </summary>
    public int GetFine(IEnumerable<EntityUid> items, float multiplier)
    {
        double fine = 0;
        foreach (var item in items)
        {
            if (!TryComp<ItemTaxComponent>(item, out var tax)
                || !tax.TaxAccounts.TryGetValue(SectorBankAccount.Nfsd, out var coeff)
                || coeff >= 0)
                continue;

            fine += _pricing.GetPrice(item) * -coeff * multiplier;
        }

        return (int) Math.Min(fine, int.MaxValue);
    }

    /// <summary>
    /// Takes the fine from the TSF budget, as much as there is, and announces it on the TSF channel.
    /// </summary>
    /// <param name="source">Where the contraband got sold, it speaks if the TSF has no admin console.</param>
    /// <param name="message">Locale of the announcement, gets $amount and $place.</param>
    public void Fine(EntityUid source, int amount, LocId message)
    {
        if (amount <= 0 || !_bank.TryGetBalance(SectorBankAccount.Nfsd, out var balance))
            return;

        // a fine bigger than the budget takes what is left instead of nothing
        var taken = Math.Min(amount, balance);
        if (taken > 0)
            _bank.TrySectorWithdraw(SectorBankAccount.Nfsd, taken, LedgerEntryType.TSFMCPenalties);

        var place = Transform(source).GridUid is { } grid ? Name(grid) : Name(source);
        var text = Loc.GetString(message,
            ("amount", BankSystemExtensions.ToSpesoString(amount)),
            ("place", place));

        if (taken < amount)
            text += " " + Loc.GetString("luam-tsf-fine-partial", ("taken", BankSystemExtensions.ToSpesoString(taken)));

        var speaker = GetTsfConsole() ?? source;
        _radio.SendRadioMessage(speaker, text, TsfChannel, speaker);
    }

    /// <summary>
    /// The TSF account console, it announces the fines from the TSF base.
    /// </summary>
    private EntityUid? GetTsfConsole()
    {
        var query = EntityQueryEnumerator<StationBankATMComponent>();
        while (query.MoveNext(out var uid, out var atm))
        {
            if (atm.Account == SectorBankAccount.Nfsd)
                return uid;
        }

        return null;
    }
}
