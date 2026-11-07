/* LuaCorp - AGPLv3. Copyright (c) 2026 LuaCorp Contributors. See AGPLv3.txt. */
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using Content.Client.Guidebook.Richtext;
using Content.Shared.Mining;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._LuaM.Guidebook;

/// <summary>Only mineral tables reachable through the configured world generation.</summary>
public sealed partial class GuideOreMatrix : BoxContainer, IDocumentTag
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private ILocalizationManager _loc = default!;
    private readonly GuidebookCatalogClientSystem _catalog;
    private readonly OptionButton _grade = new() { HorizontalExpand = true };
    private readonly ScrollContainer _scroll = new() { HorizontalExpand = true, SetHeight = 350, HScrollEnabled = true };
    private readonly GridContainer _table = new() { Columns = 1 };
    private string _selected = "";
    private List<string> _grades = new();
    public GuideOreMatrix()
    {
        IoCManager.InjectDependencies(this);
        _catalog = _entities.System<GuidebookCatalogClientSystem>();
        Orientation = LayoutOrientation.Vertical; HorizontalExpand = true;
        _grade.OnItemSelected += a => { _selected = _grades[a.Id]; Rebuild(); };
        AddChild(_grade); _scroll.AddChild(_table); AddChild(_scroll);
    }
    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    { control = this; Rebuild(); return true; }
    protected override void EnteredTree() { base.EnteredTree(); _catalog.Updated += Rebuild; _catalog.Request(); Rebuild(); }
    protected override void ExitedTree() { _catalog.Updated -= Rebuild; base.ExitedTree(); }
    private void Rebuild()
    {
        _table.RemoveAllChildren();
        if (_catalog.Catalog is not { } data) { _table.Columns = 1; Cell("Загрузка…"); _catalog.Request(); return; }
        _grades = data.OreTables.Select(t => t.Grade).Distinct().OrderBy(g => g == "Soft" ? 0 : g == "Hard" ? 1 : 2).ToList();
        _grade.Clear();
        for (var i = 0; i < _grades.Count; i++) _grade.AddItem(_grades[i] switch { "Soft" => "Мягкая порода", "Hard" => "Твёрдая порода", "HardRich" => "Обогащённая порода", _ => _grades[i] }, i);
        if (_grades.Count == 0) { _table.Columns = 1; Cell("В текущей генерации нет подключённых таблиц руды."); return; }
        if (!_grades.Contains(_selected)) _selected = _grades[0];
        _grade.SelectId(_grades.IndexOf(_selected));
        var tables = data.OreTables.Where(t => t.Grade == _selected).ToList();
        var ores = tables.SelectMany(t => t.Probabilities.Keys).Distinct().OrderBy(Name).ToList();
        _table.Columns = tables.Count + 1;
        Cell("Руда / порода", true);
        foreach (var table in tables) Cell(table.Name, true, table.Color);
        foreach (var ore in ores)
        {
            Cell(Name(ore), true);
            foreach (var table in tables) Cell((table.Probabilities.GetValueOrDefault(ore) * 100).ToString("0.#", CultureInfo.CurrentCulture) + "%");
        }
    }
    private new string Name(string id)
    {
        if (id.Length == 0) return "Пустая порода";
        if (_loc.TryGetString("guide-ore-" + id, out var localized)) return localized;
        // OreVein refers to an ore prototype, not necessarily to an entity ID.
        // Newly connected ores inherit the localization of their actual mining yield.
        if (_prototypes.TryIndex<OrePrototype>(id, out var ore) && ore.OreEntity is { } entity)
            id = entity.Id;
        return _prototypes.TryIndex<EntityPrototype>(id, out var p) ? p.Name : id;
    }
    private void Cell(string text, bool name = false, string? color = null)
    {
        var label = new RichTextLabel { SetWidth = name ? 125 : 85, Margin = new Thickness(5, 6) };
        label.SetMessage(color == null ? FormattedMessage.FromUnformatted(text)
            : FormattedMessage.FromMarkupPermissive("[color=" + color + "]" + FormattedMessage.EscapeText(text) + "[/color]"));
        _table.AddChild(label);
    }
}
