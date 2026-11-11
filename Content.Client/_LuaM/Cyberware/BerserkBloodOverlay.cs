using Content.Shared._LuaM.Cyberware;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._LuaM.Cyberware;

/// <summary>
/// Screen effect of a running berserk heart: the view goes red and blood creeps in from the edges.
/// </summary>
public sealed partial class BerserkBloodOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "BerserkBlood";

    private const float ActiveIntensity = 0.6f;
    private const float FrenzyIntensity = 1f;

    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IEntityManager _entMan = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    private readonly ShaderInstance _shader;

    public BerserkBloodOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _proto.Index(Shader).InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entMan.TryGetComponent(_player.LocalEntity, out EyeComponent? eyeComp))
            return false;

        if (args.Viewport.Eye != eyeComp.Eye)
            return false;

        return _entMan.TryGetComponent(_player.LocalEntity, out BerserkHeartComponent? heart) && heart.Active;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null
            || !_entMan.TryGetComponent(_player.LocalEntity, out BerserkHeartComponent? heart))
            return;

        var handle = args.WorldHandle;
        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("intensity", heart.Frenzy ? FrenzyIntensity : ActiveIntensity);
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
