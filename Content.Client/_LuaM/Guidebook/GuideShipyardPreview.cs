/* LuaCorp - AGPLv3. Copyright (c) 2026 LuaCorp Contributors. See AGPLv3.txt. */
using System.Linq;
using Content.Client._NF.Shipyard.UI;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._LuaM.Guidebook;

/// <summary>Only mapped shipyards, using the live console layout without a bound interface.</summary>
public sealed partial class GuideShipyardPreview : BoxContainer
{
    [Dependency] private IEntityManager _entities = default!;
    private readonly GuidebookCatalogClientSystem _catalog;
    private ShipyardConsoleMenu? _window;

    public GuideShipyardPreview()
    {
        IoCManager.InjectDependencies(this);
        _catalog = _entities.System<GuidebookCatalogClientSystem>();
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        SeparationOverride = 6;
    }

    private void Rebuild()
    {
        RemoveAllChildren();
        if (_catalog.Catalog is not { } data)
        {
            Close();
            AddChild(new Label { Text = "Загрузка верфей…" });
            _catalog.Request();
            return;
        }
        foreach (var yard in data.Shipyards.OrderBy(y => y.Name))
        {
            var button = new Button { Text = yard.Name, HorizontalExpand = true };
            button.OnPressed += _ =>
            {
                Close();
                var window = new ShipyardConsoleMenu();
                _window = window;
                window.SetGuidebookReadOnly(yard.Name, yard.Vessels.ToList());
                window.OnPreviewShip += vessel =>
                {
                    var record = data.Vessels.FirstOrDefault(v => v.Id == vessel.ID);
                    if (record != null) _entities.System<GuidebookMapPreviewSystem>().Open(record.MapPath, record.Name);
                };
                window.OnClose += Close;
                window.OpenCentered();
            };
            AddChild(button);
        }
    }

    private void Close()
    {
        var window = _window; _window = null;
        if (window == null) return;
        window.OnClose -= Close;
        if (window.Parent != null) window.Close();
    }

    protected override void EnteredTree() { base.EnteredTree(); _catalog.Updated += Rebuild; Rebuild(); _catalog.Request(); }
    protected override void ExitedTree() { _catalog.Updated -= Rebuild; Close(); base.ExitedTree(); }
}
