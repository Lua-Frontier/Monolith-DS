using System.Diagnostics.CodeAnalysis;
using Content.Shared.Construction;
using Content.Shared.Construction.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Shared._LuaM.Construction;

/// <summary>
/// Localizes the names and descriptions of the construction recipes, the prototypes only have English text.
/// A recipe takes them from the locale keys <c>construction-ID</c> and <c>construction-ID-desc</c>,
/// then from the entity it builds, and keeps the text of the prototype if there is neither.
/// </summary>
public sealed partial class ConstructionLocalizationSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
        Localize();
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<ConstructionPrototype>() ||
            args.WasModified<ConstructionGraphPrototype>() ||
            args.WasModified<EntityPrototype>())
            Localize();
    }

    private void Localize()
    {
        var fromEntity = new List<(ConstructionPrototype Recipe, EntityPrototype Target)>();

        foreach (var recipe in _proto.EnumeratePrototypes<ConstructionPrototype>())
        {
            var key = $"construction-{recipe.ID}";
            TryGetTarget(recipe, out var target);

            if (Loc.TryGetString(key, out var name))
                recipe.Name = name;
            else if (target != null)
            {
                recipe.Name = target.Name;
                fromEntity.Add((recipe, target));
            }

            if (Loc.TryGetString($"{key}-desc", out var desc))
                recipe.Description = desc;
            else if (target != null)
                recipe.Description = target.Description;
        }

        // -- части одной мебели часто зовутся одинаково и различаются только суффиксом, а меню строительства ищет кнопки по имени
        var counts = new Dictionary<string, int>();
        foreach (var recipe in _proto.EnumeratePrototypes<ConstructionPrototype>())
        {
            counts[recipe.Name] = counts.GetValueOrDefault(recipe.Name) + 1;
        }

        foreach (var (recipe, target) in fromEntity)
        {
            if (counts[recipe.Name] > 1 && !string.IsNullOrEmpty(target.EditorSuffix))
                recipe.Name = $"{recipe.Name} ({target.EditorSuffix})";
        }
    }

    /// <summary>
    /// The entity the recipe builds, if its target node has a fixed one.
    /// </summary>
    private bool TryGetTarget(ConstructionPrototype recipe, [NotNullWhen(true)] out EntityPrototype? target)
    {
        target = null;
        if (!_proto.TryIndex<ConstructionGraphPrototype>(recipe.Graph, out var graph) ||
            !graph.Nodes.TryGetValue(recipe.TargetNode, out var node))
            return false;

        var id = node.Entity.GetId(null, null, new GraphNodeEntityArgs(EntityManager))
                 ?? GetRawNodeEntity(recipe.Graph, recipe.TargetNode);
        return id != null && _proto.TryIndex(id, out target);
    }

    // -- поле entity у узла графа serverOnly, на клиенте его можно достать только из YAML
    private string? GetRawNodeEntity(string graphId, string nodeId)
    {
        if (!_proto.TryGetMapping<ConstructionGraphPrototype>(graphId, out var mapping) ||
            !mapping.TryGet<SequenceDataNode>("graph", out var nodes))
            return null;

        foreach (var raw in nodes)
        {
            if (raw is MappingDataNode node &&
                node.TryGet<ValueDataNode>("node", out var name) && name.Value == nodeId &&
                node.TryGet<ValueDataNode>("entity", out var entity))
                return entity.Value;
        }

        return null;
    }
}
