using Content.Client.Viewport;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Scp.Vignette;

public sealed partial class VignetteOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> VignetteShader = "Vignette";

    [Dependency] private IPrototypeManager _prototype = default!;

    private readonly ShaderInstance _shader;

    public float Strength = 1f; // LuaM

    public VignetteOverlay()
    {
        IoCManager.InjectDependencies(this);

        _shader = _prototype.Index<ShaderPrototype>(VignetteShader).Instance().Duplicate();
    }

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public override bool RequestScreenTexture => true;

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture is null)
            return;

        // Mono: Prevent a stupid bug with Z eyes causing multiple vignettes showing causing rendering issues.
        // This took me too long to find for how simple it was.
        if (args.Viewport.Eye is ScalingViewport.ZEye)
            return;

        if (Strength <= 0f) // LuaM
            return;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("vignette_color", Color.Black.WithAlpha(Strength)); // LuaM: WithAlpha(0.9f), шейдер раньше игнорировал альфу

        args.WorldHandle.UseShader(_shader);
        args.WorldHandle.DrawRect(args.WorldBounds, Color.White);
        args.WorldHandle.UseShader(null);
    }
}
