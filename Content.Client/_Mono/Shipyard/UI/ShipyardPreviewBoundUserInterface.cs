using static Content.Shared._Mono.Shipyard.SharedPreview;
using static Robust.Client.UserInterface.Controls.BaseButton;

namespace Content.Client._Mono.Shipyard.UI;

public sealed partial class ShipyardPreviewBoundUserInterface : BoundUserInterface
{
    private ShipyardPreviewMenu? _menu;
    [Dependency] private ShipyardPreviewSystem _preview = default!;

    public ShipyardPreviewBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {

    }

    protected override void Open()
    {
        base.Open();

        _menu = new ShipyardPreviewMenu();
        _menu.OpenCentered();
        _menu.UpdateMenu();

        _menu.OnExit += Exit;
        _preview.GridLoaded += OnGridLoaded; // LuaM: the vessel may finish loading after the window opened
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;
        _preview.GridLoaded -= OnGridLoaded; // LuaM
        _menu?.Dispose();
    }

    // LuaM
    private void OnGridLoaded()
    {
        _menu?.UpdateMenu();
    }

    private void Exit(ButtonEventArgs args)
    {
        SendMessage(new ShipyardPreviewExitMessage());
        _preview.Dispose();
    }
}
