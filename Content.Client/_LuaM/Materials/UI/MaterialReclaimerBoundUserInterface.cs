using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._LuaM.Materials.UI;

[UsedImplicitly]
public sealed class MaterialReclaimerBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private MaterialReclaimerMenu? _menu;

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<MaterialReclaimerMenu>();
        _menu.SetEntity(Owner);
    }
}
