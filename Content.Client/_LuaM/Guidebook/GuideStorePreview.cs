/* LuaCorp - AGPLv3. Copyright (c) 2026 LuaCorp Contributors. See AGPLv3.txt. */
using System.Linq;
using Content.Client.Store.Ui;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._LuaM.Guidebook;

/// <summary>Stock native store windows; never creates an activatable entity or a BUI.</summary>
public sealed partial class GuideStorePreview : BoxContainer
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IEntityManager _entities = default!;
    private readonly GuidebookCatalogClientSystem _catalog;
    private readonly List<Button> _buttons = new();
    private readonly Label _status = new() { Text = "Загрузка ассортимента…" };
    private StoreMenu? _window;
    private static readonly (string Id, string Name)[] Stores =
    {
        ("VendingMachinePDVUplink", "Авангард"), ("VendingMachineTSFMCUplink", "ТСФ"),
        ("VendingMachineMMCUplink", "ММС"), ("VendingMachineSyndieContrabandPOI", "Контравенд"),
    };

    public GuideStorePreview()
    {
        IoCManager.InjectDependencies(this);
        _catalog = _entities.System<GuidebookCatalogClientSystem>();
        Orientation = LayoutOrientation.Vertical; HorizontalExpand = true; SeparationOverride = 6;
        AddChild(_status);
        foreach (var source in Stores)
        {
            var button = new Button { Text = "Открыть снабжение: " + source.Name, HorizontalExpand = true };
            button.OnPressed += _ => Open(source.Id, source.Name);
            _buttons.Add(button);
            AddChild(button);
        }
    }

    private void Open(string id, string name)
    {
        Close();
        if (!_prototypes.TryIndex<EntityPrototype>(id, out var source)
            || !source.Components.TryGetValue("Store", out var entry) || entry.Component is not StoreComponent store) return;
        var available = _catalog.Catalog?.StoreListings.GetValueOrDefault(id)?.ToHashSet() ?? new HashSet<string>();
        var listings = _prototypes.EnumeratePrototypes<ListingPrototype>()
            .Where(l => available.Contains(l.ID) && l.Categories.Overlaps(store.Categories))
            .Select(l => new ListingData(l)).ToList();
        foreach (var listing in listings) listing.Categories.IntersectWith(store.Categories);
        var window = new StoreMenu { Title = name + " — ассортимент" };
        _window = window;
        window.SetGuidebookReadOnly();
        void Refresh(string query)
        {
            var filtered = listings.Where(l => query.Length == 0 || ListingLocalisationHelpers.GetLocalisedNameOrEntityName(l, _prototypes)
                .Contains(query, StringComparison.CurrentCultureIgnoreCase)).Select(l => new ListingDataWithCostModifiers(l)).ToList();
            window.PopulateStoreCategoryButtons(filtered.ToHashSet());
            window.UpdateListing(filtered);
        }
        window.SearchTextUpdated += (_, query) => Refresh(query.Trim());
        window.OnCategoryButtonPressed += (_, category) => { window.CurrentCategory = category; window.UpdateListing(); };
        window.OnClose += Close;
        Refresh("");
        window.OpenCentered();
    }

    private void Close()
    {
        var window = _window; _window = null;
        if (window == null) return;
        window.OnClose -= Close;
        if (window.Parent != null) window.Close();
    }
    private void UpdateButtons()
    {
        _status.Visible = _catalog.Catalog == null;
        foreach (var button in _buttons) button.Disabled = _catalog.Catalog == null;
        if (_catalog.Catalog == null) { Close(); _catalog.Request(); }
    }
    protected override void EnteredTree() { base.EnteredTree(); _catalog.Updated += UpdateButtons; UpdateButtons(); _catalog.Request(); }
    protected override void ExitedTree() { _catalog.Updated -= UpdateButtons; Close(); base.ExitedTree(); }
}
