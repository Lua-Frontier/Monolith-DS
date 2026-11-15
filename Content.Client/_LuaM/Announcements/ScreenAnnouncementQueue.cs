using Content.Shared._LuaM.Announcements;

namespace Content.Client._LuaM.Announcements;

/// <summary>One readable card at a time, with bounded backlog during announcement bursts.</summary>
public sealed class ScreenAnnouncementQueue
{
    public const int MaximumPending = 16;
    public const float FadeOutDuration = 0.45f;
    public bool IsFading => Current != null && Elapsed >= Duration - FadeOutDuration;
    public float Opacity => Current == null ? 0 : Math.Clamp((Duration - Elapsed) / FadeOutDuration, 0, 1);
    private readonly Queue<ScreenAnnouncementEvent> _pending = new();
    public ScreenAnnouncementEvent? Current { get; private set; }
    public float Elapsed { get; private set; }
    public int PendingCount => _pending.Count;
    public float Duration => Current == null ? 0 : Math.Clamp(3 + Current.Message.Length * 0.045f, 6, 24);

    public bool Enqueue(ScreenAnnouncementEvent announcement)
    {
        if (string.IsNullOrWhiteSpace(announcement.Message))
            return false;
        if (Current == null)
        {
            Current = announcement;
            Elapsed = 0;
            return true;
        }
        if (_pending.Count >= MaximumPending)
            return false;
        _pending.Enqueue(announcement);
        return true;
    }

    public void Advance(float elapsed, bool pauseForReading = false)
    {
        if (!float.IsFinite(elapsed) || elapsed <= 0 || Current == null)
            return;
        // Once fading starts, hovering cannot revive the card or stall its departure.
        if (pauseForReading && !IsFading) return;
        Elapsed += elapsed;
        while (Current != null && Elapsed >= Duration)
        {
            Elapsed -= Duration;
            Current = _pending.TryDequeue(out var next) ? next : null;
        }
        if (Current == null)
            Elapsed = 0;
    }

    public void Clear()
    {
        Current = null;
        Elapsed = 0;
        _pending.Clear();
    }
}
