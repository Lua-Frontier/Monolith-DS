/* LuaCorp - AGPLv3. Copyright (c) 2026 LuaCorp Contributors. See AGPLv3.txt. */
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Client.Guidebook.Richtext;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
namespace Content.Client._LuaM.Guidebook;
public sealed partial class GuideAcquisitions : BoxContainer, IDocumentTag
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    private readonly GuidebookCatalogClientSystem _system;
    private readonly LineEdit _search = new() { PlaceHolder = "Название предмета…", HorizontalExpand = true };
    private readonly OptionButton _sources = new() { HorizontalExpand = true };
    private readonly Label _status = new();
    private readonly BoxContainer _rows = new() { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true };
    private readonly Button _prev = new() { Text = "Назад" };
    private readonly Button _next = new() { Text = "Далее" };
    private static readonly string[] Factions = { "Авангард", "ТСФ", "СССП и ЧВК Вайпер", "Колосс Централ", "ММС", "МЦ", "Наёмники" };
    private string _kind = "armor";
    private string _faction = "";
    private int _page;
    public GuideAcquisitions()
    {
        IoCManager.InjectDependencies(this); _system = _entities.System<GuidebookCatalogClientSystem>();
        Orientation = LayoutOrientation.Vertical; HorizontalExpand = true; SeparationOverride = 6;
        _sources.AddItem("Все фракции", 0);
        for (var i = 0; i < Factions.Length; i++) _sources.AddItem(Factions[i] == "Наёмники" ? "Наёмники (доступно всем)" : Factions[i], i + 1);
        AddChild(_sources); AddChild(_search);
        var nav = new BoxContainer(); nav.AddChild(_prev); nav.AddChild(_status); nav.AddChild(_next);
        AddChild(nav); AddChild(_rows);
        _search.OnTextChanged += _ => { _page = 0; Rebuild(); };
        _sources.OnItemSelected += a => { _sources.SelectId(a.Id); _faction = a.Id == 0 ? "" : Factions[a.Id - 1]; _page = 0; Rebuild(); };
        _prev.OnPressed += _ => { _page--; Rebuild(); }; _next.OnPressed += _ => { _page++; Rebuild(); };
    }
    protected override void EnteredTree() { base.EnteredTree(); _system.Updated += Rebuild; _system.Request(); }
    protected override void ExitedTree() { _system.Updated -= Rebuild; base.ExitedTree(); }
    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        _kind = args.GetValueOrDefault("Kind", "armor");
        if (_kind == "uplink") { control = new GuideStorePreview(); return true; }
        control = this; Rebuild(); return true;
    }
    private new string Name(string id) => _prototypes.TryIndex<EntityPrototype>(id, out var p) ? p.Name : id;
    private void Rebuild()
    {
        _rows.RemoveAllChildren();
        if (_system.Catalog is not { } data) { _status.Text = "Загрузка…"; _system.Request(); return; }
        var entries = data.Acquisitions.Where(e => e.Kind == _kind && e.Offers.Any(o => MatchesFaction(o.Faction))
            && Name(e.PrototypeId).Contains(_search.Text.Trim(), StringComparison.CurrentCultureIgnoreCase))
            .GroupBy(e => e.IdentityKey.Length > 0 ? e.IdentityKey : e.PrototypeId)
            .OrderBy(g => Name(g.First().PrototypeId)).ToList();
        var pages = Math.Max(1, (entries.Count + 11) / 12); _page = Math.Clamp(_page, 0, pages - 1);
        _status.Text = $"{entries.Count} · {_page + 1}/{pages}";
        _prev.Disabled = _page == 0; _next.Disabled = _page + 1 == pages;
        var browser = new GuideCatalogList(); _rows.AddChild(browser);
        foreach (var group in entries.Skip(_page * 12).Take(12))
        {
            var entry = group.OrderByDescending(e => e.Stats.Any(s => s.Label == "Штатный выстрел")).First();
            if (!_prototypes.TryIndex<EntityPrototype>(entry.PrototypeId, out var proto)) continue;
            var offers = group.SelectMany(e => e.Offers).Where(o => MatchesFaction(o.Faction)).ToList();
            browser.AddEntry(proto.Name, string.Join(", ", offers.Select(o => FactionLabel(o.Faction)).Distinct()),
                _entities.System<SpriteSystem>().Frame0(proto), body =>
                {
                    if (entry.Kind == "armor")
                    {
                        var stats = new ArmorStats();
                        stats.TryParseTag(new() { ["Kind"] = proto.Components.ContainsKey("ArmorPlateItem") ? "plates" : "suits", ["Entities"] = proto.ID }, out _);
                        body.AddChild(stats);
                        if (entry.ShieldStats.Count > 0)
                            body.AddChild(new GuidePersonalShieldStats(entry.ShieldStats, proto));
                    }
                    else foreach (var stat in entry.Stats) body.AddChild(Text(stat.Label + ": " + stat.Value));
                    var sources = offers.Where(o => o.Method is "fabricator" or "store" or "vendor")
                        .Select(o => o.SourceName).Distinct().OrderBy(n => n).ToList();
                    if (sources.Count > 0) body.AddChild(Text("Получение: " + string.Join("; ", sources)));
                    if (entry.Kind is "implant" or "reactor")
                        foreach (var offer in offers.DistinctBy(o => (o.SourceName, o.Detail)))
                            body.AddChild(Text(offer.SourceName + ": " + offer.Detail));
                });
        }
        browser.SelectFirst();
    }
    private static string FactionLabel(string faction) => faction is "СССП" or "ЧВК Вайпер" ? "СССП и ЧВК Вайпер" : faction;
    private bool MatchesFaction(string faction) => _faction == "" || FactionLabel(faction) == _faction;
    private static RichTextLabel Text(string value)
    { var label = new RichTextLabel { HorizontalExpand = true }; label.SetMessage(value); return label; }
}
