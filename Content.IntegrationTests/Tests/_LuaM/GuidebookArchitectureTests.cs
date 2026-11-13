/* LuaM - AGPLv3. See AGPLv3.txt. */
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.Client._LuaM.Guidebook;
using Content.Client.Guidebook;
using Content.Client.Guidebook.Controls;
using Content.Client.Guidebook.Components;
using Content.Client.UserInterface.Systems.Guidebook;
using Content.Shared._LuaM.Guidebook;
using Content.Shared.Guidebook;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class GuidebookArchitectureTests
{
    [Test]
    public async Task PlayerRoutesHaveOneHomeAndLandingPageKeepsCategoriesCollapsed()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
            var guides = prototypes.EnumeratePrototypes<GuideEntryPrototype>()
                .ToDictionary(p => new ProtoId<GuideEntryPrototype>(p.ID), p => (GuideEntry) p);
            var roots = guides.Values.Where(g => g.BookRoot).OrderBy(g => g.Priority).Select(g => g.Id.ToString()).ToArray();
            Assert.That(roots, Is.EqualTo(new[] { "NewPlayer", "ColossusGuides", "Factions", "NF14", "LuaMWorld", "ColossusCatalogue", "LuaMLore", "LuaMRuleset" }));
            var seen = new HashSet<ProtoId<GuideEntryPrototype>>();
            void Walk(ProtoId<GuideEntryPrototype> id)
            {
                Assert.That(seen.Add(id), Is.True, $"{id} must occur once in the full contents.");
                foreach (var child in guides[id].Children)
                    Walk(child);
            }
            foreach (var root in roots)
                Walk(root);

            Assert.That(guides["LuaMWorld"].Children.Select(c => c.ToString()), Is.EqualTo(new[]
            {
                "SectorTopology", "LuaMStations", "LuaMWorldObjects", "LuaMGameModes", "LuaMAsteroids", "ColossusNebulae",
            }));
            Assert.That(guides["LuaMStations"].Children.Select(c => c.ToString()), Is.EqualTo(new[]
            {
                "LuaMColossusCentral", "LuaMMedical", "LuaMCamelot", "LuaMHalicon", "LuaMHelios",
            }));
            Assert.That(seen.Contains("Creatures"), Is.False);
            Assert.That(seen.Contains("ColossusCatalogueOres"), Is.False);
            Assert.That(seen.Contains("ColossusCatalogueObjects"), Is.False);
            Assert.That(guides["ColossusCatalogueOres"].Text, Is.EqualTo(guides["LuaMAsteroids"].Text));
            Assert.That(guides["ColossusCatalogueObjects"].Text, Is.EqualTo(guides["LuaMWorldObjects"].Text));
            Assert.That(guides["LuaMWorldObjects"].Children, Does.Contain(new ProtoId<GuideEntryPrototype>("ColossusSiteTradeDepots")));
            Assert.That(guides["LuaMGameModes"].Children, Does.Contain(new ProtoId<GuideEntryPrototype>("ColossusEventPlanetoids")));
            Assert.That(guides["LuaMDroneLoot"].Children, Does.Contain(new ProtoId<GuideEntryPrototype>("ColossusCatalogueDrones")));

            foreach (var route in new[]
            {
                new[] { "NewPlayer", "Species", "Human" },
                new[] { "Factions", "ColossusMMCRoles", "MMC" },
                new[] { "ColossusGuides", "ColossusProduction", "ColossusMechsHub", "S1Mechs" },
                new[] { "Engineering", "ColossusAtmosHub", "ColossusGasSynthesis", "ColossusGasFrezon" },
                new[] { "NF14", "ColossusCatalogueShipyards" },
                new[] { "LuaMWorld", "LuaMStations", "LuaMHelios" },
            })
            {
                for (var i = 1; i < route.Length; i++)
                    Assert.That(guides[route[i - 1]].Children, Does.Contain(new ProtoId<GuideEntryPrototype>(route[i])));
            }
            foreach (var (entity, guide) in new[]
            {
                ("LaserDrill", "ColossusLaserDrill"),
                ("StationLaserDrill", "ColossusLaserDrill"),
                ("TegCenter", "ColossusTEG"),
                ("TegCirculator", "ColossusTEG"),
            })
            {
                var help = (GuideHelpComponent) prototypes.Index<EntityPrototype>(entity).Components["GuideHelp"].Component;
                Assert.That(help.Guides, Does.Contain(new ProtoId<GuideEntryPrototype>(guide)), entity);
            }

            using var window = new GuidebookWindow();
            window.UpdateGuides(guides, GuidebookNavigation.GetBookRoots(guides));
            Assert.That(window.LastEntry.ToString(), Is.EqualTo("ColossusHome"));
            Assert.That(window.Tree.Items.All(i => !i.Expanded), Is.True);
            foreach (var target in new[] { "Human", "MMC", "IndustryMachineParts", "S1Mechs", "ColossusGasFrezon", "ColossusCatalogueShipyards", "LuaMHelios", "LuaMRule0" })
            {
                window.HandleClick(target);
                Assert.That(window.LastEntry.ToString(), Is.EqualTo(target));
                Assert.That(window.FindControl<BoxContainer>("ReturnContainer").Visible, Is.True);
                var button = window.FindControl<Button>("HomeButton");
                var callback = (System.Delegate) typeof(BaseButton).GetField("OnPressed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(button)!;
                callback.DynamicInvoke(new BaseButton.ButtonEventArgs(button, null!));
                Assert.That(window.LastEntry.ToString(), Is.EqualTo("ColossusHome"));
                window.HandleClick(target);
                Assert.That(window.LastEntry.ToString(), Is.EqualTo(target), "Returning to the same article after Home must select it again.");
            }

            // Exercise the actual ordinary-opening path, which supplies a configured default article.
            var controller = pair.Client.ResolveDependency<IUserInterfaceManager>().GetUIController<GuidebookUIController>();
            var windowField = typeof(GuidebookUIController).GetField("_guideWindow", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var lastField = typeof(GuidebookUIController).GetField("_lastEntry", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var oldWindow = windowField.GetValue(controller);
            var oldLast = lastField.GetValue(controller);
            using (var initial = new GuidebookWindow())
            {
                try
                {
                    windowField.SetValue(controller, initial);
                    lastField.SetValue(controller, null);
                    controller.OpenGuidebook();
                    Assert.That(initial.LastEntry.ToString(), Is.EqualTo("ColossusHome"));
                    Assert.That(initial.Tree.Items.All(i => !i.Expanded), Is.True);
                    initial.Close();
                }
                finally
                {
                    windowField.SetValue(controller, oldWindow);
                    lastField.SetValue(controller, oldLast);
                }
            }

            using var page = new BoxContainer();
            Assert.That(pair.Client.ResolveDependency<DocumentParsingManager>().TryAddMarkup(page,
                new ProtoId<GuideEntryPrototype>("ColossusHome")), Is.True);
            using var home = new GuideHome();
            Assert.That(home.ChildCount, Is.EqualTo(9));
            Assert.That(home.Children.Last(), Is.TypeOf<Button>().With.Property("Text").EqualTo("Правила"));
            foreach (var width in new[] { 360f, 720f })
            {
                home.Measure(new Vector2(width, float.PositiveInfinity));
                home.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(width, home.DesiredSize.Y)));
                foreach (var button in home.Children)
                    Assert.That(button.Position.X + button.Size.X, Is.LessThanOrEqualTo(width));
            }
        });
        await pair.CleanReturnAsync();
    }
}
