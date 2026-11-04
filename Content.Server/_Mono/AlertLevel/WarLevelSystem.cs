using Content.Server._NF.SectorServices;
using Content.Server.Chat.Systems;
using Robust.Shared.Audio;

namespace Content.Server._Mono.AlertLevel;

public sealed partial class WarLevelSystem : EntitySystem
{
    [Dependency] private SectorServiceSystem _sectorService = default!;
    [Dependency] private ChatSystem _chatSystem = default!;

    public bool GetWarLevel(EntityUid station, WarLevelComponent? alert = null)
    {
        if (!TryComp(_sectorService.GetServiceEntity(), out alert))
            return false;

        return alert.PostWar;
    }

    public void SetLevel(bool level, WarLevelComponent? component = null, bool playSound = true) // LuaM: playSound
    {
        EntityUid sectorEnt = _sectorService.GetServiceEntity();
        if (!TryComp<WarLevelComponent>(sectorEnt, out component))
        {
            Log.Error($"Unable to find WarLevelComponent for entity {sectorEnt}");
            return;
        }

        component.PostWar = level;
        component.Pending = false; // LuaM
        component.White = false; // LuaM
        Log.Info($"Setting WarLevelComponent for entity {sectorEnt} to {component.PostWar}. Input value {level}");
            _chatSystem.DispatchGlobalAnnouncement(
                level ? Loc.GetString("war-level-announcement-post") :  Loc.GetString("war-level-announcement-pre"),
                sender: Loc.GetString("war-level-announcement-sender"),
                playSound: level && playSound, // LuaM
                announcementSound: level ? new SoundPathSpecifier("/Audio/Misc/gamma.ogg") : new SoundPathSpecifier("/Audio/Announcements/notice2.ogg"),
                colorOverride: level ? Color.Crimson : Color.CornflowerBlue);

        RaiseLocalEvent(new WarLevelChangedEvent(level)); // Frontier: pass invalid, we have no station
    }

    // LuaM start: yellow war level while waiting for the other side to declare war
    public void SetPending(bool pending)
    {
        var sectorEnt = _sectorService.GetServiceEntity();
        if (!TryComp<WarLevelComponent>(sectorEnt, out var component) || component.Pending == pending)
            return;

        component.Pending = pending;
        RaiseLocalEvent(new WarLevelChangedEvent(component.PostWar));
    }

    /// <summary>
    /// White war level: the war ended with a faction's capitulation.
    /// </summary>
    public void SetWhite()
    {
        var sectorEnt = _sectorService.GetServiceEntity();
        if (!TryComp<WarLevelComponent>(sectorEnt, out var component))
            return;

        component.PostWar = false;
        component.Pending = false;
        component.White = true;
        RaiseLocalEvent(new WarLevelChangedEvent(false));
    }
    // LuaM end
}

public sealed class WarLevelChangedEvent : EntityEventArgs
{
    public bool WarLevel { get; }

    public WarLevelChangedEvent(bool alertLevel)
    {
        WarLevel = alertLevel;
    }
}
