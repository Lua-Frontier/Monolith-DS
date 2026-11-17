using Content.Shared._LuaM.Announcements;
using Content.Shared._Rat.Overwatch;
using Robust.Client.UserInterface;

namespace Content.Client._LuaM.Announcements;

public sealed partial class ScreenAnnouncementClientSystem : EntitySystem
{
    [Dependency] private IUserInterfaceManager _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ScreenAnnouncementEvent>(OnAnnouncement);
        SubscribeNetworkEvent<OverwatchAnnouncementEvent>(OnOverwatchAnnouncement);
    }

    private void OnAnnouncement(ScreenAnnouncementEvent args) =>
        _ui.GetUIController<ScreenAnnouncementUIController>().Enqueue(args);

    private void OnOverwatchAnnouncement(OverwatchAnnouncementEvent args)
    {
        var title = Loc.GetString("overwatch-announcement-title",
            ("overwatchTitle", args.OverwatchTitle), ("targetName", args.TargetName));
        var style = args.Faction switch
        {
            "TSF" or "TSFCivilian" or "TSFHighComm" => ScreenAnnouncementStyle.Tsf,
            "PDV" or "PDVCivilian" or "PDVHighComm" => ScreenAnnouncementStyle.Vanguard,
            "USSP" => ScreenAnnouncementStyle.Ussp,
            "TheViperGroup" => ScreenAnnouncementStyle.Viper,
            "MMC" => ScreenAnnouncementStyle.Mmc,
            "MD" => ScreenAnnouncementStyle.Medical,
            "System" => ScreenAnnouncementStyle.System,
            "SystemRed" => ScreenAnnouncementStyle.SystemRed,
            "SystemPurple" => ScreenAnnouncementStyle.SystemPurple,
            "Colonial" => ScreenAnnouncementStyle.Colossus,
            _ => ScreenAnnouncementStyle.Neutral,
        };
        OnAnnouncement(new ScreenAnnouncementEvent(title, args.Message, style, args.Color, args.Portrait));
    }

    public override void Shutdown()
    {
        _ui.GetUIController<ScreenAnnouncementUIController>().Clear();
        base.Shutdown();
    }
}
