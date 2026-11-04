/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Content.Shared._LuaM.Guidebook;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Explosion;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.Server._LuaM.Guidebook;

public sealed partial class GuidebookCatalogSystem
{
    // Serialize actual component instances with defaults, rather than guessing defaults from YAML.
    // Map changes are composed with the inherited prototype using the component's own serializers.
    private Dictionary<string, MappingDataNode> EquipmentComponents(EntityPrototype prototype, SequenceDataNode? overrides = null)
    {
        var result = new Dictionary<string, MappingDataNode>();
        foreach (var (name, entry) in prototype.Components)
        {
            if (EquipmentComponentNames.Contains(name))
                result[name] = (MappingDataNode) _serialization.WriteValue(entry.Component.GetType(), entry.Component, alwaysWrite: true);
        }
        // Slots and startingItem are deliberately read-only datafields and are omitted when writing a
        // component. The resolved source mapping retains that map-init information without spawning.
        if (_prototype.GetPrototypeData(prototype).TryGetValue("ItemSlots", out var slotData))
            result["ItemSlots"] = slotData.Copy();

        if (overrides == null)
            return result;

        foreach (var node in overrides)
        {
            if (node is not MappingDataNode mapping || Scalar(mapping, "type") is not { } name || !EquipmentComponentNames.Contains(name))
                continue;

            var registration = EntityManager.ComponentFactory.GetRegistration(name);
            var child = mapping.Copy();
            child.Remove("type");
            if (!result.TryGetValue(name, out var parent))
            {
                var defaults = EntityManager.ComponentFactory.GetComponent(registration);
                parent = (MappingDataNode) _serialization.WriteValue(registration.Type, defaults, alwaysWrite: true);
            }
            // A map's entity references (for example BallisticAmmoProvider.entities) are local IDs,
            // not standalone EntityUid values. Composing preserves these opaque fields without
            // resolving references or running map init. The prototype/default parent already supplies
            // typed defaults; only the displayed scalar configuration is read below.
            result[name] = (MappingDataNode) _serialization.PushComposition(registration.Type, parent, child);
        }

        return result;
    }

    private static readonly HashSet<string> EquipmentComponentNames =
    [
        "ShipGunType", "ShipGunClass", "Gun", "FireControllable", "FireControlServer", "Battery",
        "ApcPowerReceiver", "ApcPowerReceiverBattery", "BatterySelfRecharger", "SpaceArtillery",
        "BallisticAmmoProvider", "BasicEntityAmmoProvider", "ProjectileBatteryAmmoProvider", "HitscanBatteryAmmoProvider",
        "MagazineAmmoProvider", "ChamberMagazineAmmoProvider", "RevolverAmmoProvider", "ItemSlots", "ContainerFill", "CartridgeAmmo",
        "Projectile", "ProjectileSpread", "HitscanBasicDamage", "HitscanBasicRaycast", "HitscanSpawnEntity",
        "TimedDespawn", "TargetSeeking", "TargetGuided", "Explosive", "EmpOnTrigger", "ProjectileGrenade",
        "HardpointAnchorableOnly", "RadarConsole", "Tag", "TriggerOnProximity", "OnUseTimerTrigger",
        "LightningOnTrigger", "ProjectileKnockback"
    ];

