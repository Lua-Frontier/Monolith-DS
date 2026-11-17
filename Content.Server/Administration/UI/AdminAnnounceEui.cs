using Content.Shared._LuaM.Announcements; // LuaM
using Content.Server._LuaM.Announcements; // LuaM
using Content.Server.Administration.Managers;
using Content.Server.Chat;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.EUI;
using Content.Shared.Administration;
using Content.Shared.Chat; // Corvax-TTS
using Content.Shared.Eui;
using Content.Shared.Humanoid; // LuaM
using Robust.Shared.Audio; // Frontier
using Robust.Shared.GameObjects; // Corvax-TTS
using Robust.Shared.Player; // Corvax-TTS

namespace Content.Server.Administration.UI
{
    public sealed partial class AdminAnnounceEui : BaseEui
    {
        [Dependency] private IAdminManager _adminManager = default!;
        [Dependency] private IChatManager _chatManager = default!;
        [Dependency] private IEntityManager _entityManager = default!; // Corvax-TTS
        private readonly ChatSystem _chatSystem;
        private string _errorMessage = string.Empty; // LuaM

        public AdminAnnounceEui()
        {
            IoCManager.InjectDependencies(this);
            _chatSystem = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<ChatSystem>();
        }

        public override void Opened()
        {
            StateDirty();
        }

        public override EuiStateBase GetNewState()
        {
            return new AdminAnnounceEuiState { ErrorMessage = _errorMessage }; // LuaM
        }

        public override void HandleMessage(EuiMessageBase msg)
        {
            base.HandleMessage(msg);

            switch (msg)
            {
                case AdminAnnounceEuiMsg.DoAnnounce doAnnounce:
                    if (!_adminManager.HasAdminFlag(Player, AdminFlags.Admin))
                    {
                        Close();
                        break;
                    }

                    // LuaM-start: validate all presentation metadata on the server. The client
                    // sends only an entity reference, never an arbitrary appearance snapshot.
                    _errorMessage = string.Empty;
                    if (!Enum.IsDefined(doAnnounce.ScreenStyle) || !Enum.IsDefined(doAnnounce.AnnounceType) ||
                        !Enum.IsDefined(doAnnounce.PortraitMode) || string.IsNullOrWhiteSpace(doAnnounce.Announcement) ||
                        doAnnounce.Announcement.Length > 8192 || doAnnounce.Announcer == null ||
                        doAnnounce.Announcer.Length > 256 || doAnnounce.PortraitName == null || doAnnounce.PortraitName.Length > 128)
                    {
                        _errorMessage = Loc.GetString("admin-screen-error-message");
                        StateDirty();
                        break;
                    }
                    if (doAnnounce.AnnounceType == AdminAnnounceType.Overwatch && !AdminAnnounceStyles.IsFaction(doAnnounce.ScreenStyle))
                    {
                        _errorMessage = Loc.GetString("admin-screen-error-faction");
                        StateDirty();
                        break;
                    }
                    var recipients = Filter.Broadcast();
                    EntityUid? speaker = null;
                    if (doAnnounce.PortraitMode == AdminAnnouncePortraitMode.Self)
                        speaker = Player.AttachedEntity;
                    else if (doAnnounce.PortraitMode == AdminAnnouncePortraitMode.Entity &&
                        NetEntity.TryParse(doAnnounce.PortraitEntity, out var netEntity) &&
                        _entityManager.TryGetEntity(netEntity, out var entity))
                        speaker = entity;
                    if (doAnnounce.AnnounceType != AdminAnnounceType.Server &&
                        doAnnounce.PortraitMode != AdminAnnouncePortraitMode.None &&
                        (speaker == null || !_entityManager.EntityExists(speaker) ||
                         !_entityManager.HasComponent<HumanoidAppearanceComponent>(speaker)))
                    {
                        _errorMessage = Loc.GetString("admin-screen-error-portrait");
                        StateDirty();
                        break;
                    }
                    var portraitName = string.IsNullOrWhiteSpace(doAnnounce.PortraitName) ? null : doAnnounce.PortraitName.Trim();
                    var title = string.IsNullOrWhiteSpace(doAnnounce.Announcer)
                        ? Loc.GetString("chat-manager-sender-announcement") : doAnnounce.Announcer.Trim();
                    // LuaM-end

                    switch (doAnnounce.AnnounceType)
                    {
                        case AdminAnnounceType.Server:
                            _chatManager.DispatchServerAnnouncement(doAnnounce.Announcement);
                            break;
                        // LuaM: faction chat, HUD, sound and TTS all share the console's audience.
                        case AdminAnnounceType.Overwatch:
                            recipients = _entityManager.System<AdminOverwatchAnnouncementSystem>().Send(doAnnounce.Announcement,
                                doAnnounce.ScreenStyle, doAnnounce.Announcer, speaker, portraitName, doAnnounce.PlaySound);
                            break;
                        // TODO: Per-station announcement support
                        case AdminAnnounceType.Station:
                            _chatSystem.DispatchGlobalAnnouncement(doAnnounce.Announcement, title, playSound: doAnnounce.PlaySound,
                                colorOverride: Color.Gold, style: doAnnounce.ScreenStyle, speaker: speaker, portraitName: portraitName); // LuaM
                            break;
                        case AdminAnnounceType.Antag: // Frontier
                            _chatSystem.DispatchGlobalAnnouncement(doAnnounce.Announcement, title, doAnnounce.PlaySound,
                                new SoundPathSpecifier("/Audio/Announcements/war.ogg"), colorOverride: Color.Red,
                                style: doAnnounce.ScreenStyle, speaker: speaker, portraitName: portraitName, includeDefaultSound: true); // LuaM
                            break;
                    }

// Corvax-TTS-start:
                    if (doAnnounce.EnableTTS && !string.IsNullOrWhiteSpace(doAnnounce.Voice))
                    {
                        _entityManager.EventBus.RaiseEvent(
                            EventSource.Local,
                            new AnnounceSpokeEvent(doAnnounce.Voice, doAnnounce.Announcement, recipients, null));
                    }

// Corvax-TTS-end.
                    StateDirty();

                    if (doAnnounce.CloseAfter)
                        Close();

                    break;
            }
        }
    }
}
