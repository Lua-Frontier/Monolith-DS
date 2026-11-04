/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using Content.Client.Guidebook.Controls;
using Content.Client.Guidebook.Richtext;
using Content.Shared._LuaM.Guidebook;
using Robust.Client.Graphics;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._LuaM.Guidebook;

/// <summary>Shipyards first; short comparisons first; technical details loaded on demand.</summary>
public sealed class GuideCatalog : BoxContainer, IDocumentTag
{
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    private readonly GuidebookCatalogClientSystem _system;
    private readonly LineEdit _search = new() { PlaceHolder = "Поиск по названию…", HorizontalExpand = true };
    private readonly Label _status = new();
    private readonly BoxContainer _rows = new() { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 6 };
    private readonly Button _previous = new() { Text = "←" };
    private readonly Button _next = new() { Text = "→" };
    private readonly OptionButton _yards = new() { HorizontalExpand = true, Visible = false };
    private readonly List<string> _yardIds = new();
    private string _kind = "ships";
    private string? _yard;
    private int _page;
    private const int PageSize = 12;

    public GuideCatalog()
    {
        IoCManager.InjectDependencies(this);
        _system = _entities.System<GuidebookCatalogClientSystem>();
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        SeparationOverride = 6;
        AddChild(_yards);
        AddChild(_search);
        var navigation = new BoxContainer { SeparationOverride = 6 };
        navigation.AddChild(_previous);
        navigation.AddChild(_status);
        navigation.AddChild(_next);
        var refresh = new Button { Text = "Обновить" };
        refresh.OnPressed += _ => _system.Request(force: true);
        navigation.AddChild(refresh);
        AddChild(navigation);
        AddChild(_rows);
        _search.OnTextChanged += _ => { _page = 0; Rebuild(); };
        _previous.OnPressed += _ => { _page--; Rebuild(); };
        _next.OnPressed += _ => { _page++; Rebuild(); };
        _yards.OnItemSelected += args =>
        {
            _yard = args.Id == 0 ? null : _yardIds[args.Id - 1];
            _page = 0;
            Rebuild();
        };
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();
        _system.Updated += Rebuild;
        _system.Request();
    }

    protected override void ExitedTree()
    {
        _system.Updated -= Rebuild;
        base.ExitedTree();
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        _kind = args.GetValueOrDefault("Kind", "ships");
        if (_kind == "ships") { control = new GuideShipyardPreview(); return true; }
        control = this;
        Rebuild();
        return true;
    }

    private static RichTextLabel Text(string value, bool bold = false)
    {
        var result = new RichTextLabel { HorizontalExpand = true, Margin = new Thickness(4) };
        result.SetMessage(bold
            ? FormattedMessage.FromMarkupPermissive("[bold]" + FormattedMessage.EscapeText(value) + "[/bold]")
            : FormattedMessage.FromUnformatted(value));
        return result;
    }

    private string Name(string id) => _prototypes.TryIndex<EntityPrototype>(id, out var prototype) ? prototype.Name : id;
    private bool Matches(params string[] values) => string.IsNullOrWhiteSpace(_search.Text)
        || values.Any(v => v.Contains(_search.Text.Trim(), StringComparison.CurrentCultureIgnoreCase));
    private static string Money(int value) => value.ToString("N0", CultureInfo.GetCultureInfo("ru-RU")) + " кр.";

