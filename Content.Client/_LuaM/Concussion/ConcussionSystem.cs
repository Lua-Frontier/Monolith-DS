using Content.Shared._LuaM.Concussion;
using Content.Shared._LuaM.Hearing;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._LuaM.Concussion;

/// <summary>
/// Blurs the screen and rings in the ears of the local player while they are concussed, both fading out as it wears off.
/// </summary>
public sealed partial class ConcussionSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    // -- Звон, начавшийся раньше этого, уже не запускаем: игрок зашёл в тело посреди контузии
    private static readonly TimeSpan SoundStartWindow = TimeSpan.FromSeconds(1);

    private const float MinSoundGain = 0.01f;

    private ConcussionOverlay _overlay = default!;

    private EntityUid? _stream;
    private float _streamVolume;
    private (EntityUid Owner, TimeSpan Start)? _playedSound;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new ConcussionOverlay();
        _overlayManager.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlayManager.RemoveOverlay(_overlay);
        StopSound();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.CurTime;
        if (_player.LocalEntity is not { } local || !TryComp<ConcussedComponent>(local, out var concussed))
        {
            _overlay.Intensity = 0f;
            StopSound();
            return;
        }

        var intensity = concussed.GetIntensity(now);
        _overlay.Intensity = concussed.Blur ? intensity : 0f;

        if (concussed.Sound != null
            && _playedSound != (local, concussed.SoundStart)
            && now - concussed.SoundStart < SoundStartWindow)
        {
            StopSound();
            _playedSound = (local, concussed.SoundStart);
            _streamVolume = concussed.Sound.Params.Volume;
            _stream = _audio.PlayGlobal(concussed.Sound, Filter.Local(), false)?.Entity;
        }

        if (TerminatingOrDeleted(_stream))
        {
            _stream = null;
            return;
        }

        // -- Громкость идёт за силой контузии: сначала по силе разрыва, к концу сходит на нет
        var fade = concussed.Strength > 0f ? intensity / concussed.Strength : 0f;
        var gain = 0.3f * fade + 0.7f * intensity;
        if (gain < MinSoundGain)
        {
            StopSound();
            return;
        }

        // -- Глухота приглушает весь звук клиента, звон поднимаем обратно: в ушах звенит громче всего вокруг
        if (TryComp<DeafComponent>(local, out var deaf) && deaf.SoundVolume > 0f)
            gain /= deaf.SoundVolume;

        _audio.SetVolume(_stream, _streamVolume + SharedAudioSystem.GainToVolume(gain));
    }

    private void StopSound()
    {
        _stream = _audio.Stop(_stream);
    }
}
