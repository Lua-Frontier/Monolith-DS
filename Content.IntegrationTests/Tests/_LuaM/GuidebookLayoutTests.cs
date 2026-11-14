/* LuaM - AGPLv3. See AGPLv3.txt. */
using System.Linq;
using System.Numerics;
using System.IO;
using System.Reflection;
using Content.Client.Guidebook;
using Content.Client.Guidebook.Controls;
using Content.Client.Guidebook.Richtext;
using Content.Shared._LuaM.Guidebook;
using Content.Shared.Guidebook;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class GuidebookLayoutTests
{
    private static void Layout(GuidebookWindow window, Vector2? size = null)
    {
        var dimensions = size ?? new Vector2(900, 700);
        window.Measure(dimensions);
        window.Arrange(UIBox2.FromDimensions(Vector2.Zero, dimensions));
    }

    [Test]
    public async Task ExpandingRulesDoesNotTakeOverTheReaderChosenNavigationWidth()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
            var guides = prototypes.EnumeratePrototypes<GuideEntryPrototype>()
                .ToDictionary(p => new ProtoId<GuideEntryPrototype>(p.ID), p => (GuideEntry) p);
            using var window = new GuidebookWindow();
            window.UpdateGuides(guides, GuidebookNavigation.GetBookRoots(guides));
            window.Open();
            Layout(window);
            var split = window.FindControl<SplitContainer>("Split");
            var chosenWidth = split.SplitCenter;
            window.HandleClick("LuaMRuleset");
            Layout(window);
            Assert.That(split.SplitCenter, Is.EqualTo(chosenWidth).Within(1), "Long expanded rule titles must scroll inside the tree, not resize it.");
            split.SplitCenter = 185;
            Layout(window);
            Assert.That(split.SplitCenter, Is.EqualTo(185).Within(1), "The sidebar must remain shrinkable while Rules is expanded.");
            window.HandleClick("ColossusHome");
            Layout(window);
            Assert.That(split.SplitCenter, Is.EqualTo(185).Within(1));
            window.Close();
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HubHeadingHasSpaceBelowAndHomeDoesNotDisplaceTheArticle()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            using var page = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
            var parser = pair.Client.ResolveDependency<DocumentParsingManager>();
            Assert.That(parser.TryAddMarkup(page, new ProtoId<GuideEntryPrototype>("ColossusCatalogue")), Is.True);
            page.Measure(new Vector2(400, 1000));
            page.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(400, page.DesiredSize.Y)));
            var blocks = page.Children.OfType<Document>().Single().Children.ToArray();
            Assert.That(blocks[1].Position.Y - blocks[0].Position.Y - blocks[0].Size.Y, Is.GreaterThanOrEqualTo(10));

            var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
            var guides = prototypes.EnumeratePrototypes<GuideEntryPrototype>()
                .ToDictionary(p => new ProtoId<GuideEntryPrototype>(p.ID), p => (GuideEntry) p);
            using var window = new GuidebookWindow();
            window.UpdateGuides(guides, GuidebookNavigation.GetBookRoots(guides));
            window.Open();
            Layout(window);
            var body = window.FindControl<BoxContainer>("EntryContainer");
            var top = body.GlobalPosition.Y;
            window.HandleClick("ColossusCatalogue");
            Layout(window);
            Assert.That(body.GlobalPosition.Y, Is.EqualTo(top).Within(1), "The Home button belongs in window chrome, not a row above the article.");
            var home = window.FindControl<Button>("HomeButton");
            Assert.That(home.GlobalPosition.Y + home.Size.Y, Is.LessThanOrEqualTo(body.GlobalPosition.Y));
            window.Close();
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FinishedResizesAreArchivedAndRestoredAfterConfigurationReload()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            var cfg = pair.Client.ResolveDependency<IConfigurationManager>();
            var previous = new[] { cfg.GetCVar(GuidebookCVars.WindowWidth), cfg.GetCVar(GuidebookCVars.WindowHeight), cfg.GetCVar(GuidebookCVars.NavigationWidth) };
            var names = new[] { GuidebookCVars.WindowWidth.Name, GuidebookCVars.WindowHeight.Name, GuidebookCVars.NavigationWidth.Name };
            try
            {
                var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
                var guides = prototypes.EnumeratePrototypes<GuideEntryPrototype>()
                    .ToDictionary(p => new ProtoId<GuideEntryPrototype>(p.ID), p => (GuideEntry) p);
                using (var window = new GuidebookWindow())
                {
                    window.UpdateGuides(guides, GuidebookNavigation.GetBookRoots(guides));
                    window.Open();
                    Layout(window);
                    var split = window.FindControl<SplitContainer>("Split");
                    split.SplitCenter = 205;
                    var finished = (System.Action) typeof(SplitContainer).GetField("OnSplitResizeFinished", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(split)!;
                    finished();
                    Assert.That(cfg.GetCVar(GuidebookCVars.NavigationWidth), Is.EqualTo(205));

                    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    var relative = window.Size - Vector2.One;
                    var down = new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, BoundKeyState.Down, default, default, relative, relative);
                    typeof(GuidebookWindow).GetMethod("KeyBindDown", flags)!.Invoke(window, new object[] { down });
                    window.SetSize = new Vector2(840, 600);
                    Layout(window, new Vector2(840, 600));
                    var up = new GUIBoundKeyEventArgs(EngineKeyFunctions.UIClick, BoundKeyState.Up, default, default, relative, relative);
                    typeof(GuidebookWindow).GetMethod("KeyBindUp", flags)!.Invoke(window, new object[] { up });
                    Assert.That(cfg.GetCVar(GuidebookCVars.WindowWidth), Is.EqualTo(840));
                    Assert.That(cfg.GetCVar(GuidebookCVars.WindowHeight), Is.EqualTo(600));
                    window.Close();
                }
                using var stored = new MemoryStream();
                cfg.SaveToTomlStream(stored, names);
                foreach (var name in names)
                {
                    Assert.That(cfg.GetCVarFlags(name).HasFlag(CVar.ARCHIVE | CVar.CLIENTONLY), Is.True);
                    cfg.SetCVar(name, 1f);
                }
                stored.Position = 0;
                cfg.LoadFromTomlStream(stored);
                using var restored = new GuidebookWindow();
                restored.UpdateGuides(guides, GuidebookNavigation.GetBookRoots(guides));
                restored.Open();
                restored.Measure(new Vector2(2000, 2000));
                Assert.That(restored.DesiredSize, Is.EqualTo(new Vector2(840, 600)));
                Layout(restored, new Vector2(840, 600));
                Assert.That(restored.FindControl<SplitContainer>("Split").SplitCenter, Is.EqualTo(205).Within(1));
                restored.Close();
            }
            finally
            {
                for (var i = 0; i < names.Length; i++)
                    cfg.SetCVar(names[i], previous[i]);
            }
        });
        await pair.CleanReturnAsync();
    }
}
