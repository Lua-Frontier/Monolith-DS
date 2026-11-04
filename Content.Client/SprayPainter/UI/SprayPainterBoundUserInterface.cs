using Content.Shared._LuaM.SprayPainter; // LuaM
using Content.Shared.SprayPainter;
using Content.Shared.SprayPainter.Components;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.SprayPainter.UI;

public sealed class SprayPainterBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private SprayPainterWindow? _window;

    public SprayPainterBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<SprayPainterWindow>();

        _window.OnSpritePicked = OnSpritePicked;
        _window.OnColorPicked = OnColorPicked;

        // LuaM-start
        _window.Decals.OnDecalSelected = id => SendPredictedMessage(new SprayPainterSetDecalMessage(id));
        _window.Decals.OnColorChanged = color => SendPredictedMessage(new SprayPainterSetDecalColorMessage(color));
        _window.Decals.OnAngleChanged = angle => SendPredictedMessage(new SprayPainterSetDecalAngleMessage(angle));
        _window.Decals.OnSnapChanged = snap => SendPredictedMessage(new SprayPainterSetDecalSnapMessage(snap));
        _window.PopulateDecals(EntMan.System<SprayPainterSystem>().Decals);
        // LuaM-end

        if (EntMan.TryGetComponent(Owner, out SprayPainterComponent? comp))
        {
            _window.Populate(EntMan.System<SprayPainterSystem>().Entries, comp.Index, comp.PickedColor, comp.ColorPalette);
        }

        Update(); // LuaM
    }

    // LuaM-start
    public override void Update()
    {
        if (_window == null || !EntMan.TryGetComponent(Owner, out SprayPainterComponent? comp))
            return;

        _window.Decals.SetSelectedDecal(comp.SelectedDecal);
        _window.Decals.SetAngle(comp.SelectedDecalAngle);
        _window.Decals.SetColor(comp.SelectedDecalColor);
        _window.Decals.SetSnap(comp.SnapDecals);
    }
    // LuaM-end

    private void OnSpritePicked(ItemList.ItemListSelectedEventArgs args)
    {
        SendMessage(new SprayPainterSpritePickedMessage(args.ItemIndex));
    }

    private void OnColorPicked(ItemList.ItemListSelectedEventArgs args)
    {
        var key = _window?.IndexToColorKey(args.ItemIndex);
        SendMessage(new SprayPainterColorPickedMessage(key));
    }
}