    private List<GuidebookEquipmentEntry> BuildEquipment(List<GuidebookMappedEntity> entities)
    {
        var entries = new Dictionary<string, GuidebookEquipmentEntry>();
        var configurations = new Dictionary<string, GuidebookEquipmentEntry>();
        var prototypeCache = new Dictionary<string, Dictionary<string, MappingDataNode>>();
        Dictionary<string, MappingDataNode> ReadPrototype(string id)
        {
            if (prototypeCache.TryGetValue(id, out var cached))
                return cached;
            var data = _prototype.TryIndex<EntityPrototype>(id, out var prototype)
                ? EquipmentComponents(prototype)
                : new Dictionary<string, MappingDataNode>();
            prototypeCache[id] = data;
            return data;
        }

        // Resolve compatible ammunition by the exact tag/component whitelist, not a hardcoded ammunition list.
        var candidates = _prototype.EnumeratePrototypes<EntityPrototype>()
            .Where(p => !p.Abstract && (p.Components.ContainsKey("CartridgeAmmo") || p.Components.ContainsKey("BallisticAmmoProvider") || p.Components.ContainsKey("Projectile")))
            .OrderBy(p => p.ID, StringComparer.Ordinal).ToList();

        foreach (var entity in entities)
        {
            if (!_prototype.TryIndex<EntityPrototype>(entity.PrototypeId, out var prototype))
                continue;
            if (!prototype.Components.ContainsKey("ShipGunType") && !prototype.Components.ContainsKey("FireControlServer") &&
                !(entity.Components?.OfType<MappingDataNode>().Any(c => Scalar(c, "type") is "ShipGunType" or "FireControlServer") ?? false))
                continue;
            var configurationKey = entity.PrototypeId + "\n" + (entity.Components == null ? "" : string.Join("\n",
                entity.Components.OfType<MappingDataNode>().Where(c => Scalar(c, "type") is { } name && EquipmentComponentNames.Contains(name))
                    .Select(CanonicalNode).OrderBy(x => x, StringComparer.Ordinal)));
            if (configurations.TryGetValue(configurationKey, out var known))
            {
                known.Locations[entity.MapId] = known.Locations.GetValueOrDefault(entity.MapId) + 1;
                continue;
            }
            var components = entity.Components == null ? ReadPrototype(entity.PrototypeId) : EquipmentComponents(prototype, entity.Components);
            if (!components.ContainsKey("ShipGunType") && !components.ContainsKey("FireControlServer"))
                continue;

            var stats = new List<GuidebookCatalogStat>();
            void Add(string label, string value) => stats.Add(new GuidebookCatalogStat { Label = label, Value = value });
            void AddAmmo(string label, string value) => stats.Add(new GuidebookCatalogStat { Label = label, Value = value, Ammunition = true });
            void Number(string component, string field, string label, string unit = "")
            {
                if (components.TryGetValue(component, out var data) && Scalar(data, field) is { } value && value != "null")
                    Add(label, ReadableScalar(value) + unit);
            }

            var server = components.ContainsKey("FireControlServer");
            var kind = server ? "server" : Scalar(Component(components, "ShipGunType"), "shipType")?.ToLowerInvariant() ?? "ballistic";
            if (kind is not ("server" or "energy" or "missile"))
                kind = "ballistic";

            if (server)
            {
                Number("FireControlServer", "processingPower", "Вычислительная мощность", " ед.");
                var control = Component(components, "FireControlServer");
                Add("Лимит консолей", Boolean(control, "enforceMaxConsoles") ? Scalar(control, "maxConsoles") ?? "1" : "не ограничен сервером");
            }
            else
            {
                var gun = Component(components, "Gun");
                var electrical = components.ContainsKey("ProjectileBatteryAmmoProvider") || components.ContainsKey("HitscanBatteryAmmoProvider");
                var supplyProvider = Component(components, components.ContainsKey("BallisticAmmoProvider") ? "BallisticAmmoProvider" : "BasicEntityAmmoProvider");
                var selfCharging = Boolean(Component(components, "BatterySelfRecharger"), "autoRecharge");
                var mains = components.ContainsKey("SpaceArtillery") && Numeric(Component(components, "SpaceArtillery"), "powerChargeRate") > 0;
                Add("Пополнение", electrical
                    ? mains ? "Патроны не нужны; заряжается от электросети" : selfCharging ? "Патроны не нужны; заряд восстанавливается сам" : "Патроны не нужны; требуется зарядка батареи"
                    : Boolean(supplyProvider, "infiniteUnspawned") ? "Встроенный неиссякаемый боезапас" : components.ContainsKey("ItemSlots") ? "Расходует патроны; сменный магазин" : "Расходует патроны; ручная загрузка");
                if (electrical && mains) Add("Восстановление", Format(Numeric(Component(components, "SpaceArtillery"), "powerChargeRate") / 1000) + " кДж/с при наличии питания");
                else if (electrical && selfCharging) Add("Восстановление", Format(Numeric(Component(components, "BatterySelfRecharger"), "autoRechargeRate") / 1000) + " кДж/с");

                var gunClass = Scalar(Component(components, "ShipGunClass"), "shipClass");
                if (gunClass != null)
                    Add("Класс", ReadableScalar(gunClass));
                Add("Нагрузка на СУО", (gunClass switch { "Superlight" => 1, "Light" => 3, "Medium" => 6, "Heavy" => 9, "Superheavy" => 12, _ => 0 }) + " ед.");
                Number("Gun", "fireRate", "Темп вне очереди", " выстр./с");
                Number("Gun", "selectedMode", "Режим при установке");
                if (gun.TryGetValue("availableModes", out var modes))
                    Add("Доступные режимы", DisplayNode(modes));
                if ((Scalar(gun, "selectedMode") ?? "").Contains("Burst") || DisplayNode(Node(gun, "availableModes")).Contains("Burst"))
                {
                    Number("Gun", "shotsPerBurst", "Выстрелов в очереди");
                    Number("Gun", "burstFireRate", "Темп внутри очереди", " выстр./с");
                    Number("Gun", "burstCooldown", "Дополнительная пауза очереди", " с");
                    var burstRate = Numeric(gun, "burstFireRate");
                    var count = Numeric(gun, "shotsPerBurst");
                    if (burstRate > 0)
                    {
                        var cycle = count / burstRate + Numeric(gun, "burstCooldown");
                        Add("Теоретический цикл очереди", Format(cycle) + " с");
                        if (cycle > 0)
                            Add("Теоретический средний темп очередями", Format(count / cycle) + " выстр./с");
                    }
                }
                Number("FireControllable", "fireCooldown", "Пауза команд СУО", " с");
                Number("Gun", "damageModifier", "Множитель прямого урона");
                Number("Gun", "minAngle", "Минимальный разброс");
                Number("Gun", "maxAngle", "Максимальный разброс");
                Number("Gun", "angleIncrease", "Рост разброса за выстрел");
                Number("Gun", "angleDecay", "Спад разброса за секунду");
                if (components.ContainsKey("HardpointAnchorableOnly"))
                {
                    Number("HardpointAnchorableOnly", "class", "Обязательное крепление: тип");
                    Number("HardpointAnchorableOnly", "size", "Обязательное крепление: размер");
                }
                else
                    Add("Крепление", "специальный хардпоинт не требуется");

                var ammo = new HashSet<string>(StringComparer.Ordinal);
                foreach (var providerName in new[] { "BallisticAmmoProvider", "BasicEntityAmmoProvider", "ProjectileBatteryAmmoProvider", "HitscanBatteryAmmoProvider" })
                {
                    if (!components.TryGetValue(providerName, out var provider))
                        continue;
                    if (Scalar(provider, "proto") is { } initial && initial != "null")
                    {
                        ammo.Add(initial);
                        Add("Начальный боеприпас", EquipmentName(initial));
                    }
                    if (providerName is "BallisticAmmoProvider" or "BasicEntityAmmoProvider")
                    {
                        Add("Вместимость", Boolean(provider, "infiniteUnspawned") ? "бесконечная" : Scalar(provider, "capacity") ?? "не ограничена");
                        if (Scalar(provider, "count") is { } count && count != "null")
                            Add("Начальный запас", count);
                        else if (Scalar(provider, "proto") is { } loaded && loaded != "null")
                            Add("Начальный запас при заполнении карты", Scalar(provider, "capacity") ?? "не ограничен");
                    }
                    if (provider.TryGetValue("whitelist", out var whitelist) && whitelist is MappingDataNode list)
                        foreach (var candidate in candidates.Where(p => MatchesWhitelist(p, list, ReadPrototype)))
                            ammo.Add(candidate.ID);
                    if (providerName.EndsWith("BatteryAmmoProvider"))
                    {
                        var cost = Numeric(provider, "fireCost");
                        Add("Энергия боеприпаса за выстрел", Format(cost) + " Дж");
                        if (cost > 0 && components.TryGetValue("Battery", out var battery))
                            Add("Запас по стоимости боеприпаса (без доп. расхода)", Format(Math.Floor(Numeric(battery, "maxCharge") / cost)) + " выстр.");
                    }
                }
                if (Component(components, "ItemSlots").TryGetValue("slots", out var slotsNode) && slotsNode is MappingDataNode slots)
                {
                    foreach (var (slotName, slotNode) in slots)
                    {
                        if (slotNode is not MappingDataNode slot || !slotName.Contains("magazine"))
                            continue;
                        if (Scalar(slot, "startingItem") is { } magazine && magazine != "null")
                        {
                            ammo.Add(magazine);
                            Add("Начальный магазин", EquipmentName(magazine));
                        }
                        if (slot.TryGetValue("whitelist", out var whitelist) && whitelist is MappingDataNode list)
                            foreach (var candidate in candidates.Where(p => MatchesWhitelist(p, list, ReadPrototype)))
                                ammo.Add(candidate.ID);
                    }
                }
                if (ammo.Count > 0)
                    Add("Совместимые боеприпасы / магазины", string.Join(", ", ammo.OrderBy(x => x, StringComparer.Ordinal).Select(EquipmentName)));
                var visited = new HashSet<string>();
                foreach (var ammoId in ammo.OrderBy(x => x, StringComparer.Ordinal))
                    DescribeAmmunition(ammoId, "", Numeric(gun, "damageModifier", 1), Numeric(gun, "projectileSpeed"), ReadPrototype, visited, AddAmmo);
            }

            Number("Battery", "maxCharge", "Ёмкость батареи", " Дж");
            Number("Battery", "startingCharge", "Начальный заряд", " Дж");
            Number("SpaceArtillery", "powerUsePassive", "Пассивное питание", " Вт");
            Number("SpaceArtillery", "powerChargeRate", "Зарядка от сети", " Дж/с");
            Number("SpaceArtillery", "powerUseActive", "Доп. расход за событие стрельбы", " Дж");
            if (!components.ContainsKey("SpaceArtillery"))
            {
                Number("BatterySelfRecharger", "autoRecharge", "Автозарядка");
                Number("BatterySelfRecharger", "autoRechargeRate", "Темп автозарядки", " Дж/с");
            }
            Number("BatterySelfRecharger", "autoRechargePauseTime", "Пауза автозарядки после расхода", " с");
            Number("ApcPowerReceiver", "powerLoad", "Нагрузка приёмника питания", " Вт");
            Number("ApcPowerReceiverBattery", "idleLoad", "Потребление от батареи", " Вт");
            Number("ApcPowerReceiverBattery", "batteryRechargeRate", "Скорость зарядки батареи", " Дж/с");
            Number("ApcPowerReceiverBattery", "batteryRechargeEfficiency", "КПД зарядки батареи");
            Number("RadarConsole", "maxRange", "Дальность радара", " м");

            // Display-equivalent map variants share a row. Relevant changed values produce a separate stable ID.
            var signature = entity.PrototypeId + "\n" + string.Join("\n", stats.Select(s => s.Label + "=" + s.Value));
            var id = entity.PrototypeId + "-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature)))[..12];
            if (!entries.TryGetValue(id, out var entry))
            {
                entry = new GuidebookEquipmentEntry { Id = id, PrototypeId = entity.PrototypeId, Kind = kind, Stats = stats };
                entries.Add(id, entry);
            }
            entry.Locations[entity.MapId] = entry.Locations.GetValueOrDefault(entity.MapId) + 1;
            configurations[configurationKey] = entry;
        }

        return entries.Values.OrderBy(e => e.Kind).ThenBy(e => e.PrototypeId, StringComparer.Ordinal).ThenBy(e => e.Id, StringComparer.Ordinal).ToList();
    }

