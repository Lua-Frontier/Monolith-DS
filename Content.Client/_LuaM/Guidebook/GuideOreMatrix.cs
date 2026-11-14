/* LuaCorp - AGPLv3. Copyright (c) 2026 LuaCorp Contributors. See AGPLv3.txt. */
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using Content.Client.Guidebook.Richtext;
using Content.Shared._LuaM.Guidebook;
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
    private readonly OptionButton _family = new() { HorizontalExpand = true };
    private readonly RichTextLabel _legend = new() { HorizontalExpand = true };
    private readonly ScrollContainer _scroll = new() { HorizontalExpand = true, SetHeight = 350, HScrollEnabled = false };
    private readonly BoxContainer _table = new() { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true };
    private string _selected = "";
    private string _selectedTableId = "";
    private List<string> _grades = new();
    private List<GuidebookOreTable> _families = new();
    public GuideOreMatrix()
    {
        IoCManager.InjectDependencies(this);
        _catalog = _entities.System<GuidebookCatalogClientSystem>();
        Orientation = LayoutOrientation.Vertical; HorizontalExpand = true;
        _grade.OnItemSelected += a => { if (a.Id < 0 || a.Id >= _grades.Count) return; _selected = _grades[a.Id]; Rebuild(); };
        _family.OnItemSelected += a => { if (a.Id < 0 || a.Id >= _families.Count) return; _selectedTableId = _families[a.Id].Id; Rebuild(); };
        AddChild(Text("Разновидность породы")); AddChild(_grade);
        AddChild(Text("Тип астероида")); AddChild(_family); AddChild(_legend);
        _scroll.AddChild(_table); AddChild(_scroll);
    }
    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    { control = this; Rebuild(); return true; }
    protected override void EnteredTree() { base.EnteredTree(); _catalog.Updated += Rebuild; _catalog.Request(); Rebuild(); }
    protected override void ExitedTree() { _catalog.Updated -= Rebuild; base.ExitedTree(); }
    private void Rebuild()
    {
        _table.RemoveAllChildren();
        if (_catalog.Catalog is not { } data) { _table.AddChild(Text("Загрузка…")); _catalog.Request(); return; }
        _grades = data.OreTables.Select(t => t.Grade).Distinct().OrderBy(g => g == "Soft" ? 0 : g == "Hard" ? 1 : 2).ToList();
        _grade.Clear();
        for (var i = 0; i < _grades.Count; i++) _grade.AddItem(_grades[i] switch { "Soft" => "Мягкая порода", "Hard" => "Твёрдая порода", "HardRich" => "Обогащённая порода", _ => _grades[i] }, i);
        _family.Clear();
        _legend.SetMessage(FormattedMessage.FromUnformatted(""));
        if (_grades.Count == 0) { _table.AddChild(Text("Нет данных о распределении руды.")); return; }
        if (!_grades.Contains(_selected)) _selected = _grades[0];
        _grade.SelectId(_grades.IndexOf(_selected));
        _families = data.OreTables.Where(t => t.Grade == _selected).OrderBy(t => t.Name).ThenBy(t => t.Id).ToList();
        for (var i = 0; i < _families.Count; i++) _family.AddItem(_families[i].Name, i);
        var index = _families.FindIndex(t => t.Id == _selectedTableId);
        if (index < 0) index = 0;
        _family.SelectId(index);
        var table = _families[index];
        _selectedTableId = table.Id;
        _legend.SetMessage(FormattedMessage.FromMarkupPermissive("[color=" + table.Color + "]■[/color] Цвет отметки IFF"));
        Row("Ресурс", "Шанс", true);
        foreach (var (ore, probability) in table.Probabilities.OrderByDescending(p => p.Value).ThenBy(p => Name(p.Key)))
            Row(Name(ore), (probability * 100).ToString("0.#", CultureInfo.CurrentCulture) + "%");
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
    private void Row(string name, string chance, bool header = false)
    {
        var row = new BoxContainer { Name = "OreProbabilityRow", HorizontalExpand = true, SeparationOverride = 8, Margin = new Thickness(5, 4) };
        var resource = Text(name);
        var percentage = new Label { Text = chance, SetWidth = 64, Align = Label.AlignMode.Right,
            ModulateSelfOverride = header ? Color.Yellow : Color.LightBlue };
        if (header) resource.ModulateSelfOverride = Color.Yellow;
        row.AddChild(resource); row.AddChild(percentage); _table.AddChild(row);
    }
    private static RichTextLabel Text(string value)
    { var label = new RichTextLabel { HorizontalExpand = true }; label.SetMessage(FormattedMessage.FromUnformatted(value)); return label; }
}
