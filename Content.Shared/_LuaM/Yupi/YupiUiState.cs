using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared._LuaM.Yupi;

[Serializable, NetSerializable]
public sealed class YupiUiState : BoundUserInterfaceState
{
    public readonly string OwnCode;

    public readonly int Balance;

    public readonly int LowRateRemaining;

    public readonly TimeSpan NextTransfer;

    public readonly bool Blocked;

    public YupiUiState(string ownCode, int balance, int lowRateRemaining, TimeSpan nextTransfer, bool blocked = false)
    {
        Blocked = blocked;
        OwnCode = ownCode;
        Balance = balance;
        LowRateRemaining = lowRateRemaining;
        NextTransfer = nextTransfer;
    }
}

[Serializable, NetSerializable]
public sealed class YupiTransferMessage : CartridgeMessageEvent
{
    public readonly string TargetCode;
    public readonly int Amount;

    public YupiTransferMessage(string targetCode, int amount)
    {
        TargetCode = targetCode;
        Amount = amount;
    }
}
