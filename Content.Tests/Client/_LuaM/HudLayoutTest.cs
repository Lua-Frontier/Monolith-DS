using System.Numerics;
using Content.Client._LuaM.Hud;
using NUnit.Framework;

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
}
