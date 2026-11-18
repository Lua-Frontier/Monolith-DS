using Content.Client._LuaM.FederationWanted.UI;
using Content.Shared._LuaM.FederationWanted;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._LuaM.FederationWanted;

[UsedImplicitly]
public sealed class FederationWantedConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private FederationWantedConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<FederationWantedConsoleWindow>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is FederationWantedConsoleState cast)
            _window?.UpdateEntries(cast.Entries);
    }
}
