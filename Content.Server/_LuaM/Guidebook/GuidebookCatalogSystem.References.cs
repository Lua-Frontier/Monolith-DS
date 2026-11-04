/* LuaCorp - AGPLv3. Copyright (c) 2026 LuaCorp Contributors. See AGPLv3.txt. */
using System.IO;
using System.Linq;
using Content.Server.Worldgen;
using Content.Server.Worldgen.Prototypes;
using Content.Shared.CCVar;
using Content.Shared.EntityTable;
using Content.Shared.EntityTable.EntitySelectors;
using Content.Shared._LuaM.Guidebook;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Server._LuaM.Guidebook;

public sealed partial class GuidebookCatalogSystem
{
    [Dependency] private IConfigurationManager _configuration = default!;

    private IEnumerable<bool> BuildReferenceData(GuidebookCatalog result, List<GuidebookMappedEntity> mapped, HashSet<string> obtainable)
    {
        // Calibres belonging to the server's numeric ammunition families, with a
        // real acquisition chain. Generic upstream ammunition is not a fallback.
        result.Ammunition = obtainable.Where(id => _prototype.TryIndex<EntityPrototype>(id, out var p)
            && p.Components.ContainsKey("CartridgeAmmo") && _prototype.GetPrototypeData(p).TryGetValue("Tag", out var tags)
            && Values(Node(tags, "tags")).Any(t => (t.StartsWith("Cartridge") || t.StartsWith("Shell")) && t.Any(char.IsDigit)))
            .OrderBy(EquipmentName).ToList();
        result.Generators = obtainable.Where(id => _prototype.TryIndex<EntityPrototype>(id, out var p)
            && (p.Components.Keys.Any(k => k is "FuelGenerator" or "TegGenerator" or "NFSolarPanel")
                || p.Components.ContainsKey("PowerSupplier") && !p.Components.ContainsKey("PowerNetworkBattery")
                && !p.Components.ContainsKey("Battery") && _prototype.GetPrototypeData(p).TryGetValue("PowerSupplier", out var power)
                && Numeric(power, "supplyRate") > 0))
            .OrderBy(EquipmentName).ToList();
        foreach (var id in result.Generators) result.GeneratorStats[id] = GeneratorStats(_prototype.Index<EntityPrototype>(id));
        foreach (var group in mapped.Where(e => _prototype.TryIndex<EntityPrototype>(e.PrototypeId, out var p)
                     && p.Components.ContainsKey("PlantHolder")).GroupBy(e => e.MapId))
            result.Hydroponics[group.Key] = group.Select(e => e.PrototypeId).ToList();
        yield return true;

        var drones = new Dictionary<string, GuidebookDroneEntry>();
        var minerals = new Dictionary<string, GuidebookOreTable>();
        if (!_prototype.TryIndex<WorldgenConfigPrototype>(_configuration.GetCVar(CCVars.WorldgenConfig), out var config)) yield break;
        var biomeIds = config.Components.Values.Select(c => (MappingDataNode) _serialization.WriteValue(c.Component.GetType(), c.Component, alwaysWrite: true))
            .SelectMany(n => Values(Node(n, "biomes"))).Distinct().ToList();
        foreach (var biomeId in biomeIds)
        {
            if (!_prototype.TryIndex<BiomePrototype>(biomeId, out var biome) || biome.Abstract) continue;
            var area = biome.DistanceRange is { } distance ? $"{distance.X:0}–{distance.Y:0} м от центра" :
                biome.MinX.HasValue && biome.MaxX.HasValue && biome.MinY.HasValue && biome.MaxY.HasValue
                    ? $"X: {biome.MinX * WorldGen.ChunkSize}…{(biome.MaxX + 1) * WorldGen.ChunkSize} м; Y: {biome.MinY * WorldGen.ChunkSize}…{(biome.MaxY + 1) * WorldGen.ChunkSize} м" : "Дальний космос";
            var visited = new HashSet<string>();
            foreach (var component in biome.ChunkComponents.Values)
            {
                var node = (MappingDataNode) _serialization.WriteValue(component.Component.GetType(), component.Component, alwaysWrite: true);
                foreach (var id in References(node)) Walk(id, area, visited, "#aaaaaa");
            }
            yield return true;
        }
        foreach (var drone in drones.Values.OrderBy(d => d.Name))
        {
            if (!_mapLoader.TryReadFile(new(drone.MapPath), out var map)) continue;
            foreach (var group in map.Get<SequenceDataNode>("entities").OfType<MappingDataNode>())
            {
                var id = Scalar(group, "proto");
                // Only recorder containers physically placed on reachable drone
                // maps. Expedition rewards and the global loot prototype list
                // are deliberately never scanned.
                if (id == null || !id.StartsWith("ClothingBackpackDroneLoot")
                    || !_prototype.TryIndex<EntityPrototype>(id, out var p) || !p.Components.ContainsKey("SpawnItemsOnUse")) continue;
                drone.Loot[id] = group.Get<SequenceDataNode>("entities").Count;
            }
            result.MapNames.TryAdd(drone.MapPath, drone.Name);
            result.Drones.Add(drone);
            yield return true;
        }
        foreach (var family in minerals.Values.GroupBy(m => m.Id.Split("Mineral")[0]))
        {
            var name = family.FirstOrDefault(m => m.Grade == "Soft")?.Name ?? family.First().Name;
            foreach (var variant in family) variant.Name = name;
        }
        result.OreTables = minerals.Values.OrderBy(m => m.Name).ThenBy(m => m.Grade).ToList();

        void Walk(string id, string area, HashSet<string> visited, string color)
        {
            if (!visited.Add(id)) return;
            if (_prototype.TryIndex<EntityTablePrototype>(id, out var table))
            {
                if (id.Contains("Mineral") && !minerals.ContainsKey(id))
                {
                    var split = id.IndexOf("Mineral", StringComparison.Ordinal);
                    var family = id[..split];
                    var grade = id[(split + "Mineral".Length)..];
                    var oreTable = new GuidebookOreTable { Id = id, Name = family, Grade = grade, Color = color };
                    // Resolve the actual rock name from the table, instead of a
                    // fixed list of seven asteroid families.
                    var outcomes = Weighted(table.Table, 1, new());
                    var primary = outcomes.OrderByDescending(p => p.Value).FirstOrDefault();
                    if (primary.Key != null && _prototype.TryIndex<EntityPrototype>(primary.Key, out var primaryRock)) oreTable.Name = primaryRock.Name;
                    foreach (var (entity, probability) in outcomes)
                    {
                        if (!_prototype.TryIndex<EntityPrototype>(entity, out var rock)) continue;
                        var raw = _prototype.GetPrototypeData(rock);
                        var ore = raw.TryGetValue("OreVein", out var vein) ? Scalar(vein, "currentOre") : null;
                        if (ore == null && !rock.Components.ContainsKey("Item") && !rock.Components.ContainsKey("RandomSpawner") && !rock.Components.ContainsKey("EntityTableSpawner") && !rock.Components.ContainsKey("MobState"))
                        {
                            ore = "";
                            oreTable.Name = rock.Name;
                        }
                        ore ??= entity;
                        oreTable.Probabilities[ore] = oreTable.Probabilities.GetValueOrDefault(ore) + probability;
                    }
                    minerals[id] = oreTable;
                }
                foreach (var child in References(_serialization.WriteValue(table.Table.GetType(), table.Table, alwaysWrite: true))) Walk(child, area, visited, color);
                return;
            }
            if (!_prototype.TryIndex<EntityPrototype>(id, out var proto)) return;
            var data = _prototype.GetPrototypeData(proto);
            if (data.TryGetValue("IFF", out var iff)) color = Scalar(iff, "color") ?? color;
            if (data.TryGetValue("GridSpawner", out var spawner) && Scalar(spawner, "path") is { } path)
            {
                if (!drones.TryGetValue(path, out var drone))
                    drones[path] = drone = new() { PrototypeId = id, MapPath = path,
                        Name = Path.GetFileNameWithoutExtension(path).Replace('_', ' ') };
                if (!drone.Areas.Contains(area)) drone.Areas.Add(area);
                return;
            }
            foreach (var name in new[] { "SimpleFloorPlanPopulator", "RandomSpawner", "EntityTableSpawner" })
                if (data.TryGetValue(name, out var node))
                    foreach (var child in References(node)) Walk(child, area, visited, color);
        }
    }

