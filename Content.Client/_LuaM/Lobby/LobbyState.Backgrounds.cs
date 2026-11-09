using Content.Shared._LuaM.Lobby;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Client.Lobby;

public sealed partial class LobbyState
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;

    private readonly List<AnimatedLobbyBackgroundPrototype?> _backgrounds = new();
    private int _backgroundIndex;
    private Texture? _staticBackground;

    private void InitializeLuaMLobby()
    {
        Lobby!.PreviousBackgroundButton.OnPressed += OnPreviousBackground;
        Lobby.NextBackgroundButton.OnPressed += OnNextBackground;
        Lobby.PreviousSongButton.OnPressed += OnPreviousSong;
        Lobby.NextSongButton.OnPressed += OnNextSong;
        Lobby.PauseSongButton.OnPressed += OnPauseSong;

        _backgrounds.Clear();
        foreach (var proto in _prototype.EnumeratePrototypes<AnimatedLobbyBackgroundPrototype>())
        {
            _backgrounds.Add(proto);
        }

        _backgrounds.Sort((a, b) => string.CompareOrdinal(a!.ID, b!.ID));
        _backgrounds.Add(null);

        Lobby.BackgroundSwitcher.Visible = _backgrounds.Count > 1;
        _backgroundIndex = _backgrounds.FindIndex(x => x?.Default == true);
        if (_backgroundIndex < 0)
            _backgroundIndex = _random.Next(_backgrounds.Count);
        ApplyBackground();
    }

    private void ShutdownLuaMLobby()
    {
        Lobby!.PreviousBackgroundButton.OnPressed -= OnPreviousBackground;
        Lobby.NextBackgroundButton.OnPressed -= OnNextBackground;
        Lobby.PreviousSongButton.OnPressed -= OnPreviousSong;
        Lobby.NextSongButton.OnPressed -= OnNextSong;
        Lobby.PauseSongButton.OnPressed -= OnPauseSong;
    }

    private void SetStaticLobbyBackground(string? path)
    {
        _staticBackground = path != null && _resourceCache.TryGetResource<TextureResource>(path, out var texture)
            ? texture.Texture
            : null;

        if (_backgrounds.Count == 0 || _backgrounds[_backgroundIndex] == null)
            ApplyBackground();
    }

    private void ApplyBackground()
    {
        if (Lobby == null)
            return;

        if (_backgrounds.Count > 0 && _backgrounds[_backgroundIndex] is { } animated)
            Lobby.Background.SetAnimated(animated);
        else
            Lobby.Background.SetStatic(_staticBackground);
    }

    private void CycleBackground(int delta)
    {
        if (_backgrounds.Count < 2)
            return;

        _backgroundIndex = (_backgroundIndex + delta + _backgrounds.Count) % _backgrounds.Count;
        ApplyBackground();
    }

    private void OnPreviousBackground(BaseButton.ButtonEventArgs args) => CycleBackground(-1);

    private void OnNextBackground(BaseButton.ButtonEventArgs args) => CycleBackground(1);

    private void OnPreviousSong(BaseButton.ButtonEventArgs args) => _contentAudioSystem.SkipLobbyTrack(-1);

    private void OnNextSong(BaseButton.ButtonEventArgs args) => _contentAudioSystem.SkipLobbyTrack(1);

    private void OnPauseSong(BaseButton.ButtonEventArgs args)
    {
        _contentAudioSystem.ToggleLobbyMusicPause();
        UpdatePauseButton();
    }

    private void UpdatePauseButton()
    {
        if (Lobby == null)
            return;

        var paused = _contentAudioSystem.LobbyMusicPaused;
        Lobby.PauseSongIcon.TexturePath = paused
            ? "/Textures/Interface/AdminActions/play.png"
            : "/Textures/Interface/AdminActions/pause.png";
        Lobby.PauseSongButton.ToolTip = Loc.GetString(paused ? "ui-lobby-song-resume" : "ui-lobby-song-pause");
    }

    private LobbyTrackPrototype? GetLobbyTrack(string filename)
    {
        var path = new ResPath(filename);
        foreach (var track in _prototype.EnumeratePrototypes<LobbyTrackPrototype>())
        {
            if (track.Path == path)
                return track;
        }

        return null;
    }
}
