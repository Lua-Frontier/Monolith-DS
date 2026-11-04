/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Content.Server._LuaM.Guidebook;
using Content.Shared._LuaM.Guidebook;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Value;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class GuidebookEquipmentTests
{
    [Test]
    public async Task EquipmentUsesActualAmmoBurstAndMapOverridesWithoutSpawning()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitPost(() =>
        {
            var entityManager = pair.Server.ResolveDependency<IEntityManager>();
            var prototypes = pair.Server.ResolveDependency<IPrototypeManager>();
            var system = entityManager.System<GuidebookCatalogSystem>();
            var countBefore = entityManager.EntityCount;
            var stationOverride = new SequenceDataNode();
            stationOverride.Add(new MappingDataNode
            {
                { "type", new ValueDataNode("FireControlServer") },
                { "processingPower", new ValueDataNode("140") }
            });
            // Real saved maps carry local UIDs in BallisticAmmoProvider.entities. Resolving those
            // as standalone EntityUid values without a map context used to reject the entire catalog.
            var ammunitionReferences = new SequenceDataNode();
            ammunitionReferences.Add(new ValueDataNode("123"));
            var weaponOverride = new SequenceDataNode();
            weaponOverride.Add(new MappingDataNode
            {
                { "type", new ValueDataNode("BallisticAmmoProvider") },
                { "entities", ammunitionReferences },
                { "capacity", new ValueDataNode("7") }
            });
            weaponOverride.Add(new MappingDataNode
            {
                { "type", new ValueDataNode("Gun") },
                { "damageModifier", new ValueDataNode("2") }
            });
            var mapped = new List<GuidebookMappedEntity>
            {
                new("WeaponTurretAK570", "/Maps/test-vessel.yml", "Vessel", null),
                new("WeaponTurretAK570", "/Maps/test-runtime-references.yml", "Runtime references", weaponOverride),
                new("WeaponLaserTurretL1Phalanx", "/Maps/test-vessel.yml", "Vessel", null),
                new("GunneryServerStation", "/Maps/test-station.yml", "Station", null),
                new("GunneryServerStation", "/Maps/test-override.yml", "Override", stationOverride)
            };
            var build = typeof(GuidebookCatalogSystem).GetMethod("BuildEquipment", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var entries = (List<GuidebookEquipmentEntry>) build.Invoke(system, new object[] { mapped })!;
            var autocannon = entries.Single(e => e.PrototypeId == "WeaponTurretAK570" && e.Locations.ContainsKey("/Maps/test-vessel.yml"));
            Assert.That(autocannon.Stats.Single(s => s.Label == "Теоретический цикл очереди").Value, Is.EqualTo("0.8 с"));
            Assert.That(autocannon.Stats.Single(s => s.Label == "Теоретический средний темп очередями").Value, Is.EqualTo("2.5 выстр./с"));
            Assert.That(autocannon.Stats.Single(s => s.Label == "Начальный магазин").Value,
                Is.EqualTo(prototypes.Index<EntityPrototype>("Magazine57mmHE").Name));
            var bluntName = prototypes.Index<DamageTypePrototype>("Blunt").LocalizedName;
            Assert.That(autocannon.Stats.Any(s => s.Label.EndsWith("прямой урон " + bluntName) && s.Value == "65 → 71.5 после множителя орудия"), Is.True,
                "Cartridge indirection must retain resolved damage and apply the gun multiplier.");
            Assert.That(autocannon.Stats.Where(s => s.Label.Contains("прямой урон")).All(s => s.Ammunition), Is.True,
                "Ammunition is explicitly classified; colon-bearing hardpoint labels are not ammo.");
            Assert.That(autocannon.Stats.Where(s => s.Label.StartsWith("Обязательное крепление")).All(s => !s.Ammunition), Is.True);
            var referencedWeapon = entries.Single(e => e.Locations.ContainsKey("/Maps/test-runtime-references.yml"));
            Assert.That(referencedWeapon.Stats.Single(s => s.Label == "Вместимость").Value, Is.EqualTo("7"));
            Assert.That(referencedWeapon.Stats.Any(s => s.Label.EndsWith("прямой урон " + bluntName) && s.Value == "65 → 130 после множителя орудия"), Is.True,
                "Opaque map-local entity references must not prevent numeric overrides from being retained.");
            Assert.That(referencedWeapon.Stats.Single(s => s.Label == "Темп вне очереди").Value, Is.EqualTo("3 выстр./с"),
                "A partial Gun override must preserve resolved inherited defaults.");
            var laser = entries.Single(e => e.PrototypeId == "WeaponLaserTurretL1Phalanx");
            Assert.That(laser.Stats.Single(s => s.Label.EndsWith("дальность луча")).Value, Is.EqualTo("512 м"));
            Assert.That(laser.Stats.Any(s => s.Label.Contains("теоретический путь")), Is.False,
                "Hitscan must not be presented as a projectile with ballistic range.");
            var servers = entries.Where(e => e.PrototypeId == "GunneryServerStation").ToList();
            Assert.That(servers, Has.Count.EqualTo(2));
            Assert.That(servers.Select(e => e.Stats.Single(s => s.Label == "Вычислительная мощность").Value),
                Is.EquivalentTo(new[] { "3000 ед.", "140 ед." }));
            Assert.That(servers.Single(e => e.Locations.ContainsKey("/Maps/test-override.yml")).Stats
                .Single(s => s.Label == "Лимит консолей").Value, Is.EqualTo("не ограничен сервером"));
            Assert.That(entityManager.EntityCount, Is.EqualTo(countBefore));
        });
        await pair.CleanReturnAsync();
    }
}
