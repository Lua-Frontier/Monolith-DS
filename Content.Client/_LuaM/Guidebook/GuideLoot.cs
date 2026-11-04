/* LuaCorp - AGPLv3. Copyright (c) 2026 LuaCorp Contributors. See AGPLv3.txt. */
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using Content.Client.Guidebook.Richtext;
using Content.Shared.EntityTable;
using Content.Shared.EntityTable.EntitySelectors;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Utility;
namespace Content.Client._LuaM.Guidebook;
public sealed class GuideLoot : BoxContainer, IDocumentTag
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IEntityManager _entities = default!;
    public GuideLoot() { IoCManager.InjectDependencies(this); Orientation = LayoutOrientation.Vertical; HorizontalExpand = true; SeparationOverride = 6; }
    public bool TryParseTag(Dictionary<string, string> args, [NotNullWhen(true)] out Control? control)
    {
        control = this;
        if (args.TryGetValue("Table", out var tableId) && _prototypes.TryIndex<EntityTablePrototype>(tableId, out var table) && table.Table is GroupSelector group)
        {
            var total = group.Children.Sum(c => c.Weight);
            foreach (var child in group.Children.OfType<EntSelector>().OrderByDescending(c => c.Weight))
                Row(child.Id.Id, (100 * child.Weight / total).ToString("0.##", CultureInfo.CurrentCulture) + "% среди выбранных пород");
            return true;
        }
        foreach (var id in args.GetValueOrDefault("Entities", "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!_prototypes.TryIndex<EntityPrototype>(id, out var proto)) continue;
            var raw = _prototypes.GetPrototypeData(proto);
            var price = raw.TryGetValue("StaticPrice", out var p) && p.TryGet("price", out ValueDataNode? value) ? value.Value + " кр. базовой стоимости" : "Раскрыть состав";
            var header = new GuideCatalogHeader(proto.Name, price, _entities.System<SpriteSystem>().Frame0(proto));
            var body = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true, Visible = false, Margin = new Thickness(8) };
            header.OnPressed += _ =>
            {
                if (body.ChildCount == 0 && raw.TryGetValue("SpawnItemsOnUse", out var spawn) && spawn.TryGet("items", out SequenceDataNode? items))
                foreach (var item in items.OfType<MappingDataNode>())
                {
                    if (!item.TryGet("id", out ValueDataNode? childId)) continue;
                    var chance = item.TryGet("prob", out ValueDataNode? prob) && double.TryParse(prob.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var chanceValue) ? chanceValue : 1;
                    var count = item.TryGet("amount", out ValueDataNode? amount) ? amount.Value : "1";
                    if (chance <= 0) continue;
                    var text = (chance >= 1 ? "Гарантировано" : (chance * 100).ToString("0.#") + "%") + " · ×" + count;
                    var label = new RichTextLabel { HorizontalExpand = true };
                    label.SetMessage(FormattedMessage.FromUnformatted((_prototypes.TryIndex<EntityPrototype>(childId.Value, out var child) ? child.Name : "Предмет") + " — " + text));
                    body.AddChild(label);
                }
                body.Visible = !body.Visible; header.Expanded = body.Visible;
            };
            AddChild(header); AddChild(body);
        }
        return true;
    }
    private void Row(string id, string text)
    {
        if (!_prototypes.TryIndex<EntityPrototype>(id, out var proto)) return;
        var header = new GuideCatalogHeader(proto.Name, text, _entities.System<SpriteSystem>().Frame0(proto)) { Expandable = false };
        AddChild(header);
    }
}
