/* LuaCorp - AGPLv3. Copyright (c) 2026 LuaCorp Contributors. See AGPLv3.txt. */
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using Content.Client.Guidebook.Richtext;
using Content.Shared.Atmos.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._LuaM.Guidebook;

/// <summary>Prices from the same prototypes used by cargo valuation.</summary>
public sealed partial class GuideCommodityPrices : BoxContainer, IDocumentTag
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    private readonly OptionButton _type = new() { HorizontalExpand = true };
    private readonly LineEdit _search = new() { PlaceHolder = "Название вещества…", HorizontalExpand = true };
    private readonly BoxContainer _rows = new() { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true };
    private readonly Label _pageLabel = new();
    private readonly Button _prev = new() { Text = "←" };
    private readonly Button _next = new() { Text = "→" };
    private int _page;
    private bool _reagents;

    public GuideCommodityPrices()
    {
        IoCManager.InjectDependencies(this);
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        SeparationOverride = 6;
        _type.AddItem("Газы — кр./моль", 0);
        _type.AddItem("Химикаты — кр./ед.", 1);
        AddChild(_type); AddChild(_search);
        var nav = new BoxContainer();
        nav.AddChild(_prev); nav.AddChild(_pageLabel); nav.AddChild(_next);
        AddChild(nav); AddChild(_rows);
        _type.OnItemSelected += a => { _type.SelectId(a.Id); _reagents = a.Id == 1; _page = 0; Rebuild(); };
        _search.OnTextChanged += _ => { _page = 0; Rebuild(); };
        _prev.OnPressed += _ => { _page--; Rebuild(); };
        _next.OnPressed += _ => { _page++; Rebuild(); };
    }

    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    { control = this; Rebuild(); return true; }

    private void Rebuild()
    {
        _rows.RemoveAllChildren();
        var prices = _reagents
            ? _prototypes.EnumeratePrototypes<ReagentPrototype>().Select(r => (Name: r.LocalizedName, Price: (double) r.PricePerUnit, Id: r.ID))
            : _prototypes.EnumeratePrototypes<GasPrototype>().Select(g => (Name: Loc.GetString(g.Name), Price: (double) g.PricePerMole, Id: g.ID));
        var query = _search.Text.Trim();
        var rows = prices.Where(p => p.Price > 0
            && (p.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) || p.Id.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(p => p.Price).ThenBy(p => p.Name).ToList();
        var pages = Math.Max(1, (rows.Count + 11) / 12);
        _page = Math.Clamp(_page, 0, pages - 1);
        _pageLabel.Text = $"{rows.Count} веществ · {_page + 1}/{pages}";
        _prev.Disabled = _page == 0; _next.Disabled = _page + 1 == pages;
        foreach (var row in rows.Skip(_page * 12).Take(12))
        {
            var label = new RichTextLabel { HorizontalExpand = true, Margin = new Thickness(4, 5) };
            label.SetMessage(row.Name + ": " + row.Price.ToString("0.####", CultureInfo.CurrentCulture) + (_reagents ? " кр./ед." : " кр./моль"));
            _rows.AddChild(label);
        }
    }
}
