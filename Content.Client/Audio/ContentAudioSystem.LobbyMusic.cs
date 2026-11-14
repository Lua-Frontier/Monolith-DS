using System.Linq;
using Content.Client.GameTicking.Managers;
using Content.Client.Lobby;
using Content.Shared.Audio.Events;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Client;
using Robust.Client.ResourceManagement;
using Robust.Client.State;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components; // LuaM
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Audio;

// Part of ContentAudioSystem that is responsible for lobby music playing/stopping and round-end sound-effect.
public sealed partial class ContentAudioSystem
{
    [Dependency] private IBaseClient _client = default!;
    [Dependency] private ClientGameTicker _gameTicker = default!;
    [Dependency] private IResourceCache _resourceCache = default!;

    private readonly AudioParams _lobbySoundtrackParams = new(-5f, 1, 0, 0, 0, false, 0f);
    private readonly AudioParams _roundEndSoundEffectParams = new(-5f, 1, 0, 0, 0, false, 0f);

    /// <summary>
    /// EntityUid of lobby restart sound component.
    /// </summary>
    private EntityUid? _lobbyRoundRestartAudioStream;

    /// <summary>
    /// Shuffled list of soundtrack file-names.
    /// </summary>
    private string[]? _lobbyPlaylist;

    /// <summary>
    /// Short info about lobby soundtrack currently playing. Is null if soundtrack is not playing.
    /// </summary>
    private LobbySoundtrackInfo? _lobbySoundtrackInfo;

    private TimeSpan? _lobbyPausedAt; // LuaM

    private Action<LobbySoundtrackChangedEvent>? _lobbySoundtrackChanged;

    /// <summary>
    /// Event for subscription on lobby soundtrack changes.
    /// </summary>
    public event Action<LobbySoundtrackChangedEvent>? LobbySoundtrackChanged
    {
        add
        {
            if (value != null)
            {
                if (_lobbySoundtrackInfo != null)
                {
                    value(new LobbySoundtrackChangedEvent(_lobbySoundtrackInfo.Filename));
                }

                _lobbySoundtrackChanged += value;
            }
        }
        remove => _lobbySoundtrackChanged -= value;
    }

    /// <summary>
    /// Initializes subscriptions that are related to lobby music.
    /// </summary>
    private void InitializeLobbyMusic()
    {
        Subs.CVar(_configManager, CCVars.LobbyMusicEnabled, LobbyMusicCVarChanged);
        Subs.CVar(_configManager, CCVars.LobbyMusicVolume, LobbyMusicVolumeCVarChanged);

        _state.OnStateChanged += StateManagerOnStateChanged;

        _client.PlayerLeaveServer += OnLeave;

        SubscribeNetworkEvent<LobbyMusicStopEvent>(OnLobbySongStopped);
        SubscribeNetworkEvent<LobbyPlaylistChangedEvent>(OnLobbySongChanged);
    }

    private void OnLobbySongStopped(LobbyMusicStopEvent ev)
    {
        EndLobbyMusic();
    }

    private void StateManagerOnStateChanged(StateChangedEventArgs args)
    {
        switch (args.NewState)
        {
            case LobbyState:
                StartLobbyMusic();
                break;
            default:
                EndLobbyMusic();
                break;
        }
    }

    private void OnLeave(object? sender, PlayerEventArgs args)
    {
        EndLobbyMusic();
    }

    private void LobbyMusicVolumeCVarChanged(float volume)
    {
        if (_lobbySoundtrackInfo != null)
        {
            _audio.SetVolume(
                _lobbySoundtrackInfo.MusicStreamEntityUid,
                _lobbySoundtrackParams.Volume + SharedAudioSystem.GainToVolume(_configManager.GetCVar(CCVars.LobbyMusicVolume))
            );
        }
    }

    private void LobbyMusicCVarChanged(bool musicEnabled)
    {
        if (musicEnabled && _state.CurrentState is LobbyState)
        {
            StartLobbyMusic();
        }
        else
        {
            EndLobbyMusic();
        }
    }