    private void DescribeAmmunition(string id, string prefix, double multiplier, double gunSpeed,
        Func<string, Dictionary<string, MappingDataNode>> read, HashSet<string> visited, Action<string, string> add)
    {
        if (!visited.Add(id))
            return;
        var components = read(id);
        prefix += EquipmentName(id) + ": ";
        void Field(string component, string field, string label, string unit = "")
        {
            if (components.TryGetValue(component, out var node) && Scalar(node, field) is { } value && value != "null")
                add(prefix + label, ReadableScalar(value) + unit);
        }
        var ballistic = Component(components, "BallisticAmmoProvider");
        if (components.ContainsKey("BallisticAmmoProvider"))
        {
            if (Boolean(ballistic, "infiniteUnspawned"))
                add(prefix + "вместимость магазина", "бесконечная");
            else
                Field("BallisticAmmoProvider", "capacity", "вместимость магазина", " шт.");
            if (Scalar(ballistic, "proto") is { } cartridge && cartridge != "null")
                DescribeAmmunition(cartridge, "", multiplier, gunSpeed, read, visited, add);
        }
        if (Scalar(Component(components, "CartridgeAmmo"), "proto") is { } projectile && projectile != "null")
            DescribeAmmunition(projectile, "", multiplier, gunSpeed, read, visited, add);

        foreach (var damageComponent in new[] { "Projectile", "HitscanBasicDamage" })
        {
            if (!components.TryGetValue(damageComponent, out var damage))
                continue;
            if (damage.TryGetValue("damage", out var damageNode))
            {
                var specification = _serialization.Read<DamageSpecifier>(damageNode, notNullableOverride: true);
                foreach (var (type, amount) in specification.DamageDict.OrderBy(x => x.Key, StringComparer.Ordinal))
                    add(prefix + "прямой урон " + DamageName(type), amount.ToString() + " → " + (amount * (float) multiplier).ToString() + " после множителя орудия");
            }
            if (Scalar(damage, "armorPenetration") is { } ap)
                add(prefix + "бронепробитие", Format(Numeric(damage, "armorPenetration") * 100) + "%");
            Field(damageComponent, "ignoreResistances", "игнорирование сопротивлений");
        }
        Field("Projectile", "penetrationThreshold", "бюджет пробития разрушений", " урона");
        if (Node(Component(components, "Projectile"), "penetrationDamageTypeRequirement") is SequenceDataNode penetrationTypes)
            add(prefix + "обязательные типы урона для пробития", string.Join(", ", penetrationTypes.OfType<ValueDataNode>().Select(t => DamageName(t.Value))));
        Field("Projectile", "linearDampening", "линейное затухание");
        Field("HitscanBasicRaycast", "maxDistance", "дальность луча", " м");
        if (components.ContainsKey("Projectile"))
        {
            add(prefix + "скорость, заданная орудием", Format(gunSpeed) + " м/с");
            Field("TimedDespawn", "lifetime", "время существования", " с");
            if (Numeric(Component(components, "Projectile"), "linearDampening") == 0 && components.TryGetValue("TimedDespawn", out var lifetime))
                add(prefix + "теоретический путь без препятствий и скорости корабля", Format(gunSpeed * Numeric(lifetime, "lifetime")) + " м");
        }
        foreach (var guidance in new[] { "TargetSeeking", "TargetGuided" })
        {
            if (!components.ContainsKey(guidance))
                continue;
            add(prefix + "наведение", guidance == "TargetSeeking" ? "самонаведение; пусковая скорость прибавляется к скорости орудия" : "курсор; скорость задаётся системой наведения");
            Field(guidance, "launchSpeed", "пусковая скорость наведения", " м/с");
            Field(guidance, "maxSpeed", "максимальная скорость", " м/с");
            Field(guidance, "acceleration", "ускорение", " м/с²");
            Field(guidance, "turnRate", "скорость поворота");
            Field(guidance, "detectionRange", "захват цели", " м");
            Field(guidance, "scanArc", "угол поиска", "°");
            Field(guidance, "trackDelay", "задержка поиска", " с");
            Field(guidance, "maxLifetime", "предел времени наведения", " с");
        }
        Field("ProjectileSpread", "count", "число снарядов за выстрел");
        Field("ProjectileSpread", "spread", "разлёт снарядов");
        if (Scalar(Component(components, "ProjectileSpread"), "proto") is { } pellet && pellet != id)
            DescribeAmmunition(pellet, "", multiplier, gunSpeed, read, visited, add);
        Field("Explosive", "explosionType", "тип взрыва");
        Field("Explosive", "totalIntensity", "общая интенсивность взрыва");
        Field("Explosive", "intensitySlope", "спад интенсивности взрыва");
        Field("Explosive", "maxIntensity", "максимум интенсивности на клетке");
        Field("Explosive", "tileBreakScale", "множитель разрушения пола");
        Field("Explosive", "maxTileBreak", "предел разрушений пола");
        Field("Explosive", "canCreateVacuum", "возможность разгерметизации");
        if (Scalar(Component(components, "Explosive"), "explosionType") is { } explosionId &&
            _prototype.TryIndex<ExplosionPrototype>(explosionId, out var explosion))
        {
            foreach (var (damageType, amount) in explosion.DamagePerIntensity.DamageDict.OrderBy(p => p.Key, StringComparer.Ordinal))
                add(prefix + "урон взрыва " + DamageName(damageType) + " за единицу интенсивности", amount.ToString());
            if (explosion.FireStacks is { } stacks)
                add(prefix + "огонь от взрыва", Format(stacks));
            if (explosion.Temperature is { } temperature)
                add(prefix + "температура взрыва", Format(temperature) + " К");
        }
        Field("EmpOnTrigger", "range", "радиус ЭМИ", " м");
        Field("EmpOnTrigger", "energyConsumption", "энергия ЭМИ", " Дж");
        Field("EmpOnTrigger", "disableDuration", "отключение ЭМИ", " с");
        Field("ProjectileGrenade", "capacity", "число вторичных снарядов");
        Field("ProjectileGrenade", "minVelocity", "мин. скорость вторичных снарядов", " м/с");
        Field("ProjectileGrenade", "maxVelocity", "макс. скорость вторичных снарядов", " м/с");
        Field("ProjectileKnockback", "knockback", "отбрасывание");
        Field("OnUseTimerTrigger", "delay", "задержка таймера", " с");
        if (Node(Component(components, "TriggerOnProximity"), "shape") is MappingDataNode shape && Scalar(shape, "radius") is { } radius)
            add(prefix + "радиус неконтактного взрывателя", radius + " м");
        Field("LightningOnTrigger", "chance", "вероятность электрического эффекта");
        Field("LightningOnTrigger", "count", "число электрических дуг");
        Field("LightningOnTrigger", "range", "дальность электрических дуг", " м");
        Field("LightningOnTrigger", "arcDepth", "глубина цепи дуг");
        foreach (var (component, field) in new[] { ("HitscanSpawnEntity", "spawnedEntity"), ("ProjectileGrenade", "fillPrototype"), ("LightningOnTrigger", "lightningProto") })
        {
            if (Scalar(Component(components, component), field) is { } child && child != "null")
            {
                add(prefix + "вторичный объект", EquipmentName(child));
                // Child effects are independent of the gun's direct-damage modifier.
                DescribeAmmunition(child, "", 1, 0, read, visited, add);
            }
        }
    }

