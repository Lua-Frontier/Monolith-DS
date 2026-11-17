using Content.Shared._LuaM.Announcements;
using Robust.Shared.Player;

namespace Content.Server._LuaM.Announcements;

/// <summary>Routes presentation metadata alongside announcements without changing their chat or audio.</summary>
public sealed partial class ScreenAnnouncementSystem : EntitySystem
{
    [Dependency] private AnnouncementPortraitCaptureSystem _portraits = default!;

    public void Send(string title, string message, ScreenAnnouncementStyle style, Color? accent, Filter? filter = null, EntityUid? speaker = null, string? portraitName = null)
    {
        var portrait = _portraits.Capture(speaker);
        if (portrait != null && !string.IsNullOrWhiteSpace(portraitName))
            portrait.Name = portraitName.Trim()[..Math.Min(portraitName.Trim().Length, 128)];
        var ev = new ScreenAnnouncementEvent(title, message, style, accent, portrait);
        if (filter == null)
            RaiseNetworkEvent(ev);
        else
            RaiseNetworkEvent(ev, filter);
    }
}
