using Robust.Shared.Serialization;

namespace Content.Shared._LuaM.Announcements;

[Serializable, NetSerializable]
public enum ScreenAnnouncementStyle : byte
{
    System,
    Neutral,
    Tsf,
    Vanguard,
    Mmc,
    Ussp,
    Viper,
    Medical,
    Colossus,
    SystemRed,
    SystemPurple,
}

/// <summary>Presentation metadata sent only to the recipients of the corresponding announcement.</summary>
[Serializable, NetSerializable]
public sealed class ScreenAnnouncementEvent : EntityEventArgs
{
    public readonly string Title;
    public readonly string Message;
    public readonly ScreenAnnouncementStyle Style;
    public readonly Color? Accent;
    public readonly AnnouncementPortraitSnapshot? Portrait;

    public ScreenAnnouncementEvent(string title, string message,
        ScreenAnnouncementStyle style = ScreenAnnouncementStyle.System, Color? accent = null,
        AnnouncementPortraitSnapshot? portrait = null)
    {
        Title = title;
        Message = message;
        Style = style;
        Accent = accent;
        Portrait = portrait;
    }
}
