/* LuaCorp - AGPLv3. Copyright (c) 2026 LuaCorp Contributors. See AGPLv3.txt. */
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client.Guidebook.Richtext;
using Content.Shared._LuaM.Guidebook;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
namespace Content.Client._LuaM.Guidebook;
public sealed class GuideSites : BoxContainer, IDocumentTag
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    private readonly GuidebookCatalogClientSystem _system;
    private readonly LineEdit _search = new() { HorizontalExpand = true, PlaceHolder = "Объект или оборудование…" };
    private readonly BoxContainer _rows = new() { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 6 };
    public GuideSites()
    {
        IoCManager.InjectDependencies(this); _system = _entities.System<GuidebookCatalogClientSystem>();
        Orientation = LayoutOrientation.Vertical; HorizontalExpand = true; AddChild(_search); AddChild(_rows);
        _search.OnTextChanged += _ => Rebuild();
    }
    protected override void EnteredTree() { base.EnteredTree(); _system.Updated += Rebuild; _system.Request(); }
    protected override void ExitedTree() { _system.Updated -= Rebuild; base.ExitedTree(); }
    protected override void Dispose(bool disposing) { _system.Updated -= Rebuild; base.Dispose(disposing); }
    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control) { control = this; Rebuild(); return true; }
    private string Name(string id) => _prototypes.TryIndex<EntityPrototype>(id, out var p) ? p.Name : "";
    private void Rebuild()
    {
        foreach (var child in _rows.Children.ToArray()) child.Dispose();
        if (_system.Catalog is not { } data) { _rows.AddChild(Text("Загрузка с сервера…")); return; }
        var query = _search.Text.Trim();
        var sites = data.Sites.OrderBy(s => s.Name).Where(s => query.Length == 0 || s.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || s.Facilities.Keys.Any(id => Name(id).Contains(query, StringComparison.CurrentCultureIgnoreCase))).ToList();
        _rows.AddChild(Text(sites.Count + " объектов"));
        if (sites.Count == 0) { _rows.AddChild(Text("Нет объектов по этому запросу.")); return; }
        var browser = new GuideCatalogList(); _rows.AddChild(browser);
        foreach (var site in sites)
        {
            browser.AddEntry(site.Name, site.Hidden ? "Без обычной отметки БСС" : "Объект сектора", null, body =>
            {
                var map = new GuideMap(); map.TryParseTag(new() { ["Map"] = site.MapPath, ["Name"] = site.Name }, out _); body.AddChild(map);
                AddList(body, "Оборудование", site.Facilities);
                AddList(body, "Обитатели и спавнеры", site.Inhabitants);
                AddList(body, "Отмеченные ценности", site.Finds);
            });
        }
        browser.SelectFirst();
    }
    private void AddList(BoxContainer body, string title, Dictionary<string, int> items)
    {
        if (items.Count == 0) return;
        body.AddChild(Text(title, true));
        foreach (var (id, count) in items.OrderBy(p => Name(p.Key)))
        { var name = Name(id); if (name.Length > 0) body.AddChild(Text("• " + name + " ×" + count)); }
    }
    private static RichTextLabel Text(string value, bool bold = false)
    { var label = new RichTextLabel { HorizontalExpand = true }; label.SetMessage(bold
        ? FormattedMessage.FromMarkupPermissive("[bold]" + FormattedMessage.EscapeText(value) + "[/bold]") : FormattedMessage.FromUnformatted(value)); return label; }
}
