using System.Numerics;
using Content.Client.Parallax;
using Content.Client.Parallax.Managers;
using Content.Client.Viewport;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._LuaM.SectorAnomaly;

public sealed partial class SectorAnomalyBackdropOverlay : Overlay
{
    public const int BackdropZIndex = ParallaxSystem.ParallaxZIndex + 2;

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IParallaxManager _parallax = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    private readonly CESharedZLevelsSystem _zLevel;

    public float Strength;
    public EntityUid? Map;
    public string Parallax = string.Empty;
    public Color Base;
    public Color Flash;
    public Color? Haze;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

    public SectorAnomalyBackdropOverlay()
    {
        IoCManager.InjectDependencies(this);
        _zLevel = _entManager.System<CESharedZLevelsSystem>();
        ZIndex = BackdropZIndex;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (Strength <= 0f || args.MapUid != Map)
            return false;

        if (args.Viewport.Eye is ScalingViewport.ZEye zEye)
            return zEye.DrawParallax;

        return !_entManager.HasComponent<CEZTransitMapComponent>(args.MapUid) && !_zLevel.TryMapDown(args.MapUid, out _);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;

        if (!_cfg.GetCVar(CCVars.ParallaxEnabled))
        {
            var tint = Haze ?? Flash;
            handle.DrawRect(args.WorldAABB, tint.WithAlpha(tint.A * Strength));
            return;
        }

        if (Strength >= 0.5f)
        {
            handle.DrawRect(args.WorldAABB, Base);
            DrawLayers(handle, args.WorldAABB, args.Viewport.Eye?.Position.Position ?? Vector2.Zero);

            if (Haze is { } haze)
                handle.DrawRect(args.WorldAABB, haze);
        }

        // The flash hides the parallax swap at half strength.
        var flash = 1f - MathF.Abs(Strength * 2f - 1f);
        if (flash > 0f)
            handle.DrawRect(args.WorldAABB, Flash.WithAlpha(flash));
    }

    private void DrawLayers(DrawingHandleWorld handle, Box2 worldAABB, Vector2 position)
    {
        var realTime = (float) _timing.RealTime.TotalSeconds;

        foreach (var layer in _parallax.GetParallaxLayers(Parallax))
        {
            handle.UseShader(string.IsNullOrEmpty(layer.Config.Shader)
                ? null
                : _prototype.Index<ShaderPrototype>(layer.Config.Shader).Instance());

            var tex = layer.Texture;
            var size = tex.Size / (float) EyeManager.PixelsPerMeter * layer.Config.Scale;
            var home = layer.Config.WorldHomePosition + _parallax.ParallaxAnchor;
            var originBL = (position - home) * layer.Config.Slowness + layer.Config.Scrolling * realTime + home;
            originBL += layer.Config.WorldAdjustPosition - size / 2;

            if (!layer.Config.Tiled)
            {
                handle.DrawTextureRect(tex, Box2.FromDimensions(originBL, size));
                continue;
            }

            var flooredBL = ((worldAABB.BottomLeft - originBL) / size).Floored() * size + originBL;
            for (var x = flooredBL.X; x < worldAABB.Right; x += size.X)
            {
                for (var y = flooredBL.Y; y < worldAABB.Top; y += size.Y)
                {
                    handle.DrawTextureRect(tex, Box2.FromDimensions(new Vector2(x, y), size));
                }
            }
        }

        handle.UseShader(null);
    }
}
