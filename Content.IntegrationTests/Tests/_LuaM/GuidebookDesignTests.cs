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
    [Test]
    public void ParameterFormattingKeepsValuesAndExplainsComparisonColors()
    {
        var vulnerable = GuideStatText.FormatLine("Получаемый урон, радиация: +20%");
        var resistant = GuideStatText.FormatLine("Получаемый урон, радиация: -20%");
        Assert.That(vulnerable.ToString(), Is.EqualTo("Получаемый урон, радиация: +20%"));
        Assert.That(vulnerable.ToMarkup(), Does.Contain("orange"));
        Assert.That(resistant.ToMarkup(), Does.Contain("lightgreen"));
        Assert.That(GuideStatText.Format("Урон", "[bold]10[/bold]").ToString(), Is.EqualTo("Урон: [bold]10[/bold]"));
    }

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
                var pageModes = new HashSet<string>();
                foreach (var hub in All(page).OfType<GuideHub>())
                {
                    var icons = All(hub).OfType<GuideNavigationIcon>().ToArray();
                    var previews = All(hub).OfType<GuideEntityEmbed>().ToArray();
                    var jobIcons = All(hub).OfType<GuideLobbyJobIcon>().ToArray();
                    var buttons = All(hub).OfType<Button>().Count();
                    Assert.That(icons.Length + previews.Length + jobIcons.Length, Is.EqualTo(buttons), "Every existing hub has a suitable image: " + path);
                    Assert.That(new[] { icons.Length, previews.Length, jobIcons.Length }.Count(n => n > 0), Is.EqualTo(1), "One image family per hub: " + path);
                    pageModes.Add(jobIcons.Length > 0 ? "jobs" : icons.Length > 0 ? "mono" : "sprites");
                }
                Assert.That(pageModes.Count, Is.LessThanOrEqualTo(1), "One image family throughout the hub page: " + path);
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

            using var caption = new GuideCaption();
            var root = pair.Client.ResolveDependency<IUserInterfaceManager>().RootControl;
            root.AddChild(caption);
            caption.SetMessage("Длинное название медицинского оборудования");
            caption.Measure(new Vector2(145, float.PositiveInfinity));
            caption.Arrange(UIBox2.FromDimensions(Vector2.Zero, caption.DesiredSize));
            Assert.That(caption.Children.Count(), Is.GreaterThan(1));
            foreach (var line in caption.Children.Cast<Label>())
            {
                Assert.That(line.Align, Is.EqualTo(Label.AlignMode.Center));
                Assert.That(line.Size.X, Is.EqualTo(caption.Size.X));
            }
            caption.Orphan();

            foreach (var hubId in new[] { "NewPlayer", "ColossusGuides", "Factions", "LuaMWorld", "ColossusRndHub", "ColossusExtraction", "ColossusExpeditionEnemies" })
            {
                using var hub = new GuideHub();
                Assert.That(hub.TryParseTag(new() { ["Root"] = hubId }, out _), Is.True);
                var buttons = All(hub).OfType<Button>().ToArray();
                // Long titles must grow the entire row, rather than leave mismatched tile bottoms.
                All(buttons[0]).OfType<RichTextLabel>().First().SetMessage("Очень длинное название раздела с дополнительным пояснением темы");
                hub.Measure(new Vector2(640, float.PositiveInfinity));
                hub.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(640, hub.DesiredSize.Y)));
                Assert.That(buttons[0].Size.Y, Is.EqualTo(buttons[1].Size.Y));
                var pictures = All(hub).Count(c => c is TextureRect or GuideNavigationIcon or GuideEntityEmbed);
                Assert.That(pictures == 0 || pictures == buttons.Length, Is.True, "A hub cannot mix numbered and illustrated entries.");
                if (hubId is "ColossusRndHub" or "ColossusExtraction" or "ColossusExpeditionEnemies")
                    Assert.That(All(hub).OfType<GuideEntityEmbed>().Count(), Is.EqualTo(buttons.Length), "Every topic needs its own representative object.");
            }

            using (var rolesHub = new GuideHub())
            {
                var count = entities.EntityCount;
                Assert.That(rolesHub.TryParseTag(new() { ["Root"] = "Factions" }, out _), Is.True);
                var badges = All(rolesHub).OfType<GuideLobbyJobIcon>().ToArray();
                Assert.That(badges.Length, Is.EqualTo(9));
                Assert.That(All(rolesHub).OfType<GuideNavigationIcon>(), Is.Empty);
                rolesHub.Measure(new Vector2(640, float.PositiveInfinity));
                rolesHub.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(640, rolesHub.DesiredSize.Y)));
                foreach (var badge in badges)
                {
                    Assert.That(badge.Texture, Is.Not.Null, badge.JobId);
                    Assert.That(badge.Size.X, Is.EqualTo(40), badge.JobId);
                    Assert.That(badge.Size.X, Is.GreaterThan(badge.Texture!.Width), "The lobby badges must be visibly enlarged.");
                }
                Assert.That(entities.EntityCount, Is.EqualTo(count), "Lobby icons must not spawn role entities.");
            }

            foreach (var entity in new[] { "MedicalTrackingImplanter", "ComputerShipyard", "HandHeldMassScanner" })
            {
                using var picture = new GuideTexture();
                Assert.That(picture.TryParseTag(new() { ["Entity"] = entity, ["Powered"] = "true", ["Caption"] = "auto" }, out _), Is.True);
                var count = entities.EntityCount;
                root.AddChild(picture);
                Assert.That(All(picture).OfType<SpriteView>().Single().Sprite, Is.Not.Null, entity);
                Assert.That(All(picture).OfType<SpriteView>().Single().Sprite!.AllLayers.Count(), Is.GreaterThan(1), "Render the complete layered object, not its icon layer.");
                picture.Orphan();
                Assert.That(entities.EntityCount, Is.EqualTo(count), "Closing an illustration must release its local preview.");
            }
        });
        await pair.CleanReturnAsync();
    }
}
