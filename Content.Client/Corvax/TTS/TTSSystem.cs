using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Corvax.TTS;
using Robust.Client.Audio;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Client.Corvax.TTS;

/// <summary>
/// Plays TTS audio in world.
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class TTSSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IResourceManager _res = default!;
    [Dependency] private readonly AudioSystem _audio = default!;

    private ISawmill _sawmill = default!;

    // Static so the root survives system shutdown/reinit, e.g. round restart.
    private static readonly MemoryContentRoot ContentRoot = new();
    private static bool _rootRegistered;
    private static readonly ResPath Prefix = ResPath.Root / "TTS";

    private const float WhisperFade = 3f;
    private const float MinimalVolume = -6f;
    private const float VoiceRange = 10f;
    private const float WhisperMuffledRange = 5f;

    private float _volume;
    private float _volumeRadio;
    private bool _playRadio = true;
    private int _fileIdx;

    public override void Initialize()
    {
        _sawmill = Logger.GetSawmill("tts");
        if (!_rootRegistered)
        {
            _res.AddRoot(Prefix, ContentRoot);
            _rootRegistered = true;
        }

        _cfg.OnValueChanged(CCVars.TTSVolume, OnTtsVolumeChanged, true);
        _cfg.OnValueChanged(CCVars.TTSVolumeRadio, OnTtsRadioVolumeChanged, true);
        _cfg.OnValueChanged(CCVars.RadioTTSSoundsEnabled, OnTtsPlayRadioChanged, true);
        SubscribeNetworkEvent<PlayTTSEvent>(OnPlayTTS);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _cfg.UnsubValueChanged(CCVars.TTSVolume, OnTtsVolumeChanged);
        _cfg.UnsubValueChanged(CCVars.TTSVolumeRadio, OnTtsRadioVolumeChanged);
        _cfg.UnsubValueChanged(CCVars.RadioTTSSoundsEnabled, OnTtsPlayRadioChanged);
    }

    public void RequestPreviewTTS(string voiceId)
    {
        RaiseNetworkEvent(new RequestPreviewTTSEvent(voiceId));
    }

    private void OnTtsVolumeChanged(float volume)
    {
        _volume = volume;
    }

    private void OnTtsRadioVolumeChanged(float volume)
    {
        _volumeRadio = volume;
    }

    private void OnTtsPlayRadioChanged(bool radio)
    {
        _playRadio = radio;
    }

    private void OnPlayTTS(PlayTTSEvent ev)
    {
        if (ev.IsRadio && !_playRadio)
            return;

        if (ev.Data is not { Length: > 0 })
        {
            _sawmill.Warning("TTS event has no audio data");
            return;
        }

        _sawmill.Verbose($"Play TTS audio {ev.Data.Length} bytes from {ev.SourceUid} entity");

        var filePath = new ResPath($"{_fileIdx++}.ogg");
        ContentRoot.AddOrUpdateFile(filePath, ev.Data);

        var audioResource = new AudioResource();
        audioResource.Load(IoCManager.Instance!, Prefix / filePath);
        var soundSpecifier = new ResolvedPathSpecifier(Prefix / filePath);

        var audioParams = AudioParams.Default
            .WithVolume(AdjustVolume(ev.IsWhisper, ev.IsRadio))
            .WithMaxDistance(AdjustDistance(ev.IsWhisper));

        if (ev.SourceUid != null)
        {
            if (TryGetEntity(ev.SourceUid.Value, out var sourceUid))
                _audio.PlayEntity(audioResource.AudioStream, sourceUid.Value, soundSpecifier, audioParams);
        }
        else
        {
            _audio.PlayGlobal(audioResource.AudioStream, soundSpecifier, audioParams);
        }

        ContentRoot.RemoveFile(filePath);
    }

    private float AdjustVolume(bool isWhisper, bool isRadio)
    {
        var volume = MinimalVolume + SharedAudioSystem.GainToVolume(_volume);

        if (isWhisper && !isRadio)
            volume -= SharedAudioSystem.GainToVolume(WhisperFade);
        else if (isRadio)
            volume = MinimalVolume + SharedAudioSystem.GainToVolume(_volumeRadio);

        return volume;
    }

    private float AdjustDistance(bool isWhisper)
    {
        return isWhisper ? WhisperMuffledRange : VoiceRange;
    }
}