    private static bool MatchesWhitelist(EntityPrototype prototype, MappingDataNode whitelist,
        Func<string, Dictionary<string, MappingDataNode>> read)
    {
        var requirements = new List<bool>();
        if (whitelist.TryGetValue("components", out var componentNode) && componentNode is SequenceDataNode components)
            requirements.AddRange(components.OfType<ValueDataNode>().Select(c => prototype.Components.ContainsKey(c.Value)));
        if (whitelist.TryGetValue("tags", out var tagsNode) && tagsNode is SequenceDataNode tags)
        {
            var actual = Node(Component(read(prototype.ID), "Tag"), "tags") as SequenceDataNode;
            var values = actual?.OfType<ValueDataNode>().Select(t => t.Value).ToHashSet() ?? [];
            requirements.AddRange(tags.OfType<ValueDataNode>().Select(t => values.Contains(t.Value)));
        }
        // Size/mind-role predicates need runtime entities; never claim an unverified match.
        if (whitelist.TryGetValue("sizes", out var sizes) && sizes is SequenceDataNode { Count: > 0 })
            return false;
        return requirements.Count > 0 && (Boolean(whitelist, "requireAll") ? requirements.All(x => x) : requirements.Any(x => x));
    }

    private static MappingDataNode Component(Dictionary<string, MappingDataNode> components, string name) => components.GetValueOrDefault(name) ?? new MappingDataNode();
    private string EquipmentName(string id) => _prototype.TryIndex<EntityPrototype>(id, out var prototype) && !string.IsNullOrWhiteSpace(prototype.Name) ? prototype.Name : id;
    private string DamageName(string id) => _prototype.TryIndex<DamageTypePrototype>(id, out var prototype) ? prototype.LocalizedName : id;
    private static string ReadableScalar(string value)
    {
        if (bool.TryParse(value, out var boolean)) return boolean ? "да" : "нет";
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return Format(number);
        if (value.EndsWith(" rad", StringComparison.Ordinal)
            && double.TryParse(value[..^4], NumberStyles.Float, CultureInfo.InvariantCulture, out var radians))
            return Format(radians * 180 / Math.PI) + "°";
        return value switch
        {
            "Superlight" => "сверхлёгкий", "Light" => "лёгкий", "Medium" => "средний",
            "Heavy" => "тяжёлый", "Superheavy" => "сверхтяжёлый",
            "FullAuto" => "автоматический", "SemiAuto" => "одиночный", "Burst" => "очередь",
            _ => value,
        };
    }
    private static DataNode? Node(MappingDataNode node, string key) => node.TryGetValue(key, out var value) ? value : null;
    private static string? Scalar(MappingDataNode node, string key) => node.TryGetValue(key, out var value) && value is ValueDataNode scalar ? scalar.Value : null;
    private static bool Boolean(MappingDataNode node, string key) => bool.TryParse(Scalar(node, key), out var result) && result;
    private static double Numeric(MappingDataNode node, string key, double fallback = 0) => double.TryParse(Scalar(node, key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string DisplayNode(DataNode? node) => node switch
    {
        ValueDataNode value => ReadableScalar(value.Value),
        SequenceDataNode sequence => string.Join(", ", sequence.Select(DisplayNode)),
        _ => string.Empty
    };
    private static string CanonicalNode(DataNode node) => node switch
    {
        ValueDataNode value => value.Value.Length + ":" + value.Value,
        MappingDataNode mapping => "{" + string.Join(";", mapping.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Key + "=" + CanonicalNode(p.Value))) + "}",
        SequenceDataNode sequence => "[" + string.Join(";", sequence.Select(CanonicalNode)) + "]",
        _ => string.Empty
    };
}
