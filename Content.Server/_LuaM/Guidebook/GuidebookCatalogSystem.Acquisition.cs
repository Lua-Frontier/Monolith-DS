/* LuaCorp - AGPLv3. Copyright (c) 2026 LuaCorp Contributors. See AGPLv3.txt. */
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Content.Shared._LuaM.Guidebook;
using Content.Shared.Lathe.Prototypes;
using Content.Shared.Research.Prototypes;
using Content.Shared.Store;
using Content.Shared.VendingMachines;
using Content.Shared.Roles;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Research.Components;
using Content.Server.Store.Conditions;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Server._LuaM.Guidebook;
public sealed partial class GuidebookCatalogSystem
{
    // The guide lists supply chains for the combat factions, not every item with a
    // resistance or melee component. Civilian map equipment isn't a combat source.
    private static readonly Dictionary<string, string> GuideStores = new()
    {
        ["VendingMachinePDVUplink"] = "Авангард",
        ["VendingMachineTSFMCUplink"] = "ТСФ",
        ["VendingMachineMMCUplink"] = "ММС",
        ["VendingMachineSyndieContrabandPOI"] = "Контравенд",
    };

    private static string FactionFor(string path, string source = "")
    {
        var device = source.ToLowerInvariant();
        if (source is "StationRepresentative" or "SecurityGuard" || device.StartsWith("csec")) return "Колосс Централ";
        if (device.Contains("viper") || device.StartsWith("vg")) return "ЧВК Вайпер";
        if (device.Contains("ussp")) return "СССП";
        if (device.Contains("pdv") || device.StartsWith("pirate")) return "Авангард";
        if (device.Contains("tsf") || new[] { "Sheriff", "Bailiff", "Deputy", "SeniorOfficer", "Brigmedic" }.Contains(source)) return "ТСФ";
        if (device.Contains("mmc") || device.Contains("mieyo")) return "ММС";
        var value = path.ToLowerInvariant();
        if (value.Contains("/pdv/") || value.Contains("pdvhelios")) return "Авангард";
        if (value.Contains("/tsf/") || value.Contains("tsfmchalcyon") || value.Contains("arcturus")) return "ТСФ";
        if (value.Contains("/ussp/") || value.Contains("camelot")) return "СССП";
        if (value.Contains("/vg/")) return "ЧВК Вайпер";
        if (value.Contains("/mmc/") || value.Contains("hokkaido")) return "ММС";
        if (value.Contains("medical") || device.StartsWith("mdmedic") || device.Contains("directorofcare")) return "МЦ";
        if (value.Contains("caelestinus") || value.Contains("/csec/")) return "Колосс Централ";
        return "Наёмники";
    }

    private static readonly Dictionary<string, string> FactionCompanies = new()
    { ["USSP"] = "СССП", ["TheViperGroup"] = "ЧВК Вайпер", ["TSF"] = "ТСФ", ["TSFCivilian"] = "ТСФ",
      ["PDV"] = "Авангард", ["PDVCivilian"] = "Авангард", ["MMC"] = "ММС" };

    private static readonly HashSet<string> AcquisitionComponents = new()
    { "Lathe", "ResearchServer", "TechnologyDatabase", "VendingMachine", "Store", "ContainerFill", "StorageFill", "SuitStorage", "Clothing", "Gun", "Armor", "ArmorPlateItem", "CartridgeAmmo", "BallisticAmmoProvider", "SeedExtractor", "PlantHolder", "FuelGenerator", "NFSolarPanel", "TegGenerator", "Rtg", "PowerSupplier", "Generator", "Turbine" };