    private void OnLobbySongChanged(LobbyPlaylistChangedEvent playlistChangedEvent)
    {
        var playlist = playlistChangedEvent.Playlist;
        //playlist is already playing, no need to restart it
        if (_lobbySoundtrackInfo != null
            && _lobbyPlaylist != null
            && _lobbyPlaylist.SequenceEqual(playlist)
           )
        {
            return;
        }

        EndLobbyMusic();
        StartLobbyMusic(playlistChangedEvent.Playlist);
    }

    /// <summary>
    /// Re-starts playing lobby music from playlist, last sent from server. if there is currently none - does nothing.
    /// </summary>
    private void StartLobbyMusic()
    {
        if (_lobbyPlaylist == null || _lobbyPlaylist.Length == 0)
        {
            return;
        }

        StartLobbyMusic(_lobbyPlaylist);
    }

    /// <summary>
    /// Starts playing lobby music from playlist. If playlist is empty, or lobby music setting is turned off - does nothing.
    /// </summary>
    /// <param name="playlist">Array of soundtrack filenames for lobby playlist.</param>
    private void StartLobbyMusic(string[] playlist)
    {
        _lobbyPlaylist = playlist; // LuaM
        if (_lobbySoundtrackInfo != null || !_configManager.GetCVar(CCVars.LobbyMusicEnabled))
            return;

        if (_lobbyPlaylist.Length == 0)
        {
            return;
        }

        PlaySoundtrack(playlist[0]);
    }

    private void PlaySoundtrack(string soundtrackFilename)
    {
        if (!_resourceCache.TryGetResource(new ResPath(soundtrackFilename), out AudioResource? audio))
        {
            Log.Warning($"Failed to load lobby soundtrack {soundtrackFilename}"); // LuaM
            return;
        }

        var playResult = _audio.PlayGlobal(
            new ResolvedPathSpecifier(soundtrackFilename),
            Filter.Local(),
            false,
            _lobbySoundtrackParams.WithVolume(_lobbySoundtrackParams.Volume + SharedAudioSystem.GainToVolume(_configManager.GetCVar(CCVars.LobbyMusicVolume)))
        );
        if (playResult == null)
        {
            Log.Warning(
                $"Tried to play lobby soundtrack '{{Filename}}' using {nameof(SharedAudioSystem)}.{nameof(SharedAudioSystem.PlayGlobal)} but it returned default value of EntityUid!",
                soundtrackFilename);
            return;
        }

        _lobbyPausedAt = null; // LuaM
        var nextTrackOn = _timing.CurTime + audio.AudioStream.Length;
        _lobbySoundtrackInfo = new LobbySoundtrackInfo(soundtrackFilename, nextTrackOn, playResult.Value.Entity);

        var lobbySongChangedEvent = new LobbySoundtrackChangedEvent(soundtrackFilename);
        _lobbySoundtrackChanged?.Invoke(lobbySongChangedEvent);
    }

    private void EndLobbyMusic()
    {
        if (_lobbySoundtrackInfo == null)
        {
            return;
        }

        if (Exists(_lobbySoundtrackInfo.MusicStreamEntityUid)) // LuaM: _audio.Stop > Del
            Del(_lobbySoundtrackInfo.MusicStreamEntityUid);
        _lobbySoundtrackInfo = null;
        _lobbyPausedAt = null; // LuaM
        var lobbySongChangedEvent = new LobbySoundtrackChangedEvent();
        _lobbySoundtrackChanged?.Invoke(lobbySongChangedEvent);
    }

    private void PlayRestartSound(RoundRestartCleanupEvent ev)
    {
        if (!_configManager.GetCVar(CCVars.RestartSoundsEnabled))
            return;

        var file = _gameTicker.RestartSound;
        if (ResolvedSoundSpecifier.IsNullOrEmpty(file))
        {
            return;
        }

        _lobbyRoundRestartAudioStream = _audio.PlayGlobal(
            file,
            Filter.Local(),
            false,
            _roundEndSoundEffectParams.WithVolume(_roundEndSoundEffectParams.Volume + SharedAudioSystem.GainToVolume(_configManager.GetCVar(CCVars.LobbyMusicVolume)))
        )?.Entity;
    }

