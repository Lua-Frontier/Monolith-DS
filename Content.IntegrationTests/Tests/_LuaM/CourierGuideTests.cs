/* LuaCorp - AGPLv3. See AGPLv3.txt for details. */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Content.Client._LuaM.Delivery;
using Content.Client._LuaM.Guidebook;
using Content.Client.Guidebook;
using Content.Client.Guidebook.Controls;
using Content.Client.Guidebook.Richtext;
using Content.Client.UserInterface.Controls;
using Content.Shared.Guidebook;
using Content.Shared.Roles;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class CourierGuideTests
{
    private static IEnumerable<Control> Descendants(Control root)
    {
        yield return root;
        foreach (var child in root.Children)
        foreach (var descendant in Descendants(child))
            yield return descendant;
    }

    [Test]
    public async Task CourierGuideLinksToTheNativeLocalAppAndReleasesIt()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
            var resources = pair.Client.ResolveDependency<IResourceManager>();
            var parser = pair.Client.ResolveDependency<DocumentParsingManager>();
            var entities = pair.Client.ResolveDependency<IEntityManager>();
            var root = pair.Client.ResolveDependency<IUserInterfaceManager>().RootControl;
            Assert.That(prototypes.Index<JobPrototype>("DeliveryCourier").Guides!.First().ToString(), Is.EqualTo("ColossusCourier"));
            Assert.That(prototypes.Index<GuideEntryPrototype>("ColossusActivities").Children.Select(c => c.ToString()), Does.Contain("ColossusCourier"));
            using (var reader = resources.ContentFileReadText(prototypes.Index<GuideEntryPrototype>("ColossusCourier").Text))
            using (var document = new Document())
                Assert.That(parser.TryAddMarkup(document, reader.ReadToEnd()), Is.True);
            using (var reader = resources.ContentFileReadText(prototypes.Index<GuideEntryPrototype>("Jobs").Text))
            {
                var hub = reader.ReadToEnd();
                Assert.That(hub, Does.Contain("ColossusCourier").And.Not.Contain("30% первых"));
            }
            var before = entities.EntityCount;
            using var example = new GuideNavigationUI();
            Assert.That(example.TryParseTag(new() { ["Mode"] = "delivery" }, out _), Is.True);
            root.AddChild(example);
            var open = typeof(GuideNavigationUI).GetMethod("OpenExample", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var field = typeof(GuideNavigationUI).GetField("_window", BindingFlags.Instance | BindingFlags.NonPublic)!;
            open.Invoke(example, null);
            var window = (FancyWindow)field.GetValue(example)!;
            Assert.That(Descendants(window).OfType<DeliveryUiFragment>().Count(), Is.EqualTo(1));
            var take = Descendants(window).OfType<Button>().First(b => b.Text == Loc.GetString("delivery-ui-take"));
            void Click(Button button)
            {
                var callback = (Delegate)typeof(BaseButton).GetField("OnPressed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(button)!;
                callback.DynamicInvoke(new object?[] { null });
            }
            Click(take);
            Assert.That(Descendants(window).OfType<Label>().Count(l => l.Text == Loc.GetString("delivery-ui-status-yours")), Is.EqualTo(2));
            Click(Descendants(window).OfType<Button>().Single(b => b.Text == Loc.GetString("delivery-ui-take")));
            Assert.That(Descendants(window).OfType<Label>().Any(l => l.Text.Contains("только два")), Is.True);
            Assert.That(entities.EntityCount, Is.EqualTo(before), "The example must not create parcels or other game entities.");
            open.Invoke(example, null);
            Assert.That(window.Disposed, Is.True, "Replacing the example releases the previous native app.");
            var reopened = (FancyWindow)field.GetValue(example)!;
            root.RemoveChild(example);
            Assert.That(reopened.Disposed, Is.True, "Leaving the article releases the app and its timers.");
            Assert.That(field.GetValue(example), Is.Null);
            Assert.That(entities.EntityCount, Is.EqualTo(before));
        });
        await pair.CleanReturnAsync();
    }
}
