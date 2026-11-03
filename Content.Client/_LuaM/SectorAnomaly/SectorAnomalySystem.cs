using Content.Client._FarHorizons.StarSystem;
using Content.Client.Audio;
using Content.Client.Parallax;
using Content.Client.Parallax.Managers;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._LuaM.SectorAnomaly;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._LuaM.SectorAnomaly;

public sealed partial class SectorAnomalySystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IParallaxManager _parallax = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private ContentAudioSystem _contentAudio = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private const float FadeTime = 8f;
    private const float MusicFadeVolume = 32f;
    private const float DarkStarLight = 0.15f;

    private const string DarknessParallax = "SectorDarkness";
    private const string BrightnessParallax = "SectorBrightness";
    private static readonly Color DarknessBase = Color.FromHex("#060606");
    private static readonly Color BrightnessBase = Color.FromHex("#B8D4EC");
    private static readonly Color BrightnessHaze = Color.FromHex("#C8DCF0").WithAlpha(0.45f);

    private SectorDarknessOverlay _darkness = default!;
    private SectorAnomalyBackdropOverlay _backdrop = default!;
    private int _sources;

    private Anomaly _anomaly;
    private EntityUid? _map;
    private float _strength;

    private EntityUid? _music;
    private float _musicVolume;
    private float _sliderVolume;

    private Color _ambientColor;
    private Color _baseAmbient;
    private float _baseStarIntensity;
    private Color _baseStarFloor;

    public override void Initialize()
    {
        base.Initialize();

        _darkness = new SectorDarknessOverlay(_prototype);
        _backdrop = new SectorAnomalyBackdropOverlay();

        SubscribeLocalEvent<SectorDarknessComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SectorDarknessComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SectorBrightnessComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SectorBrightnessComponent, ComponentShutdown>(OnShutdown);
        Subs.CVar(_cfg, CCVars.AmbientMusicVolume, OnMusicVolumeChanged, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        Switch(Anomaly.None, null);
    }

    private void OnStartup(EntityUid uid, IComponent component, ComponentStartup args)
    {
        _sources++;
    }

    private void OnShutdown(EntityUid uid, IComponent component, ComponentShutdown args)
    {
        _sources--;
    }

    private void OnMusicVolumeChanged(float value)
    {
        _sliderVolume = SharedAudioSystem.GainToVolume(value);
        if (_music != null)
            _audio.SetVolume(_music, GetMusicVolume());
    }

    public override void FrameUpdate(float frameTime)
    {
        if (_sources == 0 && _anomaly == Anomaly.None)
            return;

        var map = CompOrNull<TransformComponent>(_player.LocalEntity)?.MapUid;
        var target = GetAnomaly(map);

        if (target != _anomaly && (_anomaly == Anomaly.None || _strength <= 0f || map != _map))
            Switch(target, map);

        var goal = _anomaly != Anomaly.None && _anomaly == target ? 1f : 0f;
        if (_strength == goal)
            return;

        _strength = goal > _strength
            ? MathF.Min(1f, _strength + frameTime / FadeTime)
            : MathF.Max(0f, _strength - frameTime / FadeTime);

        Apply();
    }

    private Anomaly GetAnomaly(EntityUid? map)
    {
        if (HasComp<SectorDarknessComponent>(map))
            return Anomaly.Darkness;

        return HasComp<SectorBrightnessComponent>(map) ? Anomaly.Brightness : Anomaly.None;
    }

    private void Switch(Anomaly anomaly, EntityUid? map)
    {
        if (_anomaly != Anomaly.None)
        {
            _strength = 0f;
            Apply();

            _music = _audio.Stop(_music);
            _overlays.RemoveOverlay(_darkness);
            _overlays.RemoveOverlay(_backdrop);
            SetStarSystemZIndex(false, false);
            _contentAudio.SetMusicSuppressed(nameof(SectorAnomalySystem), false);
        }

        _anomaly = anomaly;
        _map = map;
        _darkness.Map = map;
        _backdrop.Map = map;

        switch (anomaly)
        {
            case Anomaly.Darkness:
                var star = CompOrNull<StarLightComponent>(map);
                _baseStarIntensity = star?.Intensity ?? 0f;
                _baseStarFloor = star?.AmbientFloor ?? Color.Black;
                SetBackdrop(DarknessParallax, DarknessBase, Color.Black, null);
                _overlays.AddOverlay(_darkness);
                StartMusic(Comp<SectorDarknessComponent>(map!.Value).Music);
                break;
            case Anomaly.Brightness:
                var brightness = Comp<SectorBrightnessComponent>(map!.Value);
                _ambientColor = brightness.AmbientColor;
                _baseAmbient = CompOrNull<MapLightComponent>(map)?.AmbientLightColor ?? MapLightComponent.DefaultColor;
                SetBackdrop(BrightnessParallax, BrightnessBase, Color.White, BrightnessHaze);
                StartMusic(brightness.Music);
                break;
            default:
                return;
        }

        _overlays.AddOverlay(_backdrop);
        // -- в темноте звезда остаётся под фоном и пропадает, планеты и пояса рисуются поверх
        SetStarSystemZIndex(true, anomaly == Anomaly.Brightness);
        _contentAudio.SetMusicSuppressed(nameof(SectorAnomalySystem), true);
    }

    private void SetBackdrop(string parallax, Color baseColor, Color flash, Color? haze)
    {
        _backdrop.Parallax = parallax;
        _backdrop.Base = baseColor;
        _backdrop.Flash = flash;
        _backdrop.Haze = haze;

        if (!_parallax.IsLoaded(parallax))
            _ = _parallax.LoadParallaxByName(parallax);
    }

    private void StartMusic(SoundSpecifier? music)
    {
        if (music == null)
            return;

        _musicVolume = music.Params.Volume;
        _music = _audio.PlayGlobal(music, Filter.Local(), false, music.Params.WithLoop(true).WithVolume(GetMusicVolume()))?.Entity;
    }

    private float GetMusicVolume()
    {
        return _musicVolume + _sliderVolume - MusicFadeVolume * (1f - _strength);
    }

    private void Apply()
    {
        _darkness.Strength = _strength;
        _backdrop.Strength = _strength;

        if (_music != null)
            _audio.SetVolume(_music, GetMusicVolume());

        switch (_anomaly)
        {
            case Anomaly.Darkness when TryComp(_map, out StarLightComponent? star):
                star.Intensity = MathHelper.Lerp(_baseStarIntensity, _baseStarIntensity * DarkStarLight, _strength);
                star.AmbientFloor = Color.InterpolateBetween(_baseStarFloor, Color.Black, _strength);
                break;
            case Anomaly.Brightness when TryComp(_map, out MapLightComponent? light):
                light.AmbientLightColor = Color.InterpolateBetween(_baseAmbient, _ambientColor, _strength);
                break;
        }
    }

    private void SetStarSystemZIndex(bool raised, bool star)
    {
        var raisedZIndex = SectorAnomalyBackdropOverlay.BackdropZIndex + 1;
        var normalZIndex = ParallaxSystem.ParallaxZIndex + 1;

        if (_overlays.TryGetOverlay<PlanetOverlay>(out var planets))
            planets.ZIndex = raised ? raisedZIndex : normalZIndex;

        if (_overlays.TryGetOverlay<AsteroidBeltOverlay>(out var belts))
            belts.ZIndex = raised ? raisedZIndex : normalZIndex;

        if (_overlays.TryGetOverlay<StarOverlay>(out var stars))
            stars.ZIndex = raised && star ? raisedZIndex : normalZIndex;
    }

    private enum Anomaly : byte
    {
        None,
        Darkness,
        Brightness,
    }
}
