/* LuaCorp - AGPLv3. See AGPLv3.txt for details. */
using System.Linq;
using Content.Shared._LuaM.Guidebook;
using Content.Shared._Mono.PersonalShield;
using Content.Shared.Damage.Prototypes;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._LuaM.Guidebook;

/// <summary>The same image-left / colored-stats-right layout as ArmorStats.</summary>
public sealed class GuidePersonalShieldStats : BoxContainer
{
    public GuidePersonalShieldStats(IReadOnlyList<GuidebookCatalogStat> stats, EntityPrototype? suit = null)
    {
        HorizontalExpand = true;
        SeparationOverride = 8;
        var visuals = suit != null && suit.TryGetComponent<PersonalShieldComponent>(out var field)
            ? field.Visuals : new PersonalShieldVisuals();
        var image = new BoxContainer { Orientation = LayoutOrientation.Vertical, SetWidth = 170,
            Margin = new Thickness(5), VerticalAlignment = VAlignment.Top };
        image.AddChild(new GuideShieldPreview(visuals));
        image.AddChild(Label("Встроенный персональный щит"));
        AddChild(image);
        var body = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true, Margin = new Thickness(4) };
        AddChild(body);
        foreach (var stat in stats.Where(s => !s.Label.StartsWith("Расход заряда:")
            && s.Label != "Повторное полное развёртывание" && s.Label != "Питание при непрерывной работе"))
            body.AddChild(Line(stat.Label, stat.Value));
        body.AddChild(Label("Расход запаса поля:", true));
        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        var costs = stats.Where(s => s.Label.StartsWith("Расход заряда:")).ToList();
        var used = new HashSet<string>();
        foreach (var id in new[] { "Brute", "Burn" })
        {
            if (!prototypes.TryIndex<DamageGroupPrototype>(id, out var group)) continue;
            var names = group.DamageTypes.Select(t => prototypes.Index(t).LocalizedName).ToHashSet();
            var rows = costs.Where(s => names.Contains(s.Label.Substring("Расход заряда: ".Length))).ToList();
            if (rows.Count == 0) continue;
            foreach (var row in rows) used.Add(row.Label);
            body.AddChild(Line(id == "Brute" ? "Механические" : "Ожоговые", string.Join("; ", rows.Select(Cost))));
        }
        var other = costs.Where(s => !used.Contains(s.Label)).ToList();
        if (other.Count > 0) body.AddChild(Line("Остальные", string.Join("; ", other.Select(Cost))));
        var details = stats.Where(s => s.Label == "Повторное полное развёртывание" || s.Label == "Питание при непрерывной работе").ToList();
        if (details.Count > 0)
        {
            var toggle = new Button { Text = "Расчёт восстановления и питания", ToggleMode = true };
            var extra = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true, Visible = false };
            foreach (var stat in details) extra.AddChild(Line(stat.Label, stat.Value));
            toggle.OnPressed += _ => extra.Visible = toggle.Pressed;
            body.AddChild(toggle); body.AddChild(extra);
        }
    }
    private static string Cost(GuidebookCatalogStat stat) => stat.Label.Substring("Расход заряда: ".Length) + " " + stat.Value;
    private static RichTextLabel Line(string name, string value)
    {
        var label = new RichTextLabel { HorizontalExpand = true };
        label.SetMessage(FormattedMessage.FromMarkupOrThrow("- [color=yellow]" + FormattedMessage.EscapeText(name)
            + "[/color]: [color=lightblue]" + FormattedMessage.EscapeText(value) + "[/color]"));
        return label;
    }
    private static RichTextLabel Label(string text, bool bold = false)
    {
        var label = new RichTextLabel { HorizontalExpand = true };
        label.SetMessage(bold ? FormattedMessage.FromMarkupOrThrow("[bold]" + FormattedMessage.EscapeText(text) + "[/bold]")
            : FormattedMessage.FromUnformatted(text));
        return label;
    }
}
