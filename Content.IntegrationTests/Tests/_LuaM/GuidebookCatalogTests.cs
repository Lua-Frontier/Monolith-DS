/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */
using System.Linq;
using Content.Server._LuaM.Guidebook;
using Content.Client._LuaM.Guidebook;
using Content.Shared._NF.Shipyard.Prototypes;
using Content.Shared.Guidebook;
using Content.Shared._LuaM.Guidebook;
using Content.Client.Guidebook;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class GuidebookCatalogTests
{
    [Test]
    public async Task CatalogIncludesResolvedVesselsWithoutSpawningMaps()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitPost(() =>
        {
            var entities = server.ResolveDependency<IEntityManager>();
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var countBefore = entities.EntityCount;
            var catalog = entities.System<GuidebookCatalogSystem>().BuildSnapshot();
            Assert.That(catalog.Sites, Is.Not.Empty, "Local Sandbox must show the standard sector's reference objects.");
            Assert.That(catalog.Shipyards.All(y => y.Locations.Count > 0), Is.True, "Unmapped admin consoles must not create extra catalogues.");
            Assert.That(catalog.StoreListings.Keys, Is.EquivalentTo(new[] { "VendingMachinePDVUplink", "VendingMachineTSFMCUplink", "VendingMachineMMCUplink", "VendingMachineSyndieContrabandPOI" }));
            Assert.That(catalog.Acquisitions.SelectMany(e => e.Offers).Select(o => o.Faction).Distinct(),
                Is.SubsetOf(new[] { "Авангард", "ТСФ", "СССП", "ЧВК Вайпер", "Колосс Централ", "ММС", "МЦ", "Наёмники" }));
            foreach (var group in catalog.Acquisitions.GroupBy(e => e.IdentityKey))
            {
                var offers = group.SelectMany(e => e.Offers).ToList();
                if (offers.Any(o => o.Faction == "Наёмники" && o.Method is "fabricator" or "vendor" or "loadout"))
                    Assert.That(offers.All(o => o.Faction == "Наёмники"), Is.True, group.First().PrototypeId);
            }
            Assert.That(catalog.Acquisitions.Where(e => e.PrototypeId == "ClothingOuterHardsuitScaf")
                .SelectMany(e => e.Offers).Select(o => o.Faction).Distinct(), Is.EquivalentTo(new[] { "Наёмники" }));
            var armed = catalog.Acquisitions.Where(e => e.Kind == "weapon" && e.Stats.Any(s => s.Label == "Темп")).ToList();
            Assert.That(armed.Any(e => e.Stats.Any(s => s.Label == "Штатный выстрел")), Is.True);
            Assert.That(armed.All(e => e.Stats.Any(s => s.Label == "Разброс")), Is.True);
            Assert.That(catalog.Acquisitions.Single(e => e.PrototypeId == "ClothingOuterHardsuitSecurityRed")
                .Offers.Any(o => o.Faction == "Колосс Централ"), Is.True, "The representative's selectable suit must not be missed.");
            Assert.That(catalog.Acquisitions.Single(e => e.PrototypeId == "ClothingOuterHardsuitMieyoCombat")
                .Offers.Any(o => o.SourceId.StartsWith("role:MMC") && o.Faction == "Колосс Централ"), Is.False,
                "MMC security role IDs must not match the CSEC prefix.");
            TestContext.Out.WriteLine("COLOSS: " + string.Join(", ", catalog.Acquisitions.Where(e => e.Offers.Any(o => o.Faction == "Колосс Централ")).Select(e => e.PrototypeId)));
            Assert.That(catalog.Vessels.Select(v => v.Id), Is.EquivalentTo(
                prototypes.EnumeratePrototypes<VesselPrototype>().Select(v => v.ID)));
            Assert.That(catalog.Vessels.Single(v => v.Id == "QJ270").Group, Is.EqualTo("Shipyard"));
            Assert.That(catalog.Vessels.Single(v => v.Id == "Snakelet").Purchasable, Is.False);
            Assert.That(catalog.Vessels.Single(v => v.Id == "Snakelet").Vouchers, Is.Not.Empty);
            TestContext.Out.WriteLine($"CATALOG: {catalog.Vessels.Count} vessels; {catalog.Equipment.Count} equipment variants; {catalog.MapNames.Count} maps.");
            Assert.That(catalog.Equipment, Is.Not.Empty);
            Assert.That(catalog.Shipyards, Is.Not.Empty);
            Assert.That(catalog.Vouchers.Single(v => v.PrototypeId == "ShipVoucherBalor").ConsoleType, Is.EqualTo("Custom"));
            Assert.That(catalog.Shipyards.Single(y => y.PrototypeId == "BalorCarrierComputer").ConsoleType, Is.EqualTo("Custom"));
            Assert.That(catalog.Shipyards.SelectMany(y => y.Vessels).Distinct(), Is.EquivalentTo(catalog.Vessels.Select(v => v.Id)),
                "Every current vessel must be reachable through its actual console, including carrier listings.");
            foreach (var yard in catalog.Shipyards)
                Assert.That(yard.Name, Is.EqualTo(prototypes.Index<EntityPrototype>(yard.PrototypeId).Name));
            Assert.That(catalog.Shipyards.Single(y => y.PrototypeId == "PDVMotleyAnneCarrierComputer").Vessels,
                Is.EquivalentTo(new[] { "Snakelet", "Inertia", "Mock" }));
            Assert.That(entities.EntityCount, Is.EqualTo(countBefore));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CatalogIsDeliveredToTheConnectedClient()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var system = pair.Client.ResolveDependency<IEntityManager>().System<GuidebookCatalogClientSystem>();
        await pair.Client.WaitPost(system.Request);
        // A snapshot is built across ticks, rather than blocking a player request.
        for (var i = 0; i < 100 && system.Catalog == null; i++) await pair.RunTicksSync(5);
        await pair.Client.WaitAssertion(() =>
        {
            Assert.That(system.Catalog, Is.Not.Null);
            Assert.That(system.Catalog!.Vessels, Has.Count.EqualTo(122));
            Assert.That(system.Catalog.Equipment.Any(e => e.Kind == "server"), Is.True);
            Assert.That(system.Catalog.Ammunition, Is.Not.Empty);
            Assert.That(system.Catalog.Ammunition, Does.Not.Contain("CartridgePistol"));
            Assert.That(system.Catalog.Drones, Is.Not.Empty);
            Assert.That(system.Catalog.Drones.SelectMany(d => d.Loot.Keys).Distinct(),
                Is.EquivalentTo(new[] { "ClothingBackpackDroneLootT1", "ClothingBackpackDroneLootT21", "ClothingBackpackDroneLootT22" }));
            Assert.That(system.Catalog.OreTables, Is.Not.Empty);
            Assert.That(system.Catalog.OreTables.Any(t => t.Id == "NFScrapMineralSoft"), Is.True,
                "The configured scrap asteroid family contains mineable ore and must not be dropped by name.");
            Assert.That(system.Catalog.OreTables.All(t => t.Probabilities.Values.Sum() <= 1.0001), Is.True);
            Assert.That(system.Catalog.Generators, Is.Not.Empty);
            Assert.That(system.Catalog.Hydroponics, Is.Not.Empty);
            var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
            var parser = pair.Client.ResolveDependency<DocumentParsingManager>();
            var entities = pair.Client.ResolveDependency<IEntityManager>();
            var before = entities.EntityCount;
            using (var ammo = new GuideAutoCatalogue())
            {
                Assert.That(ammo.TryParseTag(new() { ["Kind"] = "ammo" }, out _), Is.True);
                var caliber = ammo.Children.OfType<OptionButton>().Single();
                Assert.That(caliber.Visible, Is.True);
                Assert.That(caliber.ItemCount, Is.GreaterThan(2), "Reachable ammunition supplies the caliber filter without a manual ID list.");
            }
            foreach (var id in prototypes.Index<GuideEntryPrototype>("ColossusCatalogue").Children
                .Concat(new ProtoId<GuideEntryPrototype>[] { "ColossusBotany", "ColossusSurgery", "ColossusPower", "MechsBase", "ColossusArtifacts", "ColossusAnomalies", "ColossusMedicalFaction", "ColossusHandheldWeapons", "ColossusPersonalAmmo" }))
            {
                using var page = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
                Assert.That(parser.TryAddMarkup(page, id), Is.True, id.Id);
                page.Measure(new System.Numerics.Vector2(380, 100000));
                page.Arrange(new Robust.Shared.Maths.UIBox2(0, 0, 380, page.DesiredSize.Y));
            }
            Assert.That(entities.EntityCount, Is.EqualTo(before), "Closing catalogue pages releases their selected item sprites.");
        });
        GuidebookCatalog? previous = null;
        void ObserveRefresh() { }
        await pair.Client.WaitPost(() =>
        {
            previous = system.Catalog;
            system.Updated += ObserveRefresh;
        });
        try
        {
            await pair.Server.WaitPost(() => pair.Server.ResolveDependency<IEntityManager>()
                .System<GuidebookCatalogSystem>().InvalidateResources());
            for (var i = 0; i < 100 && (system.Catalog == null || ReferenceEquals(system.Catalog, previous)); i++)
                await pair.RunTicksSync(5);
            await pair.Client.WaitAssertion(() =>
            {
                Assert.That(system.Catalog, Is.Not.Null);
                Assert.That(system.Catalog, Is.Not.SameAs(previous),
                    "An open catalogue must refresh after a server-side resource invalidation.");
            });
        }
        finally
        {
            await pair.Client.WaitPost(() => system.Updated -= ObserveRefresh);
        }
        await pair.CleanReturnAsync();
    }
}
