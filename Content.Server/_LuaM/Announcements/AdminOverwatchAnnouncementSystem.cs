using Content.Server.Administration.Logs;
using Content.Server._Rat.Overwatch;
using Content.Shared.Administration;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Shared._LuaM.Announcements;
using Content.Shared._Mono.Company;
using Content.Shared._Rat.Overwatch;
using Content.Shared.Chat;
using Content.Shared.Database;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._LuaM.Announcements;

/// <summary>
/// Administrative Overwatch transmissions use the real Overwatch event/chat format, with an
/// explicit company audience, using the same recipient and jamming rules as its console.
/// </summary>
public sealed partial class AdminOverwatchAnnouncementSystem : EntitySystem
{
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private ChatSystem _chatSystem = default!;
    [Dependency] private AnnouncementPortraitCaptureSystem _portraits = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;

    public (string Faction, string Title, Color Color) GetPresentation(ScreenAnnouncementStyle style)
    {
        var (faction, titleKey) = style switch
        {
            ScreenAnnouncementStyle.Tsf => ("TSF", "overwatch-title-tsf"),
            ScreenAnnouncementStyle.Vanguard => ("PDV", "overwatch-title-pdv"),
            ScreenAnnouncementStyle.Mmc => ("MMC", "overwatch-title-mmc"),
            ScreenAnnouncementStyle.Ussp => ("USSP", "overwatch-title-ussp"),
            ScreenAnnouncementStyle.Viper => ("TheViperGroup", "overwatch-title-thevipergroup"),
            ScreenAnnouncementStyle.Medical => ("MD", "overwatch-title-md"),
            ScreenAnnouncementStyle.Colossus => ("Colonial", "overwatch-title-colonial"),
            ScreenAnnouncementStyle.System => ("System", "chat-manager-sender-announcement"),
            ScreenAnnouncementStyle.SystemRed => ("SystemRed", "chat-manager-sender-announcement"),
            ScreenAnnouncementStyle.SystemPurple => ("SystemPurple", "chat-manager-sender-announcement"),
            _ => ("", "overwatch-title-default"),
        };
        var color = style switch
        {
            ScreenAnnouncementStyle.SystemRed => Color.FromHex("#EF5B5B"),
            ScreenAnnouncementStyle.SystemPurple => Color.FromHex("#BB87ED"),
            ScreenAnnouncementStyle.Neutral => Color.FromHex("#A7C6D7"),
            _ => Color.Gold,
        };
        if (faction.Length > 0 && _prototypes.TryIndex<CompanyPrototype>(faction, out var company))
            color = company.Color;
        return (faction, Loc.GetString(titleKey), color);
    }

    public Filter Send(string message, ScreenAnnouncementStyle style, string? title = null,
        EntityUid? speaker = null, string? portraitName = null, bool playSound = true)
    {
        if (!AdminAnnounceStyles.IsFaction(style))
            return Filter.Empty();
        var presentation = GetPresentation(style);
        var filter = EntityManager.System<OverwatchSystem>().GetAnnouncementRecipients(presentation.Faction);
        title = string.IsNullOrWhiteSpace(title) ? presentation.Title : title.Trim();
        var portrait = _portraits.Capture(speaker);
        if (portrait != null && !string.IsNullOrWhiteSpace(portraitName))
            portrait.Name = portraitName.Trim();
        var target = Loc.GetString("overwatch-announcement-target-all");
        RaiseNetworkEvent(new OverwatchAnnouncementEvent(message, target, title,
            presentation.Color, presentation.Faction, portrait), filter);
        var wrapped = $"{FormattedMessage.EscapeText(title)}: {FormattedMessage.EscapeText(message)}";
        _chat.ChatMessageToManyFiltered(filter, ChatChannel.Local, message, wrapped, EntityUid.Invalid,
            hideChat: false, recordReplay: true, colorOverride: presentation.Color);
        if (playSound)
            _chatSystem.PlayAnnouncementSound(filter);
        _adminLog.Add(LogType.Chat, LogImpact.Low, $"Administrative faction announcement from {title} ({presentation.Faction}): {message}");
        return filter;
    }
}