    private List<GuidebookCatalogStat> GeneratorStats(EntityPrototype prototype)
    {
        var data = new Dictionary<string, MappingDataNode>();
        foreach (var name in new[] { "FuelGenerator", "SolidFuelGeneratorAdapter", "ChemicalFuelGeneratorAdapter", "NFSolarPanel", "PowerSupplier", "UpgradePowerSupplier", "MaterialStorage", "SolutionContainerManager" })
            if (prototype.Components.TryGetValue(name, out var component))
                data[name] = (MappingDataNode) _serialization.WriteValue(component.Component.GetType(), component.Component, alwaysWrite: true);
        var stats = new List<GuidebookCatalogStat>();
        void Add(string label, string value) => stats.Add(new() { Label = label, Value = value });
        void Power(string label, double watts) => Add(label, Format(watts / 1000) + " кВт");
        var scale = Numeric(Component(data, "UpgradePowerSupplier"), "actualScalar", 1);
        if (data.ContainsKey("FuelGenerator"))
        {
            var fuel = Component(data, "FuelGenerator");
            Power("Максимальная мощность", Numeric(fuel, "maxTargetPower") * scale);
            Power("Экономичный режим", Numeric(fuel, "optimalPower") * scale);
            Add("Расход в экономичном режиме", Format(Numeric(fuel, "optimalBurnRate")) + " ед. топлива/с");
            Add("Рост расхода", "(настройка мощности / экономичная настройка)^" + Format(Numeric(fuel, "fuelEfficiencyConstant")));
        }
        else if (data.TryGetValue("NFSolarPanel", out var solar)) Power("Максимальная мощность", Numeric(solar, "maxSupply"));
        else if (prototype.Components.ContainsKey("TegGenerator")) Add("Мощность", "Зависит от температур, газа и циркуляции двух контуров");
        else if (data.TryGetValue("PowerSupplier", out var power)) Power("Мощность", Numeric(power, "supplyRate") * scale);
        if (data.TryGetValue("SolidFuelGeneratorAdapter", out var solid) && Scalar(solid, "fuelMaterial") is { } material)
        {
            Add("Топливо", _prototype.TryIndex<Content.Shared.Materials.MaterialPrototype>(material, out var m) ? Loc.GetString(m.Name) : material);
            Add("Топливных единиц из 100 ед. материала", Format(100 * Numeric(solid, "multiplier", 1)));
        }
        if (data.TryGetValue("ChemicalFuelGeneratorAdapter", out var chemical) && Node(chemical, "reagents") is MappingDataNode reagents)
            Add("Топливо", string.Join(", ", reagents.Keys.Select(r => _prototype.TryIndex<Content.Shared.Chemistry.Reagent.ReagentPrototype>(r, out var reagent) ? reagent.LocalizedName : r)));
        if (data.TryGetValue("MaterialStorage", out var storage) && Node(storage, "storageLimit") is ValueDataNode limit &&
            double.TryParse(limit.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var capacity))
            Add("Вместимость материала", Format(capacity) + " ед.");
        if (data.TryGetValue("ChemicalFuelGeneratorAdapter", out var adapter) &&
            data.TryGetValue("SolutionContainerManager", out var manager) && Node(manager, "solutions") is MappingDataNode solutions &&
            solutions.TryGetValue(Scalar(adapter, "solution") ?? "tank", out var tank) && tank is MappingDataNode tankData &&
            Node(tankData, "maxVol") is ValueDataNode volume)
            Add("Топливный бак", volume.Value + " ед.");
        return stats;
    }

