using Content.Shared._LuaM.Body;
using Robust.Client.Graphics;
using Robust.Client.Player;

namespace Content.Client._LuaM.Body;

public sealed class BloodlossOverlaySystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IPlayerManager _player = default!;

    private const float LerpRate = 2f;

    private BloodlossOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new BloodlossOverlay();
        _overlayManager.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlayManager.RemoveOverlay(_overlay);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var target = 0f;
        if (TryComp<BloodlossVisualsComponent>(_player.LocalEntity, out var visuals))
        {
            target = visuals.Intensity;
            _overlay.BloodColor = visuals.BloodColor;
        }

        var current = _overlay.CurrentIntensity;
        current += (target - current) * Math.Clamp(LerpRate * frameTime, 0f, 1f);
        _overlay.CurrentIntensity = current < 0.005f ? 0f : current;
    }
}
