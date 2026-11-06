using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._LuaM.Body;

public sealed class BloodlossOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "LuaMBloodloss";

    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    private readonly ShaderInstance _shader;

    public float CurrentIntensity;
    public Color BloodColor = Color.FromHex("#800000");

    public BloodlossOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototypeManager.Index(Shader).InstanceUnique();
        ZIndex = 5;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (CurrentIntensity <= 0f)
            return false;

        if (!_entityManager.TryGetComponent(_playerManager.LocalEntity, out EyeComponent? eye))
            return false;

        return args.Viewport.Eye == eye.Eye;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var handle = args.WorldHandle;
        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("bloodlossIntensity", CurrentIntensity);
        _shader.SetParameter("bloodColor", new Vector3(BloodColor.R, BloodColor.G, BloodColor.B));
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