    private void Rebuild()
    {
        foreach (var child in _rows.Children.ToArray()) child.Dispose();
        if (_system.Catalog is not { } data)
        {
            _status.Text = "Загрузка…";
            _previous.Disabled = _next.Disabled = true;
            return;
        }
        _yards.Visible = _kind == "ships";
        var equipment = data.Equipment.ToDictionary(e => e.Id);
        if (_kind == "ships")
        {
            _yards.Clear();
            _yards.AddItem("Все верфи", 0);
            _yardIds.Clear();
            foreach (var yard in data.Shipyards.OrderBy(y => y.Name))
            { _yardIds.Add(yard.Id); _yards.AddItem(yard.Name, _yardIds.Count); }
            var index = _yard == null ? -1 : _yardIds.IndexOf(_yard);
            _yards.SelectId(index + 1);
        }
        var selected = data.Shipyards.FirstOrDefault(y => y.Id == _yard);
        if (selected != null)
        {
            _rows.AddChild(Text(selected.Name, true));
            AddLocations(_rows, selected.Locations, data, "Где находится консоль");
        }
        var offered = data.Shipyards.SelectMany(y => y.Vessels).ToHashSet();
        var entries = _kind == "ships"
            ? data.Vessels.Where(v => offered.Contains(v.Id) && (selected == null || selected.Vessels.Contains(v.Id)) && Matches(v.Name, v.Id,
                string.Join(" ", v.Equipment.Keys.Where(equipment.ContainsKey).Select(id => Name(equipment[id].PrototypeId)))))
                .OrderBy(v => v.Name).Cast<object>().ToList()
            : data.Equipment.Where(e => e.Kind == _kind && Matches(Name(e.PrototypeId),
                string.Join(" ", e.Locations.Keys.Select(p => data.MapNames.GetValueOrDefault(p, p)))))
                .GroupBy(e => Name(e.PrototypeId)).OrderBy(g => g.Key).Select(g => g.ToList()).Cast<object>().ToList();
        var pages = Math.Max(1, (entries.Count + PageSize - 1) / PageSize);
        _page = Math.Clamp(_page, 0, pages - 1);
        _status.Text = $"{entries.Count} записей · {_page + 1}/{pages}";
        _previous.Disabled = _page == 0;
        _next.Disabled = _page == pages - 1;
        var browser = new GuideCatalogList();
        _rows.AddChild(browser);
        foreach (var entry in entries.Skip(_page * PageSize).Take(PageSize))
        {
            if (entry is GuidebookVesselEntry ship)
            {
                var subtitle = (ship.Purchasable ? Money(ship.Price) : "Только ваучер") + " · " +
                    string.Join(", ", ship.Engines.Select(e => Loc.TryGetString("shipyard-console-engine-" + e, out var term) ? term : e));
                browser.AddEntry(ship.Name, subtitle, null, body => FillVessel(body, ship, data, equipment, selected?.ConsoleTypes));
            }
            else
            {
                var variants = (List<GuidebookEquipmentEntry>) entry;
                var item = variants[0];
                var subtitle = string.Join(" · ", GuidebookCatalogPresentation.Summary(item).Take(2).Select(s => s.Value));
                var icon = _prototypes.TryIndex<EntityPrototype>(item.PrototypeId, out var prototype) ? _entities.System<SpriteSystem>().Frame0(prototype) : null;
                browser.AddEntry(Name(item.PrototypeId), subtitle, icon, body =>
                {
                    if (variants.Count == 1) FillEquipment(body, item, data);
                    else
                    {
                        foreach (var group in variants.SelectMany(v => GuidebookCatalogPresentation.Summary(v)).GroupBy(stat => stat.Label))
                            Row(body, group.Key, string.Join(" / ", group.Select(stat => stat.Value).Distinct()));
                        body.AddChild(Text("Подробности отличаются по месту установки:"));
                        foreach (var variant in variants)
                        {
                            var places = variant.Locations.Keys.Select(p => data.MapNames.GetValueOrDefault(p, p)).ToList();
                            var caption = string.Join(", ", places.Take(2)) + (places.Count > 2 ? " и ещё " + (places.Count - 2) : "");
                            Expand(body, caption, details => FillEquipment(details, variant, data), variant.PrototypeId,
                                string.Join(" · ", GuidebookCatalogPresentation.Summary(variant).Take(2).Select(s => s.Value)));
                        }
                    }
                });
            }
        }
        browser.SelectFirst();
    }

    private string EquipmentTitle(GuidebookEquipmentEntry item)
    {
        var value = item.Stats.FirstOrDefault(s => s.Label == "Вычислительная мощность")?.Value;
        if (value != null) return Name(item.PrototypeId) + " · " + value;
        // Preserve different map configurations as distinct visible entries.
        var damage = item.Stats.FirstOrDefault(s => s.Label == "Множитель прямого урона")?.Value;
        return Name(item.PrototypeId) + (damage is not (null or "1") ? " · урон ×" + damage : "");
    }

