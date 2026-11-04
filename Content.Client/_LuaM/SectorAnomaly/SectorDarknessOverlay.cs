using Content.Client.Viewport;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._LuaM.SectorAnomaly;

public sealed partial class SectorDarknessOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "SectorDarkness";

    private readonly ShaderInstance _shader;

    public float Strength;
    public EntityUid? Map;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    public SectorDarknessOverlay(IPrototypeManager prototype)
    {
        _shader = prototype.Index(Shader).InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        // -- ZEye рисует каждый z-уровень отдельно, без этой проверки затемнение накладывается несколько раз
        return Strength > 0f && args.MapUid == Map && args.Viewport.Eye is not ScalingViewport.ZEye;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("strength", Strength);

        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
