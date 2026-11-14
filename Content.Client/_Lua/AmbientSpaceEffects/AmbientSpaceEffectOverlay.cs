// LuaCorp - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaCorp Contributors
// See AGPLv3.txt for details.

using System.Numerics;
using Content.Shared._Lua.AmbientSpaceEffects;
using Robust.Client.Graphics;
using Robust.Client.Utility;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client._Lua.AmbientSpaceEffects;

public sealed class AmbientSpaceEffectOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> FallbackShader = "AmbientNebula";

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

    private readonly IClyde _clyde;
    private readonly IEntityManager _entManager;
    private readonly IPrototypeManager _prototypes;
    private readonly IGameTiming _timing;
    private readonly SharedTransformSystem _transform;

    // -- Параметры шейдера читаются при сбросе батча, поэтому у каждой туманности свой экземпляр
    private readonly Dictionary<EntityUid, ShaderInstance> _fieldShaders = new();
    private OwnedTexture? _noise;

    public int Quality;

    public bool LowerEnabled = true;
    public bool MidEnabled = true;
    public bool UpperEnabled = true;

    public float DensityMul = 1f;

    public AmbientSpaceEffectOverlay(IEntityManager entManager, IPrototypeManager prototypes)
    {
        _entManager = entManager;
        _prototypes = prototypes;
        _timing = IoCManager.Resolve<IGameTiming>();
        _clyde = IoCManager.Resolve<IClyde>();
        _transform = entManager.System<SharedTransformSystem>();
        ZIndex = 0;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return Quality > 0 && DensityMul > 0f && args.MapId != MapId.Nullspace;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var eyePos = args.Viewport.Eye?.Position.Position ?? args.WorldAABB.Center;
        var time = (float) _timing.CurTime.TotalSeconds;
        var drawn = false;
        var zoom = args.Viewport.Eye?.Zoom.X ?? 1f;
        // -- Мелкие прожилки видны только вблизи: на дальнем зуме они мерцают и только тратят время видеокарты
        var detailMix = Quality > 1 ? Math.Clamp((2.5f - zoom) / 1.5f, 0f, 1f) : 0f;

        var opacityMul = DensityMul * Quality switch
        {
            <= 1 => 0.75f,
            2 => 1f,
            _ => 1.08f,
        };

        var query = _entManager.EntityQueryEnumerator<AmbientSpaceFieldComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var field, out var xform))
        {
            if (xform.MapID != args.MapId || field.Seed == 0)
                continue;

            var radius = MathF.Max(field.Radius, 1f);
            var fieldPos = _transform.GetWorldPosition(xform);
            var bounds = Box2.CenteredAround(fieldPos, new Vector2(radius * 2f, radius * 2f));
            if (!bounds.Intersects(args.WorldAABB))
                continue;

            if (!_prototypes.TryIndex(field.Effect, out var effect))
                continue;

            var alphas = new Vector3(
                LowerEnabled ? effect.LowerOpacity : 0f,
                MidEnabled ? effect.MidOpacity : 0f,
                UpperEnabled && Quality > 1 ? effect.UpperOpacity : 0f) * opacityMul;
            if (alphas == Vector3.Zero)
                continue;

            var shaderSeed = AmbientSpacePalette.ShaderSeedFromField(field.Seed);
            var shader = GetFieldShader(uid, effect.Shader);
            shader.SetParameter("nebula_color", AmbientSpacePalette.ColorFromSeed(field.Seed));
            shader.SetParameter("seed", shaderSeed);
            shader.SetParameter("density", field.Density);
            shader.SetParameter("field_radius", radius);
            shader.SetParameter("layer_alpha", alphas);
            shader.SetParameter("layer_slide", new Vector3(1f - effect.LowerParallax, 0f, 1f - effect.UpperParallax));
            shader.SetParameter("eye_offset", (eyePos - fieldPos) / radius);
            shader.SetParameter("max_alpha", effect.MaxOpacity);
            shader.SetParameter("time", time + shaderSeed * 0.37f);
            shader.SetParameter("time_speed", effect.FlowSpeed);
            shader.SetParameter("detail_mix", detailMix);

            handle.UseShader(shader);
            handle.SetTransform(Matrix3x2.Identity);
            handle.DrawTextureRect(Texture.White, bounds);
            drawn = true;
        }

        if (drawn)
            handle.UseShader(null);
    }

    public void ForgetField(EntityUid uid)
    {
        if (_fieldShaders.Remove(uid, out var shader))
            shader.Dispose();
    }

    private ShaderInstance GetFieldShader(EntityUid uid, ProtoId<ShaderPrototype> id)
    {
        if (_fieldShaders.TryGetValue(uid, out var existing))
            return existing;

        if (!_prototypes.TryIndex(id, out var shaderProto))
            shaderProto = _prototypes.Index(FallbackShader);

        var instance = shaderProto.InstanceUnique();
        instance.SetParameter("noise_tex", GetNoiseTexture());
        _fieldShaders[uid] = instance;
        return instance;
    }

    private Texture GetNoiseTexture()
    {
        if (_noise != null)
            return _noise;

        const int size = AmbientSpaceNebulaNoise.BakeSize;
        var red = new byte[size * size];
        var green = new byte[size * size];
        AmbientSpaceNebulaNoise.Bake(0, red);
        AmbientSpaceNebulaNoise.Bake(1, green);

        using var image = new Image<Rgba32>(Configuration.Default, size, size, new Rgba32(0, 0, 0, 255));
        var pixels = image.GetPixelSpan();
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Rgba32(red[i], green[i], 0, 255);
        }

        var parameters = new TextureLoadParameters
        {
            SampleParameters = new TextureSampleParameters { Filter = true, WrapMode = TextureWrapMode.Repeat },
            Srgb = false,
        };

        _noise = _clyde.LoadTextureFromImage(image, "ambient-nebula-noise", parameters);
        return _noise;
    }

    protected override void DisposeBehavior()
    {
        foreach (var shader in _fieldShaders.Values)
        {
            shader.Dispose();
        }

        _fieldShaders.Clear();
        _noise?.Dispose();
        _noise = null;
        base.DisposeBehavior();
    }
}
