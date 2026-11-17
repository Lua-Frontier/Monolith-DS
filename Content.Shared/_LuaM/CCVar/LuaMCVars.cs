using Robust.Shared.Configuration;

namespace Content.Shared._LuaM.CCVar;

[CVarDefs]
public sealed class LuaMCVars
{
    public const float DefaultScreenAnnouncementX = 0.5f;
    public const float DefaultScreenAnnouncementY = 0.024074074f;
    public const float DefaultLocationAnnouncementX = 0.5f;
    public const float DefaultLocationAnnouncementY = 0.11203704f;
    public static readonly CVarDef<float> ScreenAnnouncementScale =
        CVarDef.Create("luam.screen_announcement_scale", 1f, CVar.ARCHIVE | CVar.CLIENTONLY);
    public static readonly CVarDef<float> LocationAnnouncementScale =
        CVarDef.Create("luam.location_announcement_scale", 1f, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<bool> ScreenAnnouncementsEnabled =
        CVarDef.Create("luam.screen_announcements_enabled", true, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<float> ScreenAnnouncementX =
        CVarDef.Create("luam.screen_announcement_x", DefaultScreenAnnouncementX, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<float> ScreenAnnouncementY =
        CVarDef.Create("luam.screen_announcement_y", DefaultScreenAnnouncementY, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<int> ScreenAnnouncementAlignment =
        CVarDef.Create("luam.screen_announcement_alignment", 1, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<int> LocationAnnouncementAlignment =
        CVarDef.Create("luam.location_announcement_alignment", 1, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<float> LocationAnnouncementX =
        CVarDef.Create("luam.location_announcement_x", DefaultLocationAnnouncementX, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<float> LocationAnnouncementY =
        CVarDef.Create("luam.location_announcement_y", DefaultLocationAnnouncementY, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<bool> RpPanelEnabled =
        CVarDef.Create("luam.rp_panel_enabled", true, CVar.CLIENT | CVar.ARCHIVE | CVar.REPLICATED);

    public static readonly CVarDef<bool> RpLungeEnabled =
        CVarDef.Create("luam.rp_lunge_enabled", true, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<string> RpFavoriteInteractions =
        CVarDef.Create("luam.rp_favorite_interactions", string.Empty, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<string> DeliveryFavorites =
        CVarDef.Create("luam.delivery_favorites", string.Empty, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<string> ConstructionFavorites =
        CVarDef.Create("luam.construction_favorites", string.Empty, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<float> AnnouncementVolume =
        CVarDef.Create("luam.announcement_volume", 1f, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<int> ActionsActivePreset =
        CVarDef.Create("luam.actions_active_preset", 0, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<bool> AdvancedHotbar =
        CVarDef.Create("luam.advanced_hotbar", false, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<bool> SpaceWindVisuals =
        CVarDef.Create("luam.space_wind_visuals", true, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<bool> RoundIntroEnabled =
        CVarDef.Create("luam.round_intro", true, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<bool> LobbyBackgroundParallax =
        CVarDef.Create("luam.lobby_background_parallax", true, CVar.ARCHIVE | CVar.CLIENTONLY);
}
