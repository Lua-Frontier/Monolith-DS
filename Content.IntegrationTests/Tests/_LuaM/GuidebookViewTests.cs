/* LuaCorp - Guidebook navigation and UI regression tests.
 * This file is licensed under AGPLv3. See AGPLv3.txt for details. */
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.Client._LuaM.Guidebook;
using Content.Client.Guidebook.Controls;
using Content.Shared._LuaM.Guidebook;
using Content.Shared.Guidebook;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class GuidebookViewTests
{
    [Test]
    public void ExplicitBookRootsExcludeOldAndContextualGuides()
    {
        var guides = new Dictionary<ProtoId<GuideEntryPrototype>, GuideEntry>
        {
            ["ships"] = new() { Id = "ships", BookRoot = true },
            ["oldStation"] = new() { Id = "oldStation" },
            ["character"] = new() { Id = "character" },
        };
        Assert.That(GuidebookNavigation.GetBookRoots(guides), Is.EqualTo(new[] { new ProtoId<GuideEntryPrototype>("ships") }));
        Assert.That(guides.ContainsKey("character"), Is.True, "Contextual entries remain available by link.");
    }

    [Test]
    public void ProjectsWithoutExplicitRootsKeepExistingNavigation()
    {
        var guides = new Dictionary<ProtoId<GuideEntryPrototype>, GuideEntry>
        {
            ["station"] = new() { Id = "station" },
        };
        Assert.That(GuidebookNavigation.GetBookRoots(guides), Is.Null);
    }

    [Test]
    public async Task ItemCardsAlignCaptionsAndWrapWithoutRenamingItems()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            using var cards = new GuideEntityCards();
            using var shortItem = new GuideEntityEmbed("Wrench", true, false);
            using var longItem = new GuideEntityEmbed("IndustrialFurnaceEconomyCompact", true, false);
            cards.AddChild(shortItem);
            cards.AddChild(longItem);
            cards.Measure(new Vector2(380, 1000));
            cards.Arrange(new UIBox2(0, 0, 380, cards.DesiredSize.Y));

            var shortCaption = shortItem.Children.OfType<RichTextLabel>().Single();
            var longCaption = longItem.Children.OfType<RichTextLabel>().Single();
            Assert.That(shortCaption.Position.Y, Is.EqualTo(longCaption.Position.Y), "Sprite sizes must not shift caption baselines.");
            var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
            Assert.That(shortCaption.GetMessage(), Is.EqualTo(prototypes.Index(new EntProtoId("Wrench")).Name));
            Assert.That(longCaption.GetMessage(), Is.EqualTo(prototypes.Index(new EntProtoId("IndustrialFurnaceEconomyCompact")).Name));

            cards.Measure(new Vector2(190, 1000));
            cards.Arrange(new UIBox2(0, 0, 190, cards.DesiredSize.Y));
            Assert.That(longItem.Position.Y, Is.GreaterThan(shortItem.Position.Y), "Narrow pages wrap cards onto another row.");
            foreach (var item in cards.Children)
                Assert.That(item.Position.X + item.Size.X, Is.LessThanOrEqualTo(190));

            using var thirdItem = new GuideEntityEmbed("Wrench", true, false);
            using var fourthItem = new GuideEntityEmbed("Wrench", true, false);
            cards.AddChild(thirdItem); cards.AddChild(fourthItem);
            cards.Measure(new Vector2(1200, 1000));
            cards.Arrange(new UIBox2(0, 0, 1200, cards.DesiredSize.Y));
            Assert.That(fourthItem.Position.Y, Is.GreaterThan(shortItem.Position.Y), "Four images must form at least two rows even on a wide page.");

            var entities = pair.Client.ResolveDependency<IEntityManager>();
            var beforePages = entities.EntityCount;
            using var window = new GuidebookWindow();
            var home = window.FindControl<Button>("HomeButton")!;
            var pressed = typeof(BaseButton).GetField("OnPressed", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var homeListeners = ((Delegate) pressed.GetValue(home))?.GetInvocationList().Length ?? 0;
            var allGuides = prototypes.EnumeratePrototypes<GuideEntryPrototype>()
                .ToDictionary(p => new ProtoId<GuideEntryPrototype>(p.ID), p => (GuideEntry) p);
            window.UpdateGuides(allGuides, GuidebookNavigation.GetBookRoots(allGuides), selected: "NewPlayer");
            Assert.That(window.LastEntry, Is.EqualTo(new ProtoId<GuideEntryPrototype>("NewPlayer")));
            Assert.That(window.Tree.Items.Any(i => i.Metadata is GuideEntry e && e.Id == "CharacterCreation"), Is.False);
            Assert.That(window.Tree.Items.Any(i => i.Metadata is GuideEntry e && e.Id == "Hiring"), Is.True);
            for (var i = 0; i < 3; i++)
            {
                window.HandleClick("ArmorExplanation");
                window.HandleClick("NewPlayer");
            }
            Assert.That(entities.EntityCount, Is.EqualTo(beforePages), "Changing guides releases their client-side sprites.");
            Assert.That(((Delegate) pressed.GetValue(home))?.GetInvocationList().Length ?? 0, Is.EqualTo(homeListeners),
                "Selecting pages must not accumulate home-button callbacks.");
            window.UpdateGuides(new Dictionary<ProtoId<GuideEntryPrototype>, GuideEntry>
            {
                ["TSFMC_Entry"] = prototypes.Index(new ProtoId<GuideEntryPrototype>("TSFMC_Entry")),
            }, selected: "TSFMC_Entry");
            window.HandleClick("ColossusCatalogueUplinks");
            Assert.That(window.LastEntry, Is.EqualTo(new ProtoId<GuideEntryPrototype>("ColossusCatalogueUplinks")),
                "Role help must follow links beyond its initially supplied entries.");
            window.HandleClick("NewPlayer");
            Assert.That(entities.EntityCount, Is.EqualTo(beforePages));
            Assert.DoesNotThrow(() => window.UpdateGuides(new Dictionary<ProtoId<GuideEntryPrototype>, GuideEntry>()),
                "An empty contextual guide list must leave a placeholder instead of throwing.");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LegacyDefusalGalleriesWrapInTheParsedPage()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            using var page = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
            var parser = pair.Client.ResolveDependency<Content.Client.Guidebook.DocumentParsingManager>();
            Assert.That(parser.TryAddMarkup(page, new ProtoId<GuideEntryPrototype>("Defusal")), Is.True);
            static IEnumerable<Robust.Client.UserInterface.Control> All(Robust.Client.UserInterface.Control root)
            {
                foreach (var child in root.Children) { yield return child; foreach (var nested in All(child)) yield return nested; }
            }
            var gallery = All(page).OfType<GuideEntityCards>().Single(g => g.ChildCount == 4);
            page.Measure(new Vector2(440, float.PositiveInfinity));
            page.Arrange(new UIBox2(0, 0, 440, page.DesiredSize.Y));
            var items = gallery.Children.ToArray();
            Assert.That(items[3].Position.Y, Is.GreaterThan(items[0].Position.Y));
            foreach (var item in items) Assert.That(item.Position.X + item.Size.X, Is.LessThanOrEqualTo(440));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ShipyardTagUsesNativeReadOnlyWindow()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            using var tag = new GuideCatalog();
            Assert.That(tag.TryParseTag(new() { ["Kind"] = "ships" }, out var control), Is.True);
            Assert.That(control, Is.TypeOf<GuideShipyardPreview>());
            var proto = pair.Client.ResolveDependency<IPrototypeManager>()
                .EnumeratePrototypes<Content.Shared._NF.Shipyard.Prototypes.VesselPrototype>().First();
            using var live = new Content.Client._NF.Shipyard.UI.ShipyardConsoleMenu();
            live.PopulateProducts(new() { proto.ID }, new(), false, true);
            using var guide = new Content.Client._NF.Shipyard.UI.ShipyardConsoleMenu();
            guide.SetGuidebookReadOnly("Test", new() { proto.ID });
            static IEnumerable<Robust.Client.UserInterface.Control> All(Robust.Client.UserInterface.Control root)
            {
                foreach (var child in root.Children) { yield return child; foreach (var nested in All(child)) yield return nested; }
            }
            var liveRow = All(live).OfType<Content.Client._NF.Shipyard.UI.VesselRow>().Single();
            var guideRow = All(guide).OfType<Content.Client._NF.Shipyard.UI.VesselRow>().Single();
            // LuaM start: rows only select a vessel; the window shows the live preview and buys from the bottom bar
            Assert.That(liveRow.Vessel?.ID, Is.EqualTo(proto.ID));
            Assert.That(guideRow.Vessel?.ID, Is.EqualTo(proto.ID));
            Assert.That(guide.PreviewView.Vessel?.ID, Is.EqualTo(proto.ID));
            Assert.That(live.PurchaseButton.Disabled, Is.False);
            Assert.That(guide.PurchaseButton.Disabled, Is.True);
            // LuaM end
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NativeMachineExamplesOnlyCreateAndReleaseLocalDummies()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.ResolveDependency<IEntityManager>();
            var root = pair.Client.ResolveDependency<IUserInterfaceManager>().RootControl;
            foreach (var mode in new[] { "reactor", "turbine" })
            {
                var before = entities.EntityCount;
                var example = new GuideReactorUI();
                Assert.That(example.TryParseTag(new Dictionary<string, string> { ["Mode"] = mode }, out _), Is.True);
                root.AddChild(example);
                typeof(GuideReactorUI).GetMethod("OpenExample", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(example, null);
                Assert.That(entities.EntityCount, Is.GreaterThan(before));
                example.Orphan();
                Assert.That(entities.EntityCount, Is.EqualTo(before), "Leaving the UI tree closes the teaching example and removes its isolated entity.");
            }
        });
        await pair.CleanReturnAsync();
    }
}
