using Content.Shared._LuaM.Bank;
using Content.Shared._NF.Bank.Components;

namespace Content.Client._LuaM.Bank;

/// <summary>
/// Talks to the server for the faction accounts tab of the admin panel.
/// </summary>
public sealed class AdminSectorBankSystem : EntitySystem
{
    public event Action<AdminSectorBankStateEvent>? StateReceived;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<AdminSectorBankStateEvent>(OnState);
    }

    private void OnState(AdminSectorBankStateEvent ev)
    {
        StateReceived?.Invoke(ev);
    }

    public void RequestState()
    {
        RaiseNetworkEvent(new AdminSectorBankRequestEvent());
    }

    /// <summary>
    /// Positive amount adds money, negative takes it.
    /// </summary>
    public void Change(SectorBankAccount account, int amount)
    {
        RaiseNetworkEvent(new AdminSectorBankChangeEvent(account, amount));
    }
}