    private void ShutdownLobbyMusic()
    {
        _state.OnStateChanged -= StateManagerOnStateChanged;

        _client.PlayerLeaveServer -= OnLeave;

        EndLobbyMusic();
    }

    private void UpdateLobbyMusic()
    {
        if (
            _lobbySoundtrackInfo != null
            && _lobbyPausedAt == null // LuaM
            && (_timing.CurTime >= _lobbySoundtrackInfo.NextTrackOn || !Exists(_lobbySoundtrackInfo.MusicStreamEntityUid)) // LuaM
            && _lobbyPlaylist?.Length > 0
            )
        {
            SkipLobbyTrack(1); // LuaM
        }
    }

    private static string GetNextSoundtrackFromPlaylist(string currentSoundtrackFilename, string[] playlist)
    {
        var indexOfCurrent = Array.IndexOf(playlist, currentSoundtrackFilename);
        var nextTrackIndex = indexOfCurrent + 1;
        if (nextTrackIndex > playlist.Length - 1)
        {
            nextTrackIndex = 0;
        }

        return playlist[nextTrackIndex];
    }

    /// <summary> Container for lobby soundtrack information. </summary>
    /// <param name="Filename">Soundtrack filename.</param>
    /// <param name="NextTrackOn">Time (based on <see cref="IGameTiming.CurTime"/>) when this track is going to finish playing and next track have to be started.</param>
    /// <param name="MusicStreamEntityUid">
    /// EntityUid of launched soundtrack (from <see cref="SharedAudioSystem.PlayGlobal(string,Robust.Shared.Player.Filter,bool,System.Nullable{Robust.Shared.Audio.AudioParams})"/>).
    /// </param>
    // LuaM-start
    public void SkipLobbyTrack(int delta)
    {
        if (_lobbyPlaylist is not { Length: > 0 } playlist || !_configManager.GetCVar(CCVars.LobbyMusicEnabled))
            return;

        var index = _lobbySoundtrackInfo == null ? -1 : Array.IndexOf(playlist, _lobbySoundtrackInfo.Filename);
        if (index < 0 && delta < 0)
            index = 0;

        if (_lobbySoundtrackInfo != null && Exists(_lobbySoundtrackInfo.MusicStreamEntityUid))
            Del(_lobbySoundtrackInfo.MusicStreamEntityUid);
        _lobbySoundtrackInfo = null;

        for (var i = 0; i < playlist.Length && _lobbySoundtrackInfo == null; i++)
        {
            index = ((index + delta) % playlist.Length + playlist.Length) % playlist.Length;
            PlaySoundtrack(playlist[index]);
        }
    }

    public bool LobbyMusicPaused => _lobbyPausedAt != null;

    public void ToggleLobbyMusicPause()
    {
        if (_lobbySoundtrackInfo is not { } info || !Exists(info.MusicStreamEntityUid))
            return;

        if (_lobbyPausedAt is { } pausedAt)
        {
            _audio.SetState(info.MusicStreamEntityUid, AudioState.Playing);
            _lobbySoundtrackInfo = info with { NextTrackOn = info.NextTrackOn + (_timing.CurTime - pausedAt) };
            _lobbyPausedAt = null;
            return;
        }

        _audio.SetState(info.MusicStreamEntityUid, AudioState.Paused);
        _lobbyPausedAt = _timing.CurTime;
    }
    // LuaM-end

    private sealed record LobbySoundtrackInfo(string Filename, TimeSpan NextTrackOn, EntityUid MusicStreamEntityUid);
}

/// <summary>
/// Event of changing lobby soundtrack (or stopping lobby music - will pass null for <paramref name="SoundtrackFilename"/> in that case).
/// Is used by <see cref="ContentAudioSystem.LobbySoundtrackChanged"/> and <see cref="LobbyState.UpdateLobbySoundtrackInfo"/>.
/// </summary>
/// <param name="SoundtrackFilename">Filename of newly set soundtrack, or null if soundtrack playback is stopped.</param>
public sealed record LobbySoundtrackChangedEvent(string? SoundtrackFilename = null);
