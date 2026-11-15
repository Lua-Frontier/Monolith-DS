using Content.Shared._LuaM.Hearing;
using Robust.Client.Audio;
using Robust.Client.Player;
using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Client._LuaM.Hearing;

/// <summary>
/// Muffles or mutes all sound while the player's own body is deaf.
/// </summary>
public sealed partial class DeafSystem : EntitySystem
{
    [Dependency] private IAudioManager _audio = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IPlayerManager _player = default!;

    private bool _deaf;

    public override void Shutdown()
    {
        base.Shutdown();

        if (_deaf)
            Restore();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var deaf = TryComp<DeafComponent>(_player.LocalEntity, out var comp);

        // The engine sets the gain back when the window regains focus or the volume setting changes,
        // so it is held down every frame instead of only once.
        if (deaf)
            _audio.SetMasterGain(_cfg.GetCVar(CVars.AudioMasterVolume) * Math.Clamp(comp!.SoundVolume, 0f, 1f));
        else if (_deaf)
            Restore();

        _deaf = deaf;
    }

    private void Restore()
    {
        _deaf = false;
        _audio.SetMasterGain(_cfg.GetCVar(CVars.AudioMasterVolume));
    }
}
