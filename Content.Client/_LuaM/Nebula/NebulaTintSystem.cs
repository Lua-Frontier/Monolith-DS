/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using Content.Shared._Lua.AmbientSpaceEffects;
using Content.Shared._Lua.CCVar;
using Content.Shared._Lua.SpaceHazards;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._LuaM.Nebula;

// -- Внутри плотной части туманности картинка сдвигается к её оттенку с сохранением яркости (как режим «Цветность» в Photoshop); шейдер работает только внутри
public sealed partial class NebulaTintSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private const float Strength = 0.3f;
    private const float FadeSpeed = 0.8f;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(0.25);

    private NebulaTintOverlay _overlay = default!;
    private TimeSpan _nextCheck;
    private bool _enabled;
    private bool _inside;
    private Color _target = Color.Transparent;
    private Color _rgb = Color.Transparent;
    private float _alpha;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new NebulaTintOverlay(IoCManager.Resolve<IPrototypeManager>());
        _overlays.AddOverlay(_overlay);
        Subs.CVar(_cfg, LuaCVars.AmbientSpaceEffectsQuality, v => _enabled = v > 0, true);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay(_overlay);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var now = _timing.RealTime;
        if (now >= _nextCheck)
        {
            _nextCheck = now + CheckInterval;
            _inside = _enabled && TryFindNebula(_eye.CurrentEye.Position, out _target);
        }

        if (_inside)
            _rgb = _target;

        var goal = _inside ? Strength : 0f;
        _alpha = goal > _alpha
            ? MathF.Min(goal, _alpha + FadeSpeed * Strength * frameTime)
            : MathF.Max(goal, _alpha - FadeSpeed * Strength * frameTime);

        _overlay.Tint = _rgb;
        _overlay.Strength = _alpha;
    }

    private bool TryFindNebula(MapCoordinates eye, out Color color)
    {
        color = Color.Transparent;
        if (eye.MapId == MapId.Nullspace)
            return false;

        var query = EntityQueryEnumerator<AmbientSpaceFieldComponent, TransformComponent>();
        while (query.MoveNext(out _, out var field, out var xform))
        {
            if (xform.MapID != eye.MapId || field.Seed == 0)
                continue;

            if (!NebulaVeilHelpers.IsInMidZone(field, _transform.GetWorldPosition(xform), eye.Position))
                continue;

            color = AmbientSpacePalette.ColorFromSeed(field.Seed);
            return true;
        }

        return false;
    }
}

public sealed class NebulaTintOverlay : Overlay
{
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    private static readonly ProtoId<ShaderPrototype> GradeShader = "NebulaGrade";

    private readonly ShaderInstance _shader;

    public Color Tint = Color.White;
    public float Strength;

    public NebulaTintOverlay(IPrototypeManager prototypes)
    {
        _shader = prototypes.Index(GradeShader).InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return Strength > 0.001f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("tint", new System.Numerics.Vector3(Tint.R, Tint.G, Tint.B));
        _shader.SetParameter("strength", Strength);

        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
