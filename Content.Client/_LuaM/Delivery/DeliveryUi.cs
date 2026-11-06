using Content.Client.UserInterface.Fragments;
using Content.Shared._LuaM.Delivery;
using Content.Shared.CartridgeLoader;
using Robust.Client.UserInterface;

namespace Content.Client._LuaM.Delivery;

public sealed partial class DeliveryUi : UIFragment
{
    private DeliveryUiFragment? _fragment;
    private BoundUserInterface? _userInterface;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _userInterface = userInterface;

        // -- Setup зовётся на каждое состояние КПК; новый фрагмент не прикрепляется и получал бы наши состояния вместо видимого
        if (_fragment is { Disposed: false })
            return;

        _fragment = new DeliveryUiFragment();
        _fragment.OnOrder += cart => Send(new DeliveryPlaceOrderMessage(cart));
        _fragment.OnTake += id => Send(new DeliveryTakeOrderMessage(id));
        _fragment.OnRequestCatalog += () => Send(new DeliveryRequestCatalogMessage());
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is DeliveryUiState deliveryState)
            _fragment?.UpdateState(deliveryState);
    }

    private void Send(CartridgeMessageEvent message)
    {
        _userInterface?.SendMessage(new CartridgeUiMessage(message));
    }
}
