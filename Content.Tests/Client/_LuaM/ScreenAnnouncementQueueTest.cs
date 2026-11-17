using Content.Client._LuaM.Announcements;
using Content.Shared._LuaM.Announcements;
using NUnit.Framework;

namespace Content.Tests.Client._LuaM;

[TestFixture]
public sealed class ScreenAnnouncementQueueTest
{
    [Test]
    public void AnnouncementsStayInOrderAndAdvanceOnlyAfterTheirDuration()
    {
        var queue = new ScreenAnnouncementQueue();
        var first = new ScreenAnnouncementEvent("First", "First message");
        var second = new ScreenAnnouncementEvent("Second", "Second message");
        queue.Enqueue(first);
        queue.Enqueue(second);
        queue.Advance(queue.Duration - 0.1f);
        Assert.That(queue.Current, Is.SameAs(first));
        queue.Advance(0.2f);
        Assert.That(queue.Current, Is.SameAs(second));
        queue.Advance(queue.Duration);
        Assert.That(queue.Current, Is.Null);
    }

    [Test]
    public void HoverPausesReadingButCannotInterruptOrReverseFading()
    {
        var queue = new ScreenAnnouncementQueue();
        queue.Enqueue(new ScreenAnnouncementEvent("Title", "Message"));
        Assert.That(queue.Opacity, Is.EqualTo(1), "Arrival must not fade the portrait in.");
        queue.Advance(2);
        queue.Advance(1, pauseForReading: true);
        Assert.That(queue.Elapsed, Is.EqualTo(2));
        Assert.That(queue.Opacity, Is.EqualTo(1));
        queue.Advance(queue.Duration - queue.Elapsed - ScreenAnnouncementQueue.FadeOutDuration + 0.1f);
        Assert.That(queue.IsFading, Is.True);
        var before = queue.Opacity;
        queue.Advance(0.1f, pauseForReading: true);
        Assert.That(queue.Opacity, Is.LessThan(before), "A pointer entering during fade cannot resurrect the banner.");
        queue.Advance(1, pauseForReading: true);
        Assert.That(queue.Current, Is.Null);
        Assert.That(queue.Opacity, Is.Zero);
    }

    [Test]
    public void BurstIsBoundedAndClearDropsPendingMessages()
    {
        var queue = new ScreenAnnouncementQueue();
        for (var i = 0; i < 100; i++)
            queue.Enqueue(new ScreenAnnouncementEvent("Event", "Message"));
        Assert.That(queue.PendingCount, Is.EqualTo(ScreenAnnouncementQueue.MaximumPending));
        queue.Clear();
        Assert.That(queue.Current, Is.Null);
        Assert.That(queue.PendingCount, Is.Zero);
    }

    [Test]
    public void EmptyMessagesAndInvalidFrameTimesDoNotChangePlayback()
    {
        var queue = new ScreenAnnouncementQueue();
        Assert.That(queue.Enqueue(new ScreenAnnouncementEvent("Empty", "  ")), Is.False);
        queue.Enqueue(new ScreenAnnouncementEvent("Event", "Message"));
        queue.Advance(float.NaN);
        queue.Advance(-1);
        Assert.That(queue.Elapsed, Is.Zero);
        Assert.That(queue.Current, Is.Not.Null);
    }
}
