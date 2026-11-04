using Robust.Shared.Configuration;

namespace Content.Shared._LuaM.CCVar;

[CVarDefs]
public sealed class LuaMCVars
{
    public static readonly CVarDef<bool> RpPanelEnabled =
        CVarDef.Create("luam.rp_panel_enabled", true, CVar.CLIENT | CVar.ARCHIVE | CVar.REPLICATED);

    public static readonly CVarDef<bool> RpLungeEnabled =
        CVarDef.Create("luam.rp_lunge_enabled", true, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<string> RpFavoriteInteractions =
        CVarDef.Create("luam.rp_favorite_interactions", string.Empty, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<float> AnnouncementVolume =
        CVarDef.Create("luam.announcement_volume", 1f, CVar.ARCHIVE | CVar.CLIENTONLY);

    public static readonly CVarDef<int> ActionsActivePreset =
        CVarDef.Create("luam.actions_active_preset", 0, CVar.ARCHIVE | CVar.CLIENTONLY);
}
