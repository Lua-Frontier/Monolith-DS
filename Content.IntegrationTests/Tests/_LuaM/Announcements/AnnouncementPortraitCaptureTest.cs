#nullable enable
using System.Linq;
using Content.Shared._LuaM.Announcements;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._LuaM.Announcements;

[TestFixture]
public sealed class AnnouncementPortraitCaptureTest
{
    [Test]
    public async Task AuthorsWithoutHumanoidAppearanceDoNotCreatePortraits()
    {
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var capture = pair.Server.System<AnnouncementPortraitCaptureSystem>();
            Assert.That(capture.Capture(null), Is.Null, "Off-screen/system authors use the presentation emblem.");
            var author = entities.SpawnEntity("MobMouse", Robust.Shared.Map.MapCoordinates.Nullspace);
            Assert.That(capture.Capture(author), Is.Null, "Do not invent a humanoid appearance for non-humanoids.");
            entities.DeleteEntity(author);
            Assert.That(capture.Capture(author), Is.Null);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    [TestCase(true)]
    [TestCase(false)]
    public async Task DeployedHelmetRestoresSavedHeadwearOnlyInSnapshot(bool withBeret)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var inventory = server.System<InventorySystem>();
            var capture = server.System<AnnouncementPortraitCaptureSystem>();
            var author = em.SpawnEntity("MobHuman", map.GridCoords);
            var suit = em.SpawnEntity("ClothingOuterHardsuitBasic", map.GridCoords);
            EntityUid? beret = withBeret ? em.SpawnEntity("ClothingHeadHatBeret", map.GridCoords) : null;
            Assert.That(inventory.TryEquip(author, suit, "outerClothing", force: true), Is.True);
            if (beret != null)
                Assert.That(inventory.TryEquip(author, beret.Value, "head", force: true), Is.True);

            // Exercise the actual replacement-container behavior, not a synthetic inventory approximation.
            em.EventBus.RaiseLocalEvent(suit, new ToggleClothingEvent { Performer = author });
            Assert.That(inventory.TryGetSlotEntity(author, "head", out var helmet), Is.True);
            var attached = em.GetComponent<AttachedClothingComponent>(helmet!.Value);
            Assert.That(attached.ClothingContainer!.ContainedEntity, Is.EqualTo(beret));

            var snapshot = capture.Capture(author)!;
            var head = snapshot.Equipment.SingleOrDefault(e => e.Slot == "head");
            Assert.That(head?.Prototype, Is.EqualTo(withBeret ? "ClothingHeadHatBeret" : null));
            Assert.That(snapshot.Equipment.Single(e => e.Slot == "outerClothing").Prototype,
                Is.EqualTo("ClothingOuterHardsuitBasic"));
            Assert.That(inventory.TryGetSlotEntity(author, "head", out var unchanged), Is.True);
            Assert.That(unchanged, Is.EqualTo(helmet));
            Assert.That(attached.ClothingContainer.ContainedEntity, Is.EqualTo(beret));
            em.DeleteEntity(author);
        });
        await server.WaitPost(() => server.EntMan.DeleteEntity(map.MapUid));
        await pair.CleanReturnAsync();
    }

    [Test]
    [TestCase(null, null)]
    [TestCase("ClothingHeadHatBeret", "ClothingHeadHatBeret")]
    [TestCase("ClothingHeadHelmetBasic", "ClothingHeadHelmetBasic")]
    [TestCase("ClothingHeadHelmetHardsuitBasic", null)]
    [TestCase("ClothingHeadHelmetEVA", null)]
    public async Task HeadwearAndAppearanceAreIndependentCopies(string? headPrototype, string? expected)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var inventory = server.System<InventorySystem>();
            var author = em.SpawnEntity("MobHuman", map.GridCoords);
            EntityUid? headwear = headPrototype == null ? null : em.SpawnEntity(headPrototype, map.GridCoords);
            if (headwear != null)
                Assert.That(inventory.TryEquip(author, headwear.Value, "head", force: true), Is.True);

            var appearance = em.GetComponent<HumanoidAppearanceComponent>(author);
            appearance.MarkingSet.Markings[MarkingCategories.Hair] = [new Marking("HumanHairBald", [Color.Red])];
            var snapshot = server.System<AnnouncementPortraitCaptureSystem>().Capture(author)!;
            Assert.That(snapshot.Equipment.SingleOrDefault(e => e.Slot == "head")?.Prototype, Is.EqualTo(expected));
            appearance.MarkingSet.Markings[MarkingCategories.Hair][0].SetColor(0, Color.Blue);
            appearance.PermanentlyHidden.Add(HumanoidVisualLayers.Head);
            appearance.CustomBaseLayers.Clear();
            Assert.That(snapshot.Markings.Markings[MarkingCategories.Hair][0].MarkingColors[0], Is.EqualTo(Color.Red));
            Assert.That(snapshot.PermanentlyHidden.Contains(HumanoidVisualLayers.Head), Is.False);
            Assert.That(snapshot.CustomBaseLayers, Is.Not.SameAs(appearance.CustomBaseLayers));
            inventory.TryGetSlotEntity(author, "head", out var unchanged);
            Assert.That(unchanged, Is.EqualTo(headwear));
            em.DeleteEntity(author);
        });
        await server.WaitPost(() => server.EntMan.DeleteEntity(map.MapUid));
        await pair.CleanReturnAsync();
    }
}