    private void FillVessel(BoxContainer body, GuidebookVesselEntry vessel, GuidebookCatalog data,
        Dictionary<string, GuidebookEquipmentEntry> equipment, List<string>? consoleTypes)
    {
        string Term(string type, string value) => Loc.TryGetString($"shipyard-console-{type}-{value}", out var localized) ? localized : value;
        Row(body, "Цена", vessel.Purchasable ? Money(vessel.Price) : "Только ваучер");
        Row(body, "Размер / класс", Term("category", vessel.Size) + " · " + string.Join(", ", vessel.Classes.Select(c => Term("class", c))));
        Row(body, "Энергетика", string.Join(", ", vessel.Engines.Select(e => Term("engine", e))));
        if (vessel.RequireCrew) Row(body, "Экипаж", "Требуется");
        if (vessel.ActiveLimit > 0) Row(body, "Лимит в секторе", vessel.ActiveLimit.ToString());
        var vouchers = data.Vouchers.Where(v => vessel.Vouchers.Contains(v.PrototypeId)
            && (consoleTypes == null || consoleTypes.Count == 0 || consoleTypes.Contains(v.ConsoleType))).ToList();
        if (vouchers.Count > 0)
            Expand(body, "Ваучеры этой верфи (" + vouchers.Count + ")", list =>
            {
                foreach (var voucher in vouchers)
                {
                    list.AddChild(Text(Name(voucher.PrototypeId), true));
                    Row(list, "Покупок с карты", voucher.Uses.ToString());
                    if (voucher.CooldownSeconds > 0)
                        Row(list, "Между покупками", TimeSpan.FromSeconds(voucher.CooldownSeconds).ToString(@"hh\:mm\:ss"));
                    Row(list, "После исчерпания", voucher.DestroyOnEmpty ? "Карта исчезает" : "Карта остаётся");
                }
            });
        var preview = new Button { Text = "План корабля", HorizontalExpand = true, Margin = new Thickness(0, 6),
            Disabled = !GuidebookMapRendering.Enabled, ToolTip = "Просмотр планов временно отключён." };
        preview.OnPressed += _ => _entities.System<GuidebookMapPreviewSystem>().Open(vessel.MapPath, vessel.Name);
        body.AddChild(preview);
        if (vessel.Equipment.Count > 0)
        {
            body.AddChild(Text("Вооружение и СУО", true));
            foreach (var (id, count) in vessel.Equipment.OrderBy(e => e.Key))
                if (equipment.TryGetValue(id, out var item))
                    Expand(body, count + " × " + EquipmentTitle(item), details => FillEquipment(details, item, data));
        }
        if (!string.IsNullOrWhiteSpace(vessel.Description))
            Expand(body, "Описание", details => details.AddChild(Text(vessel.Description)));
    }

    private void FillEquipment(BoxContainer body, GuidebookEquipmentEntry item, GuidebookCatalog data)
    {
        body.AddChild(new GuideEntityEmbed(item.PrototypeId, false, false));
        foreach (var stat in GuidebookCatalogPresentation.Summary(item)) Row(body, stat.Label, stat.Value);
        var ammo = item.Stats.Where(s => s.Ammunition).GroupBy(s => s.Label[..s.Label.IndexOf(": ", StringComparison.Ordinal)]).ToList();
        if (ammo.Count > 0)
            Expand(body, "Боеприпасы и эффекты (" + ammo.Count + ")", list =>
            {
                foreach (var group in ammo)
                    Expand(list, group.Key, details =>
                    {
                        foreach (var stat in group.Where(GuidebookCatalogPresentation.Useful))
                            Row(details, stat.Label[(stat.Label.IndexOf(": ", StringComparison.Ordinal) + 2)..], GuidebookCatalogPresentation.DamageValue(stat.Value));
                    });
            });
        Expand(body, "Дополнительные параметры", details =>
        {
            foreach (var stat in item.Stats.Where(s => !s.Ammunition && GuidebookCatalogPresentation.Useful(s)))
                Row(details, stat.Label, stat.Value);
        });
        AddLocations(body, item.Locations, data, "Места установки (" + item.Locations.Count + ")");
    }

    private void AddLocations(BoxContainer body, Dictionary<string, int> locations, GuidebookCatalog data, string caption)
    {
        if (locations.Count == 0) return;
        Expand(body, caption, details =>
        {
            foreach (var (path, count) in locations.OrderBy(p => data.MapNames.GetValueOrDefault(p.Key, p.Key)))
                Row(details, "×" + count, data.MapNames.GetValueOrDefault(path, path), 40);
        });
    }

    private static void Row(BoxContainer body, string label, string value, float keyWidth = 135)
    {
        var row = new BoxContainer { HorizontalExpand = true, SeparationOverride = 8 };
        var key = Text(label, true);
        key.HorizontalExpand = false;
        key.SetWidth = keyWidth;
        row.AddChild(key);
        row.AddChild(Text(value));
        body.AddChild(row);
    }

    private void Expand(BoxContainer parent, string caption, Action<BoxContainer> fill, string? prototypeId = null, string? subtitle = null)
    {
        var button = Heading(caption, prototypeId, subtitle);
        var body = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true,
            Visible = false, Margin = new Thickness(6, 4, 0, 10), SeparationOverride = 3 };
        var filled = false;
        button.OnPressed += _ =>
        {
            if (!filled) { fill(body); filled = true; }
            body.Visible = !body.Visible;
            button.Pressed = body.Visible;
            button.Expanded = body.Visible;
        };
        parent.AddChild(button); parent.AddChild(body);
    }

    private GuideCatalogHeader Heading(string caption, string? prototypeId = null, string? subtitle = null)
    {
        var icon = prototypeId != null && _prototypes.TryIndex<EntityPrototype>(prototypeId, out var prototype)
            ? _entities.System<SpriteSystem>().Frame0(prototype) : null;
        return new GuideCatalogHeader(caption, subtitle, icon);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _system.Updated -= Rebuild;
        base.Dispose(disposing);
    }
}
