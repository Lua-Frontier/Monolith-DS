using Content.Shared.Power;
using Robust.Client.UserInterface;

namespace Content.Client.Power.PowerCharge;

public sealed class PowerChargeBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private PowerChargeWindow? _window;

    public PowerChargeBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    public void SetPowerSwitch(bool on)
    {
        SendMessage(new SwitchChargingMachineMessage(on));
    }

    // Lua start
    public void ActionButton()
    {
        SendMessage(new PowerChargeActionMessage());
    }
    // Lua end

    protected override void Open()
    {
        base.Open();
        if (!EntMan.TryGetComponent(Owner, out PowerChargeComponent? component))
            return;

        _window = this.CreateWindow<PowerChargeWindow>();
        _window.UpdateWindow(this, Loc.GetString(component.WindowTitle));
        _window.SetActionUI(component.ActionUI); // Lua
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not PowerChargeState chargeState)
            return;

        _window?.UpdateState(chargeState);
    }
}
