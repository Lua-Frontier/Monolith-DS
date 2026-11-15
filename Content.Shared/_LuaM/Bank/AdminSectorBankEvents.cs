using Content.Shared._NF.Bank.Components;
using Robust.Shared.Serialization;

namespace Content.Shared._LuaM.Bank;

/// <summary>
/// Sent by the admin panel to get the faction station accounts.
/// </summary>
[Serializable, NetSerializable]
public sealed class AdminSectorBankRequestEvent : EntityEventArgs;

/// <summary>
/// Sent by the admin panel to add money to an account (positive amount) or take it (negative amount).
/// </summary>
[Serializable, NetSerializable]
public sealed class AdminSectorBankChangeEvent(SectorBankAccount account, int amount) : EntityEventArgs
{
    public readonly SectorBankAccount Account = account;
    public readonly int Amount = amount;
}

/// <summary>
/// The faction station accounts, sent to the admin who asked for them.
/// </summary>
[Serializable, NetSerializable]
public sealed class AdminSectorBankStateEvent(List<AdminSectorBankAccountState> accounts, string? error) : EntityEventArgs
{
    public readonly List<AdminSectorBankAccountState> Accounts = accounts;

    /// <summary>
    /// Why the last change failed, already localized. Null if it went through or nothing was changed.
    /// </summary>
    public readonly string? Error = error;
}

[Serializable, NetSerializable]
public readonly record struct AdminSectorBankAccountState(SectorBankAccount Account, int Balance, int IncreasePerSecond);
