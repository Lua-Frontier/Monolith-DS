using Content.Shared._LuaM.Announcements; // LuaM
using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.Administration
{
    public enum AdminAnnounceType
    {
        Station,
        Server,
        Antag, // Frontier
        Overwatch, // LuaM: faction-only administrative transmission.
    }

    // LuaM: faction delivery accepts only styles with a real company audience.
    public static class AdminAnnounceStyles
    {
        public static bool IsFaction(ScreenAnnouncementStyle style) => style is
            ScreenAnnouncementStyle.Tsf or ScreenAnnouncementStyle.Vanguard or ScreenAnnouncementStyle.Mmc or
            ScreenAnnouncementStyle.Ussp or ScreenAnnouncementStyle.Viper or ScreenAnnouncementStyle.Medical or
            ScreenAnnouncementStyle.Colossus;
    }

    // LuaM-start: portrait sources are resolved and validated by the server.
    [Serializable, NetSerializable]
    public enum AdminAnnouncePortraitMode : byte
    {
        None,
        Self,
        Entity,
    }
    // LuaM-end

    [Serializable, NetSerializable]
    public sealed class AdminAnnounceEuiState : EuiStateBase
    {
        public string ErrorMessage = string.Empty; // LuaM
    }

    public static class AdminAnnounceEuiMsg
    {
        [Serializable, NetSerializable]
        public sealed class DoAnnounce : EuiMessageBase
        {
            public bool CloseAfter;
            public string Announcer = default!;
            public string Announcement = default!;
            public AdminAnnounceType AnnounceType;
            // LuaM-start
            public ScreenAnnouncementStyle ScreenStyle = ScreenAnnouncementStyle.System;
            public AdminAnnouncePortraitMode PortraitMode;
            public string PortraitEntity = string.Empty;
            public string PortraitName = string.Empty;
            public bool PlaySound = true;
            // LuaM-end
// Corvax-TTS-start:
            public string Voice = default!;
            public bool EnableTTS;
// Corvax-TTS-end.
        }
    }
}
