using Content.Shared._LuaM.Cyberware;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._LuaM.Cyberware;

/// <summary>
/// Glitch screen effect of cyberpsychosis.
/// </summary>
public sealed partial class CyberpsychosisGlitchOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "CyberpsychosisGlitch";

    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IEntityManager _entMan = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    private readonly ShaderInstance _glitchShader;

    public CyberpsychosisGlitchOverlay()
    {
        IoCManager.InjectDependencies(this);
        _glitchShader = _proto.Index(Shader).InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entMan.TryGetComponent(_player.LocalEntity, out EyeComponent? eyeComp))
            return false;

        if (args.Viewport.Eye != eyeComp.Eye)
            return false;

        return _entMan.HasComponent<CyberpsychosisGlitchComponent>(_player.LocalEntity);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null
            || !_entMan.TryGetComponent(_player.LocalEntity, out CyberpsychosisGlitchComponent? glitch))
            return;

        var handle = args.WorldHandle;
        _glitchShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _glitchShader.SetParameter("intensity", glitch.Intensity);
        handle.UseShader(_glitchShader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