    private static IEnumerable<string> References(DataNode node)
    {
        if (node is SequenceDataNode list)
        {
            foreach (var child in list) foreach (var reference in References(child)) yield return reference;
        }
        else if (node is MappingDataNode map)
        {
            if (Scalar(map, "chance") == "0") yield break;
            if (map.TryGet("prob", out ValueDataNode? prob) && double.TryParse(prob.Value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var chance) && chance <= 0) yield break;
            if (map.TryGet("weight", out ValueDataNode? weight) && float.TryParse(weight.Value, out var value) && value <= 0) yield break;
            foreach (var (key, child) in map)
            {
                if (key is "id" or "tableId" && child is ValueDataNode scalar) yield return scalar.Value;
                else if (key is "prototypes" or "rarePrototypes")
                {
                    if (key == "rarePrototypes" && Scalar(map, "rareChance") == "0") continue;
                    foreach (var reference in Values(child)) yield return reference;
                }
                else if (child is not ValueDataNode) foreach (var reference in References(child)) yield return reference;
            }
        }
    }

    private Dictionary<string, float> Weighted(EntityTableSelector selector, float probability, HashSet<string> tables)
    {
        var result = new Dictionary<string, float>();
        probability *= (float) selector.Prob;
        if (probability <= 0) return result;
        if (selector is EntSelector entity) result[entity.Id.Id] = probability;
        else if (selector is NestedSelector nested && tables.Add(nested.TableId.Id) && _prototype.TryIndex(nested.TableId, out var table))
        {
            result = Weighted(table.Table, probability, tables);
            tables.Remove(nested.TableId.Id);
        }
        else if (selector is GroupSelector group)
        {
            var total = group.Children.Sum(c => Math.Max(0, c.Weight));
            if (total <= 0) return result;
            foreach (var child in group.Children)
                foreach (var (id, weight) in Weighted(child, probability * Math.Max(0, child.Weight) / total, tables))
                    result[id] = result.GetValueOrDefault(id) + weight;
        }
        return result;
    }
}
