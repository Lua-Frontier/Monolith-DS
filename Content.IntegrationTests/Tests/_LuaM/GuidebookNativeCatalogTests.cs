/* LuaCorp - AGPLv3. Copyright (c) 2026 LuaCorp Contributors. See AGPLv3.txt. */
using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Numerics;
using Content.Client._LuaM.Guidebook;
using Content.Client.Store.Ui;
using Content.Shared.FixedPoint;
using Content.Shared.Store;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class GuidebookNativeCatalogTests
{
    [Test]
    public async Task ListReleasesPreviousDetailsAndOreScrollHasVisibleHeight()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var catalog = pair.Client.ResolveDependency<IEntityManager>().System<GuidebookCatalogClientSystem>();
        await pair.Client.WaitPost(catalog.Request);
        for (var i = 0; i < 100 && catalog.Catalog == null; i++) await pair.RunTicksSync(5);
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.ResolveDependency<IEntityManager>();
            var before = entities.EntityCount;
            using var browser = new GuideCatalogList();
            pair.Client.ResolveDependency<IUserInterfaceManager>().RootControl.AddChild(browser);
            var released = 0;
            for (var i = 0; i < 24; i++) browser.AddEntry("Item " + i, "", null, body => body.AddChild(new OwnedDetail(() => released++)));
            browser.SelectFirst();
            var list = browser.Children.OfType<ItemList>().Single();
            list[1].Selected = true; list[2].Selected = true;
            Assert.That(released, Is.EqualTo(2));
            Assert.That(entities.EntityCount, Is.EqualTo(before), "Rows must not spawn preview entities.");
            using var ore = new GuideOreMatrix();
            Assert.That(ore.TryParseTag(new(), out _), Is.True);
            Assert.That(catalog.Catalog, Is.Not.Null);
            var selectors = Descendants(ore).OfType<OptionButton>().ToArray();
            Assert.That(selectors.Length, Is.EqualTo(2), "Choose a grade and asteroid family instead of adding a column per family.");
            Assert.That(selectors[1].ItemCount, Is.EqualTo(catalog.Catalog!.OreTables.Count(t => t.Grade == "Soft")));
            Assert.That(Descendants(ore).OfType<ScrollContainer>().Single().HScrollEnabled, Is.False);
            ore.Measure(new Vector2(320, float.PositiveInfinity));
            Assert.That(ore.DesiredSize.Y, Is.GreaterThanOrEqualTo(350), "A scroll control returning zero measure must still have explicit height.");
            ore.Arrange(new UIBox2(0, 0, 320, ore.DesiredSize.Y));
            Assert.That(ore.DesiredSize.X, Is.LessThanOrEqualTo(320));
            foreach (var row in Descendants(ore).OfType<BoxContainer>().Where(r => r.Name == "OreProbabilityRow"))
            {
                var cells = row.Children.ToArray();
                Assert.That(cells.Length, Is.EqualTo(2));
                Assert.That(cells[1].Position.X, Is.GreaterThanOrEqualTo(cells[0].Position.X + cells[0].Size.X));
                Assert.That(cells[1].Position.X + cells[1].Size.X, Is.LessThanOrEqualTo(320));
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NativeStoreDisablesEvenFreePurchasesWithoutSpawningEntities()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            var entities = pair.Client.ResolveDependency<IEntityManager>();
            var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
            var before = entities.EntityCount;
            var proto = prototypes.EnumeratePrototypes<ListingPrototype>().First(l => l.ProductEntity != null && l.Categories.Count > 0);
            var free = new ListingData(proto) { OriginalCost = new Dictionary<ProtoId<CurrencyPrototype>, FixedPoint2>() };
            using var store = new StoreMenu();
            store.SetGuidebookReadOnly();
            var listings = new List<ListingDataWithCostModifiers> { new(free) };
            store.PopulateStoreCategoryButtons(listings.ToHashSet()); store.UpdateListing(listings);
            var row = Descendants(store).OfType<StoreListingControl>().Single();
            Assert.That(Descendants(row).OfType<Button>().Single().Disabled, Is.True, "A zero-price listing must also be disabled.");
            Assert.That(entities.EntityCount, Is.EqualTo(before));
            store.Close();
        });
        await pair.CleanReturnAsync();
    }

    private static IEnumerable<Control> Descendants(Control root)
    { foreach (var child in root.Children) { yield return child; foreach (var nested in Descendants(child)) yield return nested; } }

    private sealed class OwnedDetail(Action released) : Control
    {
        protected override void ExitedTree() { released(); base.ExitedTree(); }
    }
}
