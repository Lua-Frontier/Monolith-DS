/* LuaM - AGPLv3. See AGPLv3.txt. */
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._LuaM.Guidebook;
using Content.Client.Guidebook;
using Content.Client.Guidebook.Controls;
using Content.Shared.Guidebook;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class GuideTextureTests
{
    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (var child in root.Children)
        {
            yield return child;
            foreach (var nested in Descendants(child))
                yield return nested;
        }
    }

    [Test]
    public async Task IllustrationsFitThePageAndIconsLeaveReadableTextSpace()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            using var image = new GuideTexture();
            Assert.That(image.TryParseTag(new Dictionary<string, string>
            {
                ["Texture"] = "/Textures/_LuaM/Interface/Guidebook/nebula.png",
            }, out _), Is.True);
            foreach (var width in new[] { 320f, 560f, 900f })
            {
                image.Measure(new Vector2(width, float.PositiveInfinity));
                Assert.That(image.DesiredSize.X, Is.LessThanOrEqualTo(width));
                image.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(width, image.DesiredSize.Y)));
                var texture = image.Children.OfType<TextureRect>().Single();
                Assert.That(texture.Size.X, Is.LessThanOrEqualTo(640));
                Assert.That(texture.Size.Y / texture.Size.X, Is.EqualTo(340f / 640f).Within(0.01f));
            }
            using var row = new GuideTexture();
            Assert.That(row.TryParseTag(new Dictionary<string, string>
            {
                ["Texture"] = "/Textures/_Lua/Interface/Radar/nebula_lightning.png",
                ["Scale"] = "1.5",
                ["Text"] = "[bold]Грозовой фронт[/bold] — повреждения корпуса, разряды и ЭМИ. Пауза оружия увеличена на 25%.",
            }, out _), Is.True);
            row.Measure(new Vector2(320, float.PositiveInfinity));
            Assert.That(row.DesiredSize.X, Is.LessThanOrEqualTo(320));
            row.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(320, row.DesiredSize.Y)));
            var icon = row.Children.OfType<TextureRect>().Single();
            var text = row.Children.OfType<RichTextLabel>().Single();
            Assert.That(text.Position.X, Is.GreaterThanOrEqualTo(icon.Position.X + icon.Size.X));
            Assert.That(text.Size.X, Is.GreaterThan(200));

            using var page = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
            var parser = pair.Client.ResolveDependency<DocumentParsingManager>();
            Assert.That(parser.TryAddMarkup(page, new ProtoId<GuideEntryPrototype>("ColossusNebulae")), Is.True);
            Assert.That(Descendants(page).OfType<GuidebookError>().Any(), Is.False);
            Assert.That(Descendants(page).OfType<GuideTexture>().Count(), Is.EqualTo(7), "One radar illustration and six real weather icons.");
            foreach (var width in new[] { 320f, 560f })
            {
                page.Measure(new Vector2(width, float.PositiveInfinity));
                page.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(width, page.DesiredSize.Y)));
                foreach (var illustration in Descendants(page).OfType<GuideTexture>())
                    Assert.That(illustration.Position.X + illustration.Size.X, Is.LessThanOrEqualTo(width));
            }
        });
        await pair.CleanReturnAsync();
    }
}
