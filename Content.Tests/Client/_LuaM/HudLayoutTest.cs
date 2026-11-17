using System.Numerics;
using Content.Client._LuaM.Hud;
using NUnit.Framework;
using Content.Shared._LuaM.Announcements;
using Content.Shared._LuaM.CCVar;

namespace Content.Tests.Client._LuaM;

[TestFixture]
public sealed class HudLayoutTest
{
    [Test]
    public void TitleAnchorDoesNotMoveWhenDescriptionChangesBlockSize()
    {
        var screen = new Vector2(1920, 1017);
        var anchor = new Vector2(0.25f, 0.15f);
        var titleOnly = HudLayout.Position(screen, new Vector2(150, 40), anchor);
        var withDescription = HudLayout.Position(screen, new Vector2(400, 110), anchor);
        Assert.That(titleOnly.Y, Is.EqualTo(withDescription.Y));
        Assert.That(titleOnly.X + 75, Is.EqualTo(withDescription.X + 200));
    }

    [Test]
    public void PositionStaysInsideScreenAfterResize()
    {
        Assert.That(HudLayout.Position(new Vector2(1000, 600), new Vector2(200, 80), Vector2.One),
            Is.EqualTo(new Vector2(800, 520)));
        Assert.That(HudLayout.Position(new Vector2(400, 200), new Vector2(200, 80), Vector2.One),
            Is.EqualTo(new Vector2(200, 120)));
    }

    [Test]
    public void InvalidCoordinatesFallBackAndOversizedBlocksDoNotEscape()
    {
        Assert.That(HudLayout.Normalize(new Vector2(float.NaN, float.PositiveInfinity)),
            Is.EqualTo(HudLayout.DefaultPosition));
        Assert.That(HudLayout.Position(new Vector2(50), new Vector2(100), new Vector2(-2, 3)),
            Is.EqualTo(Vector2.Zero));
    }

    [Test]
    public void DragPositionRoundTripsAndClampsAtEdges()
    {
        var screen = new Vector2(1000, 600);
        var block = new Vector2(200, 100);
        var point = new Vector2(320, 250);
        Assert.That(HudLayout.Position(screen, block, HudLayout.FromPosition(screen, block, point)), Is.EqualTo(point));
        var clamped = HudLayout.FromPosition(screen, block, new Vector2(-100, 900));
        Assert.That(HudLayout.Position(screen, block, clamped), Is.EqualTo(new Vector2(0, 500)));
    }
    [TestCase(HudHorizontalAlignment.Left, 0f)]
    [TestCase(HudHorizontalAlignment.Center, 0.5f)]
    [TestCase(HudHorizontalAlignment.Right, 1f)]
    public void EachHorizontalAnchorRoundTripsAndKeepsItsEdge(HudHorizontalAlignment alignment, float fraction)
    {
        var screen = new Vector2(1000, 600);
        var block = new Vector2(200, 100);
        var anchor = new Vector2(0.5f, 0.3f);
        var position = HudLayout.Position(screen, block, anchor, alignment);
        Assert.That(position.X + block.X * fraction, Is.EqualTo(500));
        var smaller = HudLayout.Position(screen, new Vector2(100, 50), anchor, alignment);
        Assert.That(smaller.X + 100 * fraction, Is.EqualTo(500));
        Assert.That(HudLayout.FromPosition(screen, block, position, alignment), Is.EqualTo(anchor));
        var outside = HudLayout.FromPosition(screen, block, new Vector2(-100, 900), alignment);
        Assert.That(HudLayout.Position(screen, block, outside, alignment), Is.EqualTo(new Vector2(0, 500)));
        foreach (var next in new[] { HudHorizontalAlignment.Left, HudHorizontalAlignment.Center, HudHorizontalAlignment.Right })
        {
            var converted = HudLayout.FromPosition(screen, block, position, next);
            Assert.That(HudLayout.Position(screen, block, converted, next), Is.EqualTo(position));
        }
    }

    [Test]
    public void WheelScaleRejectsInvalidValuesAndKeepsReadableLimits()
    {
        Assert.That(HudLayout.WheelScale(0.65f, -1), Is.EqualTo(0.65f));
        Assert.That(HudLayout.WheelScale(1f, 20, 1), Is.EqualTo(1));
        Assert.That(HudLayout.NormalizeScale(float.NaN), Is.EqualTo(1));
        Assert.That(HudLayout.WheelScale(1f, float.NaN), Is.EqualTo(1));
        Assert.That(HudLayout.WheelScale(1f, 1), Is.EqualTo(1.05f).Within(0.001));
    }

    [TestCase(HudHorizontalAlignment.Left, 0f)]
    [TestCase(HudHorizontalAlignment.Center, 0.5f)]
    [TestCase(HudHorizontalAlignment.Right, 1f)]
    public void OversizedHeadingsKeepTheirConfiguredHorizontalAnchor(HudHorizontalAlignment alignment, float fraction)
    {
        var screen = new Vector2(1000, 600);
        var block = new Vector2(1800, 180);
        var anchor = new Vector2(0.5f, 0.2f);
        var point = HudLayout.Position(screen, block, anchor, alignment, allowHorizontalOverflow: true);
        Assert.That(point.X + block.X * fraction, Is.EqualTo(500));
        Assert.That(HudLayout.FromPosition(screen, block, point, alignment, allowHorizontalOverflow: true), Is.EqualTo(anchor));
    }

    [TestCase(1920, 1080)]
    [TestCase(2560, 1440)]
    [TestCase(3440, 1440)]
    [TestCase(1280, 720)]
    public void DefaultHudAnchorsKeepTheirScreenFractionsAtEveryResolution(int width, int height)
    {
        var screen = new Vector2(width, height);
        var banner = HudLayout.Position(screen, new Vector2(640, 240), HudLayout.DefaultScreenPosition);
        var location = HudLayout.Position(screen, new Vector2(300, 40), HudLayout.DefaultPosition);
        Assert.That(banner.X + 320, Is.EqualTo(width * 0.5f).Within(0.01));
        Assert.That(location.X + 150, Is.EqualTo(width * 0.5f).Within(0.01));
        Assert.That(banner.Y / height, Is.EqualTo(26f / 1080).Within(0.000001));
        Assert.That(location.Y / height, Is.EqualTo(121f / 1080).Within(0.000001));
        Assert.That(LuaMCVars.ScreenAnnouncementY.DefaultValue, Is.EqualTo(HudLayout.DefaultScreenPosition.Y));
        Assert.That(LuaMCVars.LocationAnnouncementY.DefaultValue, Is.EqualTo(HudLayout.DefaultPosition.Y));
    }

    [Test]
    public void InvalidAlignmentFallsBackToCenter() =>
        Assert.That(HudLayout.NormalizeAlignment((HudHorizontalAlignment) 99), Is.EqualTo(HudHorizontalAlignment.Center));
}