    private List<GuidebookAcquisitionEntry> BuildAcquisitions(List<GuidebookMappedEntity> entities, Dictionary<string, List<string>> storeListings,
        HashSet<string>? obtainable = null)
    {
        var currentFaction = "Наёмники";
        var found = new Dictionary<string, GuidebookAcquisitionEntry>();
        var processed = new HashSet<string>();
        var sourceData = new Dictionary<string, IReadOnlyDictionary<string, MappingDataNode>>();
        var technologies = _prototype.EnumeratePrototypes<TechnologyPrototype>().Where(t => !t.Hidden).ToList();
        var techByRecipe = technologies.SelectMany(t => t.RecipeUnlocks.Select(r => (r.Id, Tech: t)))
            .GroupBy(t => t.Id).ToDictionary(g => g.Key, g => g.Select(t => t.Tech).ToList());
        var listings = _prototype.EnumeratePrototypes<ListingPrototype>().ToList();
        var disciplines = new Dictionary<string, HashSet<string>>();
        var startingRecipes = new Dictionary<string, HashSet<string>>();
        var mapDisciplines = new Dictionary<string, HashSet<string>>();
        foreach (var mapped in entities)
        {
            if (!_prototype.TryIndex<EntityPrototype>(mapped.PrototypeId, out var source)
                || !source.Components.TryGetValue("TechnologyDatabase", out var dbEntry)
                || dbEntry.Component is not TechnologyDatabaseComponent database) continue;
            if (!mapDisciplines.TryGetValue(mapped.MapId, out var onMap)) mapDisciplines[mapped.MapId] = onMap = new();
            onMap.UnionWith(database.SupportedDisciplines.Select(d => d.Id));
            foreach (var faction in FactionsFor(source, mapped.MapId))
            {
                if (!disciplines.TryGetValue(faction, out var supported)) disciplines[faction] = supported = new();
                supported.UnionWith(database.SupportedDisciplines.Select(d => d.Id));
                if (!startingRecipes.TryGetValue(faction, out var unlocked)) startingRecipes[faction] = unlocked = new();
                unlocked.UnionWith(database.UnlockedRecipes.Select(r => r.Id));
                var instance = mapped.Components?.OfType<MappingDataNode>().FirstOrDefault(n => Scalar(n, "type") == "TechnologyDatabase");
                if (instance != null)
                {
                    supported.UnionWith(Values(Node(instance, "supportedDisciplines")));
                    onMap.UnionWith(Values(Node(instance, "supportedDisciplines")));
                    unlocked.UnionWith(Values(Node(instance, "unlockedRecipes")));
                }
            }
        }
        foreach (var mapped in entities)
        {
            if (!_prototype.TryIndex<EntityPrototype>(mapped.PrototypeId, out var source)) continue;
            foreach (var faction in FactionsFor(source, mapped.MapId))
            {
                currentFaction = faction;
                var raw = Data(source);
                // Source instances with identical inventory configuration only need one traversal.
                var signature = currentFaction + source.ID + (mapped.Components == null ? "" : string.Join("", mapped.Components.OfType<MappingDataNode>()
                    .Where(n => Scalar(n, "type") is { } name && AcquisitionComponents.Contains(name)).Select(CanonicalNode)));
                if (!processed.Add(signature)) continue;
                var components = raw.ToDictionary(p => p.Key, p => p.Value.Copy());
                if (mapped.Components != null)
                foreach (var node in mapped.Components.OfType<MappingDataNode>())
                {
                    if (Scalar(node, "type") is not { } type || !AcquisitionComponents.Contains(type)) continue;
                    var combined = components.GetValueOrDefault(type) ?? new MappingDataNode();
                    foreach (var (key, value) in node) { if (key == "type") continue; combined.Remove(key); combined.Add(key, value.Copy()); }
                    components[type] = combined;
                }
                Visit(source.ID, source.ID, source.Name, "map", "На действующей карте: " + mapped.MapName, new HashSet<string>());
                if (components.TryGetValue("Lathe", out var lathe))
                foreach (var field in new[] { "staticPacks", "dynamicPacks" })
                foreach (var packId in Values(Node(lathe, field)))
                {
                    if (!_prototype.TryIndex<LatheRecipePackPrototype>(packId, out var pack)) continue;
                    foreach (var recipeId in pack.Recipes)
                    {
                        if (!_prototype.TryIndex(recipeId, out var recipe) || recipe.Result is not { } product) continue;
                        var research = field == "dynamicPacks" ? techByRecipe.GetValueOrDefault(recipe.ID) : null;
                        if (field == "dynamicPacks")
                        {
                            var supported = new HashSet<string>();
                            if (disciplines.TryGetValue(currentFaction, out var own)) supported.UnionWith(own);
                            if (mapDisciplines.TryGetValue(mapped.MapId, out var local)) supported.UnionWith(local);
                            if (supported.Count == 0 && disciplines.TryGetValue("Наёмники", out var common)) supported.UnionWith(common);
                            research = research?.Where(t => supported != null && t.GetAllDisciplines().Any(d => supported.Contains(d.Id))).ToList();
                            var initiallyUnlocked = startingRecipes.GetValueOrDefault(currentFaction)?.Contains(recipe.ID) == true;
                            if (!initiallyUnlocked && (research == null || research.Count == 0)) continue;
                        }
                        Visit(product.Id, source.ID, source.Name, "fabricator", "", new HashSet<string>());
                    }
                }
                if (components.TryGetValue("VendingMachine", out var vending) && Scalar(vending, "pack") is { } inventoryId
                    && _prototype.TryIndex<VendingMachineInventoryPrototype>(inventoryId, out var inventory))
                {
                    foreach (var (id, count) in inventory.StartingInventory.Where(p => p.Value > 0))
                        Visit(id.Id, source.ID, source.Name, "vendor", "В автомате: " + count + " шт.; цена указана в его меню", new HashSet<string>());
                    if (inventory.ContrabandInventory != null)
                        foreach (var (id, count) in inventory.ContrabandInventory.Where(p => p.Value > 0))
                            Visit(id.Id, source.ID, source.Name, "vendor", "Скрытый ассортимент автомата", new HashSet<string>());
                }
                Store(source, components);
            }
        }
        // All four documented stores are included even if their portable counterpart
        // is supplied inside a locker instead of being placed directly on a map.
        foreach (var id in GuideStores.Keys)
            if (_prototype.TryIndex<EntityPrototype>(id, out var storeSource))
                Store(storeSource, Data(storeSource));
        var factionDepartments = new HashSet<string>
        { "PMC", "NFSecurity", "NFPirate", "Pirate", "TSFMC", "PDV", "UnionOfSovietSocialistPlanets", "ViperGroup", "MieyoManufacturingConcern" };
        var factionRoles = _prototype.EnumeratePrototypes<DepartmentPrototype>()
            .Where(d => factionDepartments.Contains(d.ID)).SelectMany(d => d.Roles).Select(r => r.Id).ToHashSet();
        // These factions use legacy police/pirate job IDs despite their current lobby names.
        factionRoles.UnionWith(new[] { "Contractor", "Mercenary", "StationRepresentative", "SecurityGuard", "Sheriff", "Bailiff", "TsfMarsoc", "SeniorOfficer", "Brigmedic", "Deputy", "TsfEngineer", "TsfBorg", "DirectorOfCare", "MdMedic",
            "PirateCaptain", "PirateFirstMate", "PDVTarkhan", "PDVDenasvar", "PDVInfiltrator", "Pirate", "PdvBorg" });
        foreach (var roleId in factionRoles)
        {
            if (!_prototype.TryIndex<JobPrototype>(roleId, out var role)) continue;
            currentFaction = FactionFor("", role.ID);
            void Gear(StartingGearPrototype gear)
            {
                foreach (var product in gear.Equipment.Values.Concat(gear.Inhand).Concat(gear.Storage.Values.SelectMany(x => x)))
                    Visit(product.Id, "role:" + role.ID, Loc.GetString(role.Name), "loadout", "Стартовое снаряжение", new HashSet<string>());
            }
            if (role.StartingGear is { } gearId && _prototype.TryIndex(gearId, out var gear)) Gear(gear);
            if (!_prototype.TryIndex<RoleLoadoutPrototype>("Job" + role.ID, out var choices)) continue;
            var visitedGroups = new HashSet<string>();
            void Group(string id)
            {
                if (!visitedGroups.Add(id) || !_prototype.TryIndex<LoadoutGroupPrototype>(id, out var group)) return;
                foreach (var loadoutId in group.Loadouts.Concat(group.Fallbacks).Distinct())
                {
                    if (!_prototype.TryIndex(loadoutId, out var loadout)) continue;
                    if (loadout.StartingGear is { } preset && _prototype.TryIndex(preset, out var presetGear)) Gear(presetGear);
                    foreach (var product in loadout.Equipment.Values.Concat(loadout.Inhand).Concat(loadout.Storage.Values.SelectMany(x => x)))
                        Visit(product.Id, "role:" + role.ID, Loc.GetString(role.Name), "loadout", "Выбор экипировки в лобби", new HashSet<string>());
                }
                foreach (var child in group.Subgroups) Group(child.Id);
            }
            foreach (var group in choices.Groups) Group(group.Id);
        }
        // Common stock is a category of its own, even when identical items are issued
        // by a faction. Contravend loot alone must not reclassify exclusive faction gear.
        foreach (var group in found.Values.GroupBy(e => e.IdentityKey))
        {
            var offers = group.SelectMany(e => e.Offers).ToList();
            if (group.Any(e => e.PrototypeId is "ClothingHeadHelmetBombSuit" or "ClothingOuterSuitBomb")
                || offers.Any(o => o.Faction == "Наёмники" && o.Method is "fabricator" or "vendor" or "loadout"))
            {
                foreach (var offer in offers) offer.Faction = "Наёмники";
            }
            else if (offers.Any(o => o.Faction != "Наёмники"))
            {
                // A captured faction item in Contravend or a ruin is still faction equipment.
                // Preserve that acquisition source without creating a common-stock duplicate.
                var factions = offers.Where(o => o.Faction != "Наёмники").Select(o => o.Faction).Distinct().ToList();
                foreach (var entry in group)
                foreach (var offer in entry.Offers.Where(o => o.Faction == "Наёмники").ToList())
                {
                    entry.Offers.Remove(offer);
                    foreach (var faction in factions)
                        entry.Offers.Add(new() { SourceId = offer.SourceId, SourceName = offer.SourceName, Method = offer.Method,
                            Detail = offer.Detail, CategoryId = offer.CategoryId, CategoryName = offer.CategoryName, Faction = faction });
                }
            }
        }
        return found.Values.OrderBy(e => EquipmentName(e.PrototypeId)).ToList();

        IReadOnlyDictionary<string, MappingDataNode> Data(EntityPrototype proto)
        {
            if (!sourceData.TryGetValue(proto.ID, out var data)) sourceData[proto.ID] = data = _prototype.GetPrototypeData(proto);
            return data;
        }
        IEnumerable<string> FactionsFor(EntityPrototype source, string path)
        {
            if (Data(source).TryGetValue("CompanyAccessReader", out var reader) && Scalar(reader, "inverted") != "true")
            {
                var groups = Values(Node(reader, "requiredCompanies")).Where(FactionCompanies.ContainsKey).Select(c => FactionCompanies[c]).Distinct().ToArray();
                if (groups.Length > 0) return groups;
            }
            return new[] { FactionFor(path, source.ID) };
        }
        void Store(EntityPrototype source, IReadOnlyDictionary<string, MappingDataNode> data)
        {
            if (!GuideStores.TryGetValue(source.ID, out var sourceName)) return;
            if (!data.TryGetValue("Store", out var store)) return;
            var faction = sourceName == "Контравенд" ? "Наёмники" : sourceName;
            var stock = storeListings.TryGetValue(source.ID, out var existingStock) ? existingStock : storeListings[source.ID] = new();
            var categories = Values(Node(store, "categories")).ToHashSet();
            foreach (var listing in listings.Where(l => l.Categories.Any(c => categories.Contains(c.Id))))
            {
                if (listing.Conditions?.OfType<StoreWhitelistCondition>().Any(c =>
                    c.Whitelist != null && !MatchesWhitelist(source, c.Whitelist) || c.Blacklist != null && MatchesWhitelist(source, c.Blacklist)) == true) continue;
                if (!stock.Contains(listing.ID)) stock.Add(listing.ID);
                if (listing.ProductEntity is not { } product) continue;
                var price = string.Join(" + ", listing.OriginalCost.Select(c => c.Value + " " +
                    (_prototype.TryIndex(c.Key, out var currency) && Loc.TryGetString(currency.DisplayName, out var term) ? term : c.Key.Id)));
                foreach (var categoryId in listing.Categories.Where(c => categories.Contains(c.Id)))
                {
                    var categoryName = _prototype.TryIndex(categoryId, out var category)
                        ? Loc.GetString(category.Name) : categoryId.Id;
                    Visit(listing.ProductEntity!.Value.Id, source.ID, sourceName, "store", price,
                        new HashSet<string>(), categoryId.Id, categoryName, faction);
                }
            }
        }
        bool MatchesWhitelist(EntityPrototype source, EntityWhitelist whitelist)
        {
            var checks = new List<bool>();
            if (whitelist.Components != null) checks.AddRange(whitelist.Components.Select(source.Components.ContainsKey));
            if (whitelist.Tags != null)
            {
                var tags = Data(source).TryGetValue("Tag", out var tagData) ? Values(Node(tagData, "tags")).ToHashSet() : new HashSet<string>();
                checks.AddRange(whitelist.Tags.Select(t => tags.Contains(t.Id)));
            }
            return checks.Count > 0 && (whitelist.RequireAll ? checks.All(x => x) : checks.Any(x => x));
        }
        void Visit(string id, string sourceId, string sourceName, string method, string detail, HashSet<string> visited,
            string categoryId = "", string categoryName = "", string? faction = null)
        {
            faction ??= currentFaction;
            if (!visited.Add(id) || !_prototype.TryIndex<EntityPrototype>(id, out var item)) return;
            obtainable?.Add(id);
            var combatClothing = id.StartsWith("ClothingOuterHardsuit") || id.StartsWith("ClothingOuterArmor") || id.StartsWith("ClothingHeadHelmet");
            var armor = !item.Components.ContainsKey("MobState") &&
                ((combatClothing && item.Components.ContainsKey("Armor")) || item.Components.ContainsKey("ArmorPlateItem") || item.Components.ContainsKey("PersonalShield"));
            var weapon = item.Components.ContainsKey("Item") && !item.Components.ContainsKey("MobState") && !item.Components.ContainsKey("ShipGunType")
                && !id.StartsWith("WeaponCase", StringComparison.Ordinal)
                && (item.Components.ContainsKey("Gun") || (id.StartsWith("Weapon") && item.Components.ContainsKey("MeleeWeapon")));
            if (armor || weapon)
            {
                if (!found.TryGetValue(id, out var entry)) found[id] = entry = new GuidebookAcquisitionEntry
                { PrototypeId = id, Kind = armor ? "armor" : weapon ? "weapon" : "item",
                    IdentityKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(item.Name + "|" + string.Join("|", Data(item)
                        .Where(p => p.Key.Contains("Ammo") || p.Key.Contains("Projectile") || p.Key.Contains("Shield") || new[] { "Armor", "ArmorPlateItem", "Gun", "MeleeWeapon", "Battery", "ClothingSpeedModifier", "Sprite" }.Contains(p.Key))
                        .OrderBy(p => p.Key).Select(p => p.Key + CanonicalNode(p.Value)))))) };
                if (!entry.Offers.Any(o => o.SourceId == sourceId && o.Method == method && o.Detail == detail && o.CategoryId == categoryId && o.Faction == faction))
                    entry.Offers.Add(new() { SourceId = sourceId, SourceName = sourceName, Method = method, Detail = detail,
                        CategoryId = categoryId, CategoryName = categoryName, Faction = faction });
                if (weapon && entry.Stats.Count == 0 && item.Components.ContainsKey("Gun"))
                {
                    AddHandheldStats(item, entry.Stats);
                }
            }
            var raw = Data(item);
            if (raw.TryGetValue("BallisticAmmoProvider", out var ammunition) && Scalar(ammunition, "proto") is { } cartridge && cartridge != "null")
                Visit(cartridge, sourceId, sourceName, method, detail, visited, categoryId, categoryName, faction);
            // Container contents are reachable products, not a prototype-wide spawn-menu list.
            if (raw.TryGetValue("ContainerFill", out var fill) && Node(fill, "containers") is MappingDataNode containers)
                foreach (var (_, contents) in containers)
                    foreach (var child in Values(contents)) Visit(child, sourceId, sourceName, method, detail + "; в комплекте «" + item.Name + "»", visited, categoryId, categoryName, faction);
            if (raw.TryGetValue("StorageFill", out var storage) && Node(storage, "contents") is SequenceDataNode contentsList)
                foreach (var child in contentsList.OfType<MappingDataNode>())
                    if (Numeric(child, "prob", 1) > 0 && (Numeric(child, "amount", 1) > 0 || Numeric(child, "maxAmount") > 0)
                        && Scalar(child, "id") is { } childId) Visit(childId, sourceId, sourceName, method, detail + "; в комплекте «" + item.Name + "»", visited, categoryId, categoryName, faction);
            if (raw.TryGetValue("SuitStorage", out var suits))
                foreach (var field in new[] { "suit", "helmet", "suitProto", "helmetProto" })
                    if (Scalar(suits, field) is { } child) Visit(child, sourceId, sourceName, method, detail, visited, categoryId, categoryName, faction);
            if (id != sourceId && method != "store" && raw.ContainsKey("Store")) Store(item, raw);
        }
    }
    private static IEnumerable<string> Values(Robust.Shared.Serialization.Markdown.DataNode? node)
        => node is SequenceDataNode list ? list.OfType<ValueDataNode>().Select(n => n.Value) : Enumerable.Empty<string>();
}
