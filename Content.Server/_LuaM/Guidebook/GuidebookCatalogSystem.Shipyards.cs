/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */
using System.Linq;
using Content.Shared._LuaM.Guidebook;
using Content.Shared._NF.Shipyard;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Server._LuaM.Guidebook;

public sealed partial class GuidebookCatalogSystem
{
    private List<GuidebookShipyardEntry> BuildShipyards(List<GuidebookMappedEntity> entities, List<GuidebookVesselEntry> vessels)
    {
        var result = new Dictionary<string, GuidebookShipyardEntry>();
        // Use mapped consoles first: carrier listings may be replaced on a particular ship.
        foreach (var entity in entities)
        {
            if (!_prototype.TryIndex<EntityPrototype>(entity.PrototypeId, out var prototype)
                || !prototype.Components.ContainsKey("ShipyardConsole"))
                continue;
            var entry = Yard(prototype, entity.Components);
            if (entry == null)
                continue;
            if (!result.TryGetValue(entry.Id, out var known))
                result[entry.Id] = known = entry;
            known.Locations[entity.MapId] = known.Locations.GetValueOrDefault(entity.MapId) + 1;
        }
        // Merge console aliases with identical catalogues, then merge identical player-facing names.
        var catalogues = result.Values.GroupBy(y => y.ConsoleType + ":" + string.Join(",", y.Vessels.Order()))
            .Select(Merge).ToList();
        return catalogues.GroupBy(y => y.Name.Trim(), StringComparer.CurrentCultureIgnoreCase).Select(Merge)
            .OrderBy(y => y.Name).ToList();

        GuidebookShipyardEntry Merge(IEnumerable<GuidebookShipyardEntry> entries)
        {
            var group = entries.ToList();
            var merged = group.OrderByDescending(y => y.Locations.Count).First();
            var types = group.SelectMany(y => y.ConsoleTypes.Count == 0 ? new[] { y.ConsoleType } : y.ConsoleTypes.AsEnumerable()).Distinct().ToList();
            var ships = group.SelectMany(y => y.Vessels).Distinct().Order().ToList();
            var places = group.SelectMany(y => y.Locations).GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.Sum(p => p.Value));
            merged.ConsoleTypes = types; merged.Vessels = ships; merged.Locations = places;
            return merged;
        }

        GuidebookShipyardEntry? Yard(EntityPrototype prototype, SequenceDataNode? overrides)
        {
            if (!prototype.TryComp<ActivatableUIComponent>(out var ui, EntityManager.ComponentFactory)
                || ui.Key is not ShipyardConsoleUiKey group)
                return null;
            var listing = prototype.TryComp<ShipyardListingComponent>(out var list, EntityManager.ComponentFactory)
                ? list.Shuttles.Select(id => id.Id).ToHashSet() : new HashSet<string>();
            if (overrides != null)
            foreach (var node in overrides.OfType<MappingDataNode>())
            {
                if (Scalar(node, "type") == "ShipyardListing" && Node(node, "shuttles") is SequenceDataNode ships)
                    listing = ships.OfType<ValueDataNode>().Select(n => n.Value).ToHashSet();
                if (Scalar(node, "type") == "ActivatableUI" && Scalar(node, "key") is { } key
                    && Enum.TryParse<ShipyardConsoleUiKey>(key.Split('.').Last(), out var changed))
                    group = changed;
            }
            var available = vessels.Where(v => listing.Contains(v.Id) || group != ShipyardConsoleUiKey.Custom && v.Group == group.ToString())
                .Select(v => v.Id).Order().ToList();
            if (available.Count == 0)
                return null;
            return new GuidebookShipyardEntry
            {
                Id = prototype.ID + ":" + string.Join(",", available),
                PrototypeId = prototype.ID,
                Name = prototype.Name,
                ConsoleType = group.ToString(),
                Vessels = available,
            };
        }
    }
}
