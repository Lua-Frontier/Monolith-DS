/* LuaCorp - AGPLv3. See AGPLv3.txt. */
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._LuaM.Guidebook;
using Content.Client.Guidebook;
using Content.Client.Guidebook.Controls;
using Content.Shared.Guidebook;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class GuidebookDesignTests
{
    private static IEnumerable<Control> All(Control root)
    {
        yield return root;
        foreach (var child in root.Children)
        foreach (var nested in All(child)) yield return nested;
    }

    [Test]
    public async Task EveryMaintainedPageParsesAndResponsiveIllustrationsFitWithoutClipping()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
            var resources = pair.Client.ResolveDependency<IResourceManager>();
            var parser = pair.Client.ResolveDependency<DocumentParsingManager>();
            var paths = prototypes.EnumeratePrototypes<GuideEntryPrototype>().Select(g => g.Text)
                .Where(p => p.ToString().StartsWith("/ServerInfo/_LuaM/Guidebook/")).Distinct().ToArray();
            foreach (var path in paths)
            {
                using var page = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
                using var reader = resources.ContentFileReadText(path);
                Assert.That(parser.TryAddMarkup(page, reader.ReadToEnd()), Is.True, path.ToString());
                Assert.That(All(page).OfType<GuidebookError>(), Is.Empty, path.ToString());
            }
            foreach (var id in new[] { "ColossusHome", "Species", "ColossusSurgery", "ColossusTSFRoles", "LuaMAsteroids", "ColossusNebulae" })
            {
                using var page = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true };
                Assert.That(parser.TryAddMarkup(page, new ProtoId<GuideEntryPrototype>(id)), Is.True);
                foreach (var width in new[] { 320f, 640f, 1100f })
                {
                    page.Measure(new Vector2(width, float.PositiveInfinity));
                    page.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(width, page.DesiredSize.Y)));
                    foreach (var visual in All(page).Where(c => c is GuideEntityCards or GuideTexture or GuideHub or GuideHeading or GuideNote))
                        Assert.That(visual.Size.X, Is.LessThanOrEqualTo(width + 1), $"{id}: {visual.GetType().Name}");
                }
            }
            var entities = pair.Client.ResolveDependency<IEntityManager>();
            var before = entities.EntityCount;
            using var illustration = new GuideTexture();
            Assert.That(illustration.TryParseTag(new() { ["Entity"] = "Scalpel", ["Caption"] = "auto" }, out _), Is.True);
            Assert.That(illustration.Children.OfType<RichTextLabel>().Single().GetMessage(), Is.EqualTo(prototypes.Index<EntityPrototype>("Scalpel").Name));
            Assert.That(entities.EntityCount, Is.EqualTo(before), "Static icons must not leave a live preview entity.");
        });
        await pair.CleanReturnAsync();
    }
}
