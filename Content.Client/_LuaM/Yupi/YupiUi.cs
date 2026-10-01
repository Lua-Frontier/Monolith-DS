using Content.Client.UserInterface.Fragments;
using Content.Shared._LuaM.Yupi;
using Content.Shared.CartridgeLoader;
using Robust.Client.UserInterface;

namespace Content.Client._LuaM.Yupi;

public sealed partial class YupiUi : UIFragment
{
    private YupiUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new YupiUiFragment();
        _fragment.OnSend += (code, amount) =>
            userInterface.SendMessage(new CartridgeUiMessage(new YupiTransferMessage(code, amount)));
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is YupiUiState yupiState)
            _fragment?.UpdateState(yupiState);
    }
}
