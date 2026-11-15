using System;
using System.Numerics;
using System.Reflection;
using System.Text;
using Content.Client._LuaM.Announcements;
using Robust.Client.Graphics;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class AnnouncementRenderingBudgetTests
{
    [Test]
    public async Task ScrolledLongTextOnlyVisitsVisibleFontMetrics()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        await pair.Client.WaitAssertion(() =>
        {
            var font = new CountingFont();
            using var label = new AnnouncementTextLabel { Typeface = font };
            label.SetText(new string('a', 8192), Color.White);
            label.Measure(new Vector2(80, float.PositiveInfinity));
            label.MinimumVisibleHeight = 10000;
            label.MaximumVisibleHeight = 10100;
            font.DescentCalls = 0;
            var draw = typeof(AnnouncementTextLabel).GetMethod("Draw", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(DrawingHandleScreen) }, null)!;
            // The test font counts work without rendering or reading screenshots.
            draw.Invoke(label, new object[] { null! });
            Assert.That(font.Draws, Is.InRange(1, 300));
            Assert.That(font.DescentCalls, Is.LessThanOrEqualTo(2), "Hidden paragraphs must not make a font call per glyph on every frame.");
        });
        await pair.CleanReturnAsync();
    }

    private sealed class CountingFont : Font
    {
        public int Draws;
        public int DescentCalls;
        public override int GetAscent(float scale) => 12;
        public override int GetHeight(float scale) => 14;
        public override int GetDescent(float scale) { DescentCalls++; return 2; }
        public override int GetLineHeight(float scale) => 16;
        public override CharMetrics? GetCharMetrics(Rune rune, float scale, bool fallback = true) => new CharMetrics(0, 12, 8, 8, 14);
        public override float DrawChar(DrawingHandleBase handle, Rune rune, Vector2 baseline, float scale, Color color, bool fallback = true) { Draws++; return 8; }
        public override float DrawCharOutline(DrawingHandleBase handle, Rune rune, Vector2 baseline, float scale, TextOutline outline, bool fallback = true) => 8;
    }
}
