using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.Guidebook;
using Content.Client._LuaM.Guidebook;
using Content.Shared._LuaM.Guidebook;
using Content.Client.Guidebook.Controls;
using Content.Shared.Guidebook;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class TmpProtogenGuideTest
{
    [Test]
    public async Task ProtogenPageMeasures()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
            var parser = pair.Client.ResolveDependency<DocumentParsingManager>();

            foreach (var id in new[] { "Protogen", "Human", "Species" })
            {
                for (var width = 40; width <= 1000; width += 7)
                {
                    using var page = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
                    Assert.That(parser.TryAddMarkup(page, new ProtoId<GuideEntryPrototype>(id)), Is.True);
                    try
                    {
                        page.Measure(new Vector2(width, float.PositiveInfinity));
                    }
                    catch (System.Exception e)
                    {
                        Console.WriteLine($"FAIL page {id} width {width}: {e.GetType().Name} {e.Message.Split('\n')[0]}");
                        break;
                    }
                }
            }

            var allGuides = prototypes.EnumeratePrototypes<GuideEntryPrototype>()
                .ToDictionary(p => new ProtoId<GuideEntryPrototype>(p.ID), p => (GuideEntry) p);
            using var window = new GuidebookWindow();
            window.UpdateGuides(allGuides, GuidebookNavigation.GetBookRoots(allGuides), selected: "Protogen");
            foreach (var size in new[] { new Vector2(1000, 700), new Vector2(700, 500), new Vector2(500, 400), new Vector2(1600, 900) })
            {
                try
                {
                    window.Measure(size);
                    window.Arrange(UIBox2.FromDimensions(Vector2.Zero, size));
                    Console.WriteLine($"OK window {size}");
                }
                catch (System.Exception e)
                {
                    Console.WriteLine($"FAIL window {size}: {e.GetType().Name} {e.Message.Split('\n')[0]}");
                    Console.WriteLine(e.StackTrace);
                }
            }
        });
        await pair.CleanReturnAsync();
    }
}
