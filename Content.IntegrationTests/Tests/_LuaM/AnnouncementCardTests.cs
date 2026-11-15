using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._LuaM.Announcements;
using Content.Shared._LuaM.Announcements;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class AnnouncementCardTests
{
    [Test]
    public async Task MetadataTypesTogetherAndPortraitRestsOnItsInnerBottomBorder()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        await pair.Client.WaitAssertion(() =>
        {
            using var card = new ScreenAnnouncementCard();
            card.SetAnnouncement(new ScreenAnnouncementEvent("A long command heading", new string('a', 4000),
                portrait: new AnnouncementPortraitSnapshot { Prototype = "MobHuman", Species = "Human", Name = "A long character name" }));
            card.Measure(new Vector2(1000));
            card.Arrange(UIBox2.FromDimensions(Vector2.Zero, card.DesiredSize));
            card.SetElapsed(0.02f);
            var title = Descendants(card).OfType<AnnouncementTextLabel>().Single(c => c.Name == "AnnouncementTitle");
            var name = Descendants(card).OfType<AnnouncementTextLabel>().Single(c => c.Name == "AnnouncementSpeakerName");
            var body = Descendants(card).OfType<AnnouncementTextLabel>().Single(c => c.Name == "AnnouncementBody");
            var projection = Descendants(card).Single(c => c.Name == "AnnouncementProjection");
            var portrait = Descendants(card).OfType<AnnouncementPortraitView>().Single();
            Assert.Multiple(() =>
            {
                Assert.That(title.VisibleUtf16Length, Is.GreaterThan(0));
                Assert.That(name.VisibleUtf16Length, Is.GreaterThan(0));
                Assert.That(body.VisibleUtf16Length, Is.Zero);
                Assert.That(portrait.Visible, Is.True);
                Assert.That(portrait.Position.Y + portrait.Height, Is.EqualTo(projection.Height - 1).Within(0.5));
                Assert.That(projection.Position.X, Is.GreaterThanOrEqualTo(28));
                Assert.That(card.Width - title.Position.X - title.Width, Is.GreaterThanOrEqualTo(128));
                Assert.That(card.Height - projection.Position.Y - projection.Height - name.Height - 5, Is.GreaterThanOrEqualTo(29));
                Assert.That(((Robust.Client.Graphics.VectorFont) title.Typeface!).Size, Is.EqualTo(20));
                Assert.That(((Robust.Client.Graphics.VectorFont) body.Typeface!).Size, Is.EqualTo(14));
            });
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SmallCardArtworkFitsAndBodyStartsAtTheProjectionTop()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        await pair.Client.WaitAssertion(() =>
        {
            using var card = new ScreenAnnouncementCard { PresentationScale = 0.65f };
            card.SetAnnouncement(new ScreenAnnouncementEvent("Title", "Short body"));
            card.Measure(new Vector2(1000));
            card.Arrange(UIBox2.FromDimensions(Vector2.Zero, card.DesiredSize));
            var projection = Descendants(card).Single(c => c.Name == "AnnouncementProjection");
            var emblem = Descendants(card).OfType<TextureRect>().Single(c => c.Name == "AnnouncementInsignia");
            var dimensions = (UIBox2) typeof(TextureRect).GetMethod("GetDrawDimensions", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(emblem, new object[] { emblem.Texture! })!;
            var body = Descendants(card).OfType<AnnouncementTextLabel>().Single(c => c.Name == "AnnouncementBody");
            Assert.Multiple(() =>
            {
                Assert.That(dimensions.Width, Is.LessThanOrEqualTo(projection.PixelWidth));
                Assert.That(dimensions.Height, Is.LessThanOrEqualTo(projection.PixelHeight));
                Assert.That(body.GlobalPixelPosition.Y, Is.EqualTo(projection.GlobalPixelPosition.Y).Within(1));
            });
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SingleRowHeadingCanOutgrowTheViewportAndColoursFollowTheBanner()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        await pair.Client.WaitAssertion(() =>
        {
            using var card = new ScreenAnnouncementCard();
            card.SetAnnouncement(new ScreenAnnouncementEvent(new string('Ж', 120), "Body", ScreenAnnouncementStyle.Tsf));
            card.Measure(new Vector2(320, 300));
            Assert.That(card.DesiredSize.X, Is.GreaterThan(640));
            var title = Descendants(card).OfType<AnnouncementTextLabel>().Single(c => c.Name == "AnnouncementTitle");
            Assert.That(title.DesiredSize.Y, Is.LessThanOrEqualTo(title.Typeface!.GetHeight(title.UIScale) / title.UIScale));
            card.Arrange(UIBox2.FromDimensions(Vector2.Zero, card.DesiredSize));
            Assert.That(title.Width, Is.GreaterThanOrEqualTo(title.DesiredSize.X));
            var wideTitle = title.DesiredSize.X;
            card.PresentationScale = 0.65f;
            card.Measure(new Vector2(320, 300));
            Assert.That(title.DesiredSize.X, Is.LessThan(wideTitle * 0.8f), "Unbounded measure must still refresh font metrics on scale changes.");
            var blue = card.MessageColor;
            card.SetAnnouncement(new ScreenAnnouncementEvent("System", "Body", accent: Color.Red));
            card.Measure(new Vector2(1000));
            var yellow = card.MessageColor;
            Assert.That(blue.B, Is.GreaterThan(blue.R));
            Assert.That(yellow.R, Is.GreaterThan(yellow.B));
            card.SetAnnouncement(new ScreenAnnouncementEvent("System", "Body", accent: Color.Green));
            Assert.That(card.MessageColor, Is.EqualTo(yellow), "Alert colours must not override the banner's tint.");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task EveryStyleUsesSeparateEmblemAndFixedPortraitProjection()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        await pair.Client.WaitAssertion(() =>
        {
            foreach (var style in Enum.GetValues<ScreenAnnouncementStyle>())
            {
                using var card = new ScreenAnnouncementCard();
                card.SetAnnouncement(new ScreenAnnouncementEvent("Sender", "Message", style));
                card.Measure(new Vector2(744, 300));
                card.Arrange(UIBox2.FromDimensions(Vector2.Zero, card.DesiredSize));
                var emblem = Descendants(card).OfType<TextureRect>().Single(c => c.Name == "AnnouncementInsignia");
                var portrait = Descendants(card).OfType<AnnouncementPortraitView>().Single();
                var projection = Descendants(card).Single(c => c.Name == "AnnouncementProjection");
                Assert.That(emblem.Texture, Is.Not.Null);
                Assert.That(emblem.Visible, Is.True);
                Assert.That(portrait.Visible, Is.False);
                Assert.That(projection.Size, Is.EqualTo(new Vector2(133, 103)));
                card.SetAnnouncement(new ScreenAnnouncementEvent("Sender", "Message", style,
                    portrait: new AnnouncementPortraitSnapshot { Prototype = "MobHuman", Species = "Human" }));
                card.Measure(new Vector2(1100, 300));
                card.Arrange(UIBox2.FromDimensions(Vector2.Zero, card.DesiredSize));
                Assert.That(emblem.Visible, Is.False);
                Assert.That(portrait.Visible, Is.True);
                Assert.That(portrait.Size, Is.EqualTo(new Vector2(100, 77)));
                Assert.That(projection.Size, Is.EqualTo(new Vector2(133, 103)));
                Assert.That(portrait.PreviewEntity, Is.Null);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LongMessagesScrollInsideTheSameFixedFrame()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        await pair.Client.WaitAssertion(() =>
        {
            using var card = new ScreenAnnouncementCard();
            var shortSize = Vector2.Zero;
            foreach (var text in new[] { "Короткое сообщение", new string('Ж', 4000) })
            {
                card.SetAnnouncement(new ScreenAnnouncementEvent("Системы оповещений", text));
                card.Measure(new Vector2(1000));
                Assert.That(card.DesiredSize.X, Is.LessThanOrEqualTo(640));
                Assert.That(card.DesiredSize.Y, Is.LessThan(220));
                if (text.Length < 1000) shortSize = card.DesiredSize;
                else
                {
                    Assert.That(card.DesiredSize.X, Is.GreaterThan(shortSize.X));
                    Assert.That(card.DesiredSize.Y, Is.EqualTo(shortSize.Y));
                }
                card.Arrange(UIBox2.FromDimensions(Vector2.Zero, card.DesiredSize));
                Assert.That(Descendants(card).OfType<ScrollContainer>().Count(), Is.EqualTo(1));
                Assert.That(Descendants(card).Any(c => c.Name == "AnnouncementOverflow"), Is.False);
                if (text.Length > 1000)
                {
                    var scroll = Descendants(card).OfType<ScrollContainer>().Single();
                    var body = Descendants(card).OfType<AnnouncementTextLabel>().Single(c => c.Name == "AnnouncementBody");
                    var layouts = body.LayoutBuildCount;
                    scroll.VScrollTarget = 200;
                    Assert.That(scroll.VScrollTarget, Is.GreaterThan(0));
                    scroll.SetScrollValue(new Vector2(0, scroll.VScrollTarget));
                    card.Arrange(UIBox2.FromDimensions(Vector2.Zero, card.DesiredSize));
                    Assert.That(scroll.VScroll, Is.GreaterThan(0));
                    Assert.That(body.LayoutBuildCount, Is.EqualTo(layouts));
                }
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PortraitNamesFitThreeLinesAndPreserveRevealAcrossScaling()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        await pair.Client.WaitAssertion(() =>
        {
            foreach (var name in new[] { "Алексей Иванов", "Ки", "Свет-Проходящий-Сквозь-Туман", "Мария Анна де ла Крус", new string('Ж', 256), "\U0001F469\u200D\U0001F680 Звёздный Свет" })
            {
                using var card = new ScreenAnnouncementCard();
                card.SetAnnouncement(new ScreenAnnouncementEvent("Командование", "Текст", portrait:
                    new AnnouncementPortraitSnapshot { Prototype = "MobHuman", Species = "Human", Name = name }));
                foreach (var scale in new[] { 1f, 0.65f })
                {
                    card.PresentationScale = scale;
                    card.SetProgress(0);
                    card.Measure(new Vector2(1000));
                    card.Arrange(UIBox2.FromDimensions(Vector2.Zero, card.DesiredSize));
                    var caption = Descendants(card).OfType<AnnouncementTextLabel>().Single(c => c.Name == "AnnouncementSpeakerName");
                    Assert.That(caption.VisibleUtf16Length, Is.Zero);
                    Assert.That(caption.DesiredSize.X, Is.LessThanOrEqualTo(133 * scale + 1));
                    Assert.That(caption.DesiredSize.Y, Is.LessThanOrEqualTo((2 * (int) (caption.Typeface!.GetLineHeight(caption.UIScale) * caption.LineHeightScale) + caption.Typeface.GetHeight(caption.UIScale)) / caption.UIScale + 0.01f));
                    card.SetProgress(1);
                    Assert.That(caption.VisibleUtf16Length, Is.GreaterThan(0));
                    Assert.That(caption.MaximumLines, Is.EqualTo(3));
                    Assert.That(caption.FadeLastLine, Is.True);
                    Assert.That(caption.VerticalAlignment, Is.EqualTo(Control.VAlignment.Top));
                }
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BodyTypesAtSeventyPercentSpeedWithoutDelayingTheHeading()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        await pair.Client.WaitAssertion(() =>
        {
            using var card = new ScreenAnnouncementCard();
            card.SetAnnouncement(new ScreenAnnouncementEvent("Title", new string('a', 100)));
            card.Measure(new Vector2(1000));
            var body = Descendants(card).OfType<AnnouncementTextLabel>().Single(c => c.Name == "AnnouncementBody");
            var title = Descendants(card).OfType<AnnouncementTextLabel>().Single(c => c.Name == "AnnouncementTitle");
            card.SetElapsed(1.2f);
            Assert.That(title.VisibleUtf16Length, Is.EqualTo(5));
            Assert.That(body.VisibleUtf16Length, Is.LessThan(100));
            Assert.That(card.RevealDuration, Is.GreaterThan(1.2f));
            card.SetElapsed(card.RevealDuration + 0.001f);
            Assert.That(body.VisibleUtf16Length, Is.EqualTo(100));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UnarrangedAnnouncementDoesNotSubmitDrawingCommands()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        await pair.Client.WaitAssertion(() =>
        {
            using var card = new ScreenAnnouncementCard();
            card.SetAnnouncement(new ScreenAnnouncementEvent("System", "A received announcement."));
            Assert.That(card.PixelSize, Is.EqualTo(Robust.Shared.Maths.Vector2i.Zero));
            var draw = typeof(ScreenAnnouncementCard).GetMethod("Draw",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new[] { typeof(Robust.Client.Graphics.DrawingHandleScreen) }, null)!;
            // There is no drawable geometry before layout; even a missing handle must not be used.
            Assert.DoesNotThrow(() => draw.Invoke(card, new object[] { null }));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WrappedAnnouncementKeepsItsLayoutThroughoutReveal()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        await pair.Client.WaitAssertion(() =>
        {
            foreach (var width in new[] { 320f, 640f, 1100f })
            foreach (var style in Enum.GetValues<ScreenAnnouncementStyle>())
            {
                using var card = new ScreenAnnouncementCard();
                var message = string.Join(" ", Enumerable.Repeat("Экипажи, следуйте к шлюзу. 🚀 е\u0308 [color=red]Текст[/color]", 12));
                card.SetAnnouncement(new ScreenAnnouncementEvent(
                    "Объявление: [bold]это буквальный текст[/bold]",
                    message, style));
                var available = new Vector2(width, 10000);
                card.SetProgress(0);
                card.Measure(available);
                var initial = card.DesiredSize;
                card.Arrange(UIBox2.FromDimensions(Vector2.Zero, initial));
                var title = Descendants(card).OfType<AnnouncementTextLabel>().Single(c => c.Name == "AnnouncementTitle");
                var revealBody = Descendants(card).OfType<AnnouncementTextLabel>().Single(c => c.Name == "AnnouncementBody");
                Assert.That(title.VisibleUtf16Length, Is.Zero, "Sender must not appear in full before the typing starts.");
                Assert.That(revealBody.VisibleUtf16Length, Is.Zero);
                var titleLayouts = title.LayoutBuildCount;
                var bodyLayouts = revealBody.LayoutBuildCount;
                Assert.That(title.WrapText, Is.False);
                Assert.That(initial.Y, Is.GreaterThan(80));
                foreach (var progress in new[] { 0.01f, 0.37f, 1f })
                {
                    card.SetProgress(progress);
                    card.Measure(available);
                    Assert.That(card.DesiredSize, Is.EqualTo(initial), "Reveal must not move or resize the card.");
                }
                Assert.That(title.LayoutBuildCount, Is.EqualTo(titleLayouts), "Typing must reuse the sender glyph layout.");
                Assert.That(revealBody.LayoutBuildCount, Is.EqualTo(bodyLayouts), "Typing must reuse the body glyph layout.");
                AssertIgnored(card);
                var body = Descendants(card).OfType<RichTextLabel>().Single(c => c.Name == "AnnouncementBody");
                Assert.That(body.GetFormattedMessage()!.ToString(), Is.EqualTo(message), "Player markup must remain literal.");
                card.MaxHeight = 100;
                card.Measure(available);
                card.Arrange(UIBox2.FromDimensions(Vector2.Zero, card.DesiredSize));
                // Visibility changes during arrange need a second normal layout pass.
                card.Measure(available);
                card.Arrange(UIBox2.FromDimensions(Vector2.Zero, card.DesiredSize));
                Assert.That(card.Height, Is.LessThanOrEqualTo(100));
                Assert.That(Descendants(card).OfType<ScrollContainer>().Single().VScrollEnabled, Is.True);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RevealKeepsCombinedUnicodeCharactersAndNeverReflows()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        await pair.Client.WaitAssertion(() =>
        {
            using var label = new AnnouncementTextLabel();
            label.SetText("A e\u0308 👩‍🚀", Color.White);
            label.Measure(new Vector2(320, 1000));
            var builds = label.LayoutBuildCount;
            var size = label.DesiredSize;
            label.VisibleElements = 3;
            Assert.That(label.VisibleUtf16Length, Is.EqualTo(4));
            label.VisibleElements = 5;
            Assert.That(label.VisibleUtf16Length, Is.EqualTo("A e\u0308 👩‍🚀".Length));
            for (var i = 0; i < 120; i++)
            {
                label.VisibleElements = i % 6;
                label.Measure(new Vector2(320, 1000));
                Assert.That(label.DesiredSize, Is.EqualTo(size));
            }
            Assert.That(label.LayoutBuildCount, Is.EqualTo(builds));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OversizedMessagesHaveBoundedLayoutAndPointToFullChatText()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        await pair.Client.WaitAssertion(() =>
        {
            using var card = new ScreenAnnouncementCard { MaxHeight = 180 };
            card.SetAnnouncement(new ScreenAnnouncementEvent("System", new string('x', 100_000)));
            card.Measure(new Vector2(640, 800));
            card.Arrange(UIBox2.FromDimensions(Vector2.Zero, card.DesiredSize));
            var body = Descendants(card).OfType<AnnouncementTextLabel>().Single(c => c.Name == "AnnouncementBody");
            Assert.That(body.GetFormattedMessage()!.ToString().Length, Is.EqualTo(ScreenAnnouncementCard.MaximumBodyLength));
            Assert.That(Descendants(card).OfType<ScrollContainer>().Single().Height, Is.LessThanOrEqualTo(card.Height));
            Assert.That(Descendants(card).OfType<ScrollContainer>().Single().VScrollEnabled, Is.True);
        });
        await pair.CleanReturnAsync();
    }

    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (var child in control.Children)
        {
            yield return child;
            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    private static void AssertIgnored(Control control)
    {
        var expected = control is ScrollContainer ? Control.MouseFilterMode.Pass
            : control is ScrollBar ? Control.MouseFilterMode.Stop : Control.MouseFilterMode.Ignore;
        Assert.That(control.MouseFilter, Is.EqualTo(expected));
        foreach (var child in control.Children)
            AssertIgnored(child);
    }
}
