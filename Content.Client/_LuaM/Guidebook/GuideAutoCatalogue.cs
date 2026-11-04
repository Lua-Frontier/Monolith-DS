/* LuaCorp - AGPLv3. Copyright (c) 2026 LuaCorp Contributors. See AGPLv3.txt. */
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Content.Client.Guidebook.Richtext;
using Content.Shared._LuaM.Guidebook;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Utility;

namespace Content.Client._LuaM.Guidebook;

/// <summary>Native paged lists over the server's reachable reference data.</summary>
public sealed class GuideAutoCatalogue : BoxContainer, IDocumentTag
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    private readonly GuidebookCatalogClientSystem _catalog;
    private readonly LineEdit _search = new() { PlaceHolder = "Поиск…", HorizontalExpand = true };
    private readonly BoxContainer _body = new() { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true };
    private readonly Label _status = new();
    private readonly Button _previous = new() { Text = "Назад" };
    private readonly Button _next = new() { Text = "Далее" };
    private string _kind = "ammo";
    private int _page;
    private readonly OptionButton _caliber = new() { HorizontalExpand = true, Visible = false };
    private List<string> _calibers = new();
    private string _selectedCaliber = "";
    private GuidebookCatalog? _filterCatalog;
    private readonly Dictionary<string, string> _ammoCalibers = new();

    public GuideAutoCatalogue()
    {
        IoCManager.InjectDependencies(this);
        _catalog = _entities.System<GuidebookCatalogClientSystem>();
        Orientation = LayoutOrientation.Vertical; HorizontalExpand = true; SeparationOverride = 6;
        AddChild(_caliber);
        AddChild(_search);
        _caliber.OnItemSelected += args => { _selectedCaliber = _calibers[args.Id]; _page = 0; Rebuild(); };
        var navigation = new BoxContainer(); navigation.AddChild(_previous); navigation.AddChild(_status); navigation.AddChild(_next);
        AddChild(navigation); AddChild(_body);
        _search.OnTextChanged += _ => { _page = 0; Rebuild(); };
        _previous.OnPressed += _ => { _page--; Rebuild(); };
        _next.OnPressed += _ => { _page++; Rebuild(); };
    }
    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    { _kind = args.GetValueOrDefault("Kind", "ammo"); control = this; Rebuild(); return true; }
    protected override void EnteredTree() { base.EnteredTree(); _catalog.Updated += Rebuild; _catalog.Request(); Rebuild(); }
    protected override void ExitedTree() { _catalog.Updated -= Rebuild; base.ExitedTree(); }
    protected override void Dispose(bool disposing) { _catalog.Updated -= Rebuild; base.Dispose(disposing); }
    private string Name(string id) => _prototypes.TryIndex<EntityPrototype>(id, out var p) ? p.Name : id;
    private string Caliber(string id)
    {
        if (!_prototypes.TryIndex<EntityPrototype>(id, out var proto) ||
            !_prototypes.GetPrototypeData(proto).TryGetValue("Tag", out var tag) ||
            !tag.TryGet("tags", out SequenceDataNode? tags)) return "";
        return tags.OfType<ValueDataNode>().Select(t => t.Value)
            .Where(t => Regex.IsMatch(t, @"^(Cartridge|Shell).*\d"))
            .OrderBy(t => t.Length).ThenBy(t => t).FirstOrDefault() ?? "";
    }
    private string CaliberName(string key, string example)
    {
        var match = Regex.Match(example, @"\d+(?:[.,]\d+)?\s*[xх×]\s*\d+(?:[.,]\d+)?\s*(?:мм|mm)R?|\.?\d+(?:[.,]\d+)?\s*(?:калибр|gauge|ACP|SAM|Magnum)|\.\d+", RegexOptions.IgnoreCase);
        if (match.Success) return match.Value;
        // New cartridge tags remain filterable even before their names receive localization.
        return Regex.Replace(key, @"^(Cartridge|Shell)", "").Replace("mm", " мм");
    }
    private void Rebuild()
    {
        foreach (var child in _body.Children.ToArray()) child.Dispose();
        if (_catalog.Catalog is not { } data) { _status.Text = "Загрузка…"; _previous.Disabled = _next.Disabled = true; _catalog.Request(); return; }
        _caliber.Visible = _kind == "ammo";
        if (_caliber.Visible && !ReferenceEquals(_filterCatalog, data))
        {
            _filterCatalog = data;
            _ammoCalibers.Clear();
            foreach (var ammo in data.Ammunition) _ammoCalibers[ammo] = Caliber(ammo);
            var groups = data.Ammunition.GroupBy(id => _ammoCalibers[id]).Where(g => g.Key.Length > 0)
                .Select(g => (Key: g.Key, Label: CaliberName(g.Key, Name(g.First())))).OrderBy(g => g.Label).ToList();
            _calibers = new List<string> { "" }; _calibers.AddRange(groups.Select(g => g.Key));
            _caliber.Clear(); _caliber.AddItem("Все калибры", 0);
            for (var i = 0; i < groups.Count; i++) _caliber.AddItem(groups[i].Label, i + 1);
            if (!_calibers.Contains(_selectedCaliber)) _selectedCaliber = "";
        }
        if (_caliber.Visible) _caliber.SelectId(_calibers.IndexOf(_selectedCaliber));
        var offered = data.Shipyards.SelectMany(y => y.Vessels).ToHashSet();
        var entries = _kind switch
        {
            "drones" => data.Drones.Select(d => (Id: d.MapPath, Name: d.Name, Summary: string.Join("; ", d.Areas))),
            "loot" => data.Drones.SelectMany(d => d.Loot.Keys).Distinct().Select(id => (Id: id, Name: Name(id), Summary: "Регистратор с дронов")),
            "hydroponics" => data.Vessels.Where(v => offered.Contains(v.Id) && data.Hydroponics.ContainsKey(v.MapPath))
                .Select(v => (Id: v.Id, Name: v.Name, Summary: data.Hydroponics[v.MapPath].Count + " лотков")),
            "generators" => data.Generators.GroupBy(Name).Select(g => (Id: g.First(), Name: g.Key, Summary: "Электропитание")),
            _ => data.Ammunition.Where(id => _selectedCaliber.Length == 0 || _ammoCalibers.GetValueOrDefault(id) == _selectedCaliber).Select(id => (Id: id, Name: Name(id), Summary: "Боеприпасы")),
        };
        var list = entries.Where(e => e.Name.Contains(_search.Text.Trim(), StringComparison.CurrentCultureIgnoreCase)).OrderBy(e => e.Name).ToList();
        var pages = Math.Max(1, (list.Count + 11) / 12); _page = Math.Clamp(_page, 0, pages - 1);
        _status.Text = $"{list.Count} · {_page + 1}/{pages}"; _previous.Disabled = _page == 0; _next.Disabled = _page + 1 == pages;
        var browser = new GuideCatalogList(); _body.AddChild(browser);
        foreach (var entry in list.Skip(_page * 12).Take(12))
        {
            var icon = _prototypes.TryIndex<EntityPrototype>(entry.Id, out var proto) ? _entities.System<SpriteSystem>().Frame0(proto) : null;
            browser.AddEntry(entry.Name, entry.Summary, icon, body => Fill(body, entry.Id, data));
        }
        browser.SelectFirst();
    }
    private void Fill(BoxContainer body, string id, GuidebookCatalog data)
    {
        if (_kind == "drones")
        {
            var drone = data.Drones.First(d => d.MapPath == id);
            Text(body, "Область появления: " + string.Join("; ", drone.Areas));
            var plan = new GuideMap(); plan.TryParseTag(new() { ["Map"] = id, ["Name"] = drone.Name }, out _); body.AddChild(plan);
            foreach (var loot in drone.Loot) Text(body, Name(loot.Key) + " ×" + loot.Value);
        }
        else if (_kind == "hydroponics")
        {
            var vessel = data.Vessels.First(v => v.Id == id);
            foreach (var tray in data.Hydroponics[vessel.MapPath].GroupBy(t => t)) Text(body, Name(tray.Key) + " ×" + tray.Count());
            var plan = new GuideMap(); plan.TryParseTag(new() { ["Map"] = vessel.MapPath, ["Name"] = vessel.Name }, out _); body.AddChild(plan);
        }
        else if (_kind == "loot")
        {
            var loot = new GuideLoot(); loot.TryParseTag(new() { ["Entities"] = id }, out _); body.AddChild(loot);
        }
        else if (_kind == "generators")
        {
            var variants = data.Generators.Where(g => Name(g) == Name(id) && data.GeneratorStats.ContainsKey(g))
                .Select(g => data.GeneratorStats[g])
                .GroupBy(stats => string.Join("\n", stats.Select(s => s.Label + ": " + s.Value)))
                .Select(g => g.First()).ToList();
            for (var i = 0; i < variants.Count; i++)
            {
                if (variants.Count > 1) Text(body, "Вариант " + (i + 1));
                foreach (var stat in variants[i]) Text(body, stat.Label + ": " + stat.Value);
            }
        }
        else
        {
            var stats = new ArmorStats(); stats.TryParseTag(new() { ["Kind"] = "ammo", ["Entities"] = id }, out _); body.AddChild(stats);
        }
    }
    private static void Text(Control parent, string value)
    { var label = new RichTextLabel { HorizontalExpand = true }; label.SetMessage(FormattedMessage.FromUnformatted(value)); parent.AddChild(label); }
}
