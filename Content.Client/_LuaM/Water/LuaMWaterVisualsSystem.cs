/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using System.Numerics;
using Content.Client._CE.ZLevels.Core;
using Content.Client.Graphics;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._LuaM.Vehicles.Components;
using Content.Shared._LuaM.Water;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Standing;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._LuaM.Water;

// -- Порт погружения в воду из RMC (cmss13#12918): моб плавно опускается спрайтом, подводная часть
// -- окрашивается текстурой воды, у ног рисуется волна. Трансформ и хитбокс моба не меняются
public sealed partial class LuaMWaterVisualsSystem : EntitySystem
{
    [Dependency] private LuaMWaterSystem _water = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private IResourceCache _resources = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly ProtoId<ShaderPrototype> WaterShader = "LuaMWaterSubmersion";
    private static readonly EntProtoId WakeVisual = "LuaMWaterWake";
    private static readonly EntProtoId FoamVisual = "LuaMWaterVehicleFoam";
    private const float SinkRate = 4f;
    private const float RiseRate = 12f;
    private const float ImmersedDepth = 18f;
    // -- В рисунке волн CM ноги стоят в четырёх пикселях над низом холста каждого размера
    private const float FeetLine = 4f;
    private static readonly TimeSpan SurfaceCheckInterval = TimeSpan.FromSeconds(0.1);
    private static readonly TimeSpan MovingLinger = TimeSpan.FromSeconds(0.2);

    private readonly HashSet<EntityUid> _pending = new();
    private bool _newWater;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesAfter.Add(typeof(CEClientZLevelsPostAnimSystem));

        SubscribeLocalEvent<MobStateComponent, ComponentStartup>(OnMobStartup);
        SubscribeLocalEvent<MobStateComponent, ComponentShutdown>(OnMobShutdown);
        SubscribeLocalEvent<MobStateComponent, MoveEvent>(OnMobMove);
        SubscribeLocalEvent<LuaMWaterVehicleComponent, ComponentStartup>(OnVehicleStartup);
        SubscribeLocalEvent<LuaMWaterVehicleComponent, ComponentShutdown>(OnVehicleShutdown);
        SubscribeLocalEvent<LuaMWaterVehicleComponent, MoveEvent>(OnVehicleMove);
        SubscribeLocalEvent<LuaMWaterComponent, ComponentStartup>(OnWaterStartup);
        SubscribeLocalEvent<LuaMWaterVisualsComponent, ComponentShutdown>(OnVisualsShutdown);
        SubscribeLocalEvent<LuaMWaterVisualsComponent, BeforePostShaderRenderEvent>(OnBeforeShader);
    }

    public override void Shutdown()
    {
        _pending.Clear();
        _newWater = false;
        base.Shutdown();
    }

    private void OnMobStartup(Entity<MobStateComponent> ent, ref ComponentStartup args) => _pending.Add(ent);

    private void OnMobShutdown(Entity<MobStateComponent> ent, ref ComponentShutdown args)
    {
        _pending.Remove(ent);
        RemCompDeferred<LuaMWaterVisualsComponent>(ent);
    }

    private void OnMobMove(Entity<MobStateComponent> ent, ref MoveEvent args) => OnSwimmerMove(ent, ref args);

    private void OnVehicleStartup(Entity<LuaMWaterVehicleComponent> ent, ref ComponentStartup args) => _pending.Add(ent);

    private void OnVehicleShutdown(Entity<LuaMWaterVehicleComponent> ent, ref ComponentShutdown args)
    {
        _pending.Remove(ent);
        RemCompDeferred<LuaMWaterVisualsComponent>(ent);
    }

    private void OnVehicleMove(Entity<LuaMWaterVehicleComponent> ent, ref MoveEvent args) => OnSwimmerMove(ent, ref args);

    private void OnSwimmerMove(EntityUid uid, ref MoveEvent args)
    {
        _pending.Add(uid);
        if (args.OldPosition != args.NewPosition && TryComp<LuaMWaterVisualsComponent>(uid, out var visual))
            visual.MovingUntil = _timing.CurTime + MovingLinger;
    }

    // -- Ховер-техника идёт над водой
    private bool IsSwimmer(EntityUid uid)
    {
        return HasComp<MobStateComponent>(uid) ||
               HasComp<LuaMWaterVehicleComponent>(uid) && !HasComp<HoverVehicleComponent>(uid);
    }

    private void OnWaterStartup(Entity<LuaMWaterComponent> ent, ref ComponentStartup args) => _newWater = true;

    private bool UpdateSurface(EntityUid uid)
    {
        if (TerminatingOrDeleted(uid) || !IsSwimmer(uid) || !TryComp<SpriteComponent>(uid, out var sprite))
            return false;

        var found = _water.TryGetWater(uid, out var water, out var covered);
        if (!TryComp<LuaMWaterVisualsComponent>(uid, out var visual))
        {
            if (!found)
                return false;

            visual = AddComp<LuaMWaterVisualsComponent>(uid);
            visual.BaseOffset = sprite.Offset;
            visual.LastOffset = sprite.Offset;
        }

        // -- В прыжке над водой не погружаемся
        var airborne = TryComp<CEZPhysicsComponent>(uid, out var zPhys) && zPhys.LocalPosition > 0.01f;

        // -- Выходя из воды, моб ещё всплывает: текстура прежней воды нужна, иначе подводная часть станет просто полупрозрачной
        if (water != null)
            visual.Water = water.Value.Owner;
        visual.TargetDepth = water is { } surface && !covered && !airborne && _water.CanSubmerge(uid)
            ? Math.Clamp(surface.Comp.Depth, 0f, 32f)
            : 0f;
        visual.NextSurfaceCheck = _timing.CurTime + SurfaceCheckInterval;
        return found;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        // -- Вода приходит по PVS тысячами тайлов, поэтому проверяем не окрестности каждого тайла, а всех мобов один раз
        if (_newWater)
        {
            _newWater = false;
            var mobs = EntityQueryEnumerator<MobStateComponent>();
            while (mobs.MoveNext(out var mob, out _))
                _pending.Add(mob);

            var vehicles = EntityQueryEnumerator<LuaMWaterVehicleComponent>();
            while (vehicles.MoveNext(out var vehicle, out _))
                _pending.Add(vehicle);
        }

        foreach (var uid in _pending)
            UpdateSurface(uid);
        _pending.Clear();

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<LuaMWaterVisualsComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var visual, out var sprite))
        {
            if (time >= visual.NextSurfaceCheck && !UpdateSurface(uid) && visual.Depth <= 0.01f)
            {
                RemCompDeferred<LuaMWaterVisualsComponent>(uid);
                continue;
            }

            // -- Другие системы (z-уровни, пристёгивание, анимации) могут переписать смещение, пока моб в воде
            if (sprite.Offset != visual.LastOffset)
                visual.BaseOffset = sprite.Offset;

            var rate = visual.TargetDepth < visual.Depth ? RiseRate : SinkRate;
            visual.Depth = MathHelper.Lerp(visual.Depth, visual.TargetDepth, Math.Min(1f, frameTime * rate));
            if (MathF.Abs(visual.Depth - visual.TargetDepth) < 0.01f)
                visual.Depth = visual.TargetDepth;

            if (TryComp<LuaMWaterVehicleComponent>(uid, out var vehicle))
            {
                UpdateFoam((uid, visual), sprite, vehicle, visual.TargetDepth > 0);
                continue;
            }

            visual.LastOffset = visual.BaseOffset - new Vector2(0, visual.Depth * sprite.Scale.Y / EyeManager.PixelsPerMeter);
            _sprite.SetOffset((uid, sprite), visual.LastOffset);

            visual.Immersed = _standing.IsDown(uid) && visual.TargetDepth >= ImmersedDepth;

            // -- Вышедшему на сушу подводная часть уже не нужна: иначе ноги ещё всплывают полупрозрачными
            var submerged = visual.Depth > 0 && visual.TargetDepth > 0;
            if (submerged && !_sprite.HasPostShader((uid, sprite), ContentPostShaderIds.WaterSubmersion))
            {
                _sprite.SetPostShader((uid, sprite), new SpriteComponent.PostShaderArgs(
                    ContentPostShaderIds.WaterSubmersion, ProtoMan.Index(WaterShader).InstanceUnique())
                {
                    RaiseShaderEvent = true,
                    Before = [ContentPostShaderIds.Stealth, .. ContentPostShaderIds.BeforeOutlines],
                });
            }
            else if (!submerged)
            {
                _sprite.RemovePostShader((uid, sprite), ContentPostShaderIds.WaterSubmersion);
            }

            var alive = !TryComp<MobStateComponent>(uid, out var mob) || mob.CurrentState != MobState.Dead;
            UpdateSplash((uid, visual), sprite, alive, visual.TargetDepth > 0);
        }
    }

    private void UpdateSplash(Entity<LuaMWaterVisualsComponent> ent, SpriteComponent sprite, bool alive, bool visible)
    {
        visible &= !ent.Comp.Immersed || alive;
        if (!visible)
        {
            if (ent.Comp.Splash != null)
                QueueDel(ent.Comp.Splash);
            ent.Comp.Splash = null;
            ent.Comp.SplashState = null;
            return;
        }

        var xform = Transform(ent);
        if (ent.Comp.Splash is not { } splash || !Exists(splash))
        {
            splash = Spawn(WakeVisual, xform.Coordinates);
            ent.Comp.Splash = splash;
        }

        // -- Волна отдельной сущностью: маскировка моба не должна прятать потревоженную воду
        _transform.SetCoordinates(splash, xform.Coordinates);
        _transform.SetLocalRotation(splash, xform.LocalRotation);
        var splashSprite = Comp<SpriteComponent>(splash);
        var width = sprite.BaseRSI?.Size.X ?? 32;
        var size = width <= 32 ? 32 : width <= 48 ? 48 : width <= 64 ? 64 : 88;
        var depth = ent.Comp.TargetDepth;
        var state = ent.Comp.Immersed ? "bubbles" :
            depth <= 2 ? "coast_shallow" : depth <= 4 ? "coast_deep" : depth <= 8 ? "shallow" : depth <= 12 ? "intermediate" : "deep";
        if (!ent.Comp.Immersed && size == 32 && _standing.IsDown(ent))
            state = $"human_resting_{(depth <= 4 ? "coast" : "deep")}_{(sprite.Rotation.Theta > 0 ? "w" : "e")}";
        var rsi = _resources.GetResource<RSIResource>(new ResPath($"/Textures/_LuaM/Effects/Water/splash{size}.rsi")).RSI;
        if (_timing.CurTime < ent.Comp.MovingUntil && rsi.TryGetState(state + "_moving", out _))
            state += "_moving";

        if (ent.Comp.SplashState != state || ent.Comp.SplashSize != size)
        {
            _sprite.LayerSetRsi((splash, splashSprite), 0, rsi);
            _sprite.LayerSetRsiState((splash, splashSprite), 0, state);
            ent.Comp.SplashState = state;
            ent.Comp.SplashSize = size;
        }

        _sprite.SetOffset((splash, splashSprite), ent.Comp.Immersed ? ent.Comp.BaseOffset : ent.Comp.LastOffset);
        _sprite.SetScale((splash, splashSprite), sprite.Scale);
        // -- Кадры «лёжа» нарисованы стоя и поворачиваются вместе с телом, как в CM
        _sprite.SetRotation((splash, splashSprite), ent.Comp.Immersed ? Angle.Zero : sprite.Rotation);
        _sprite.SetDrawDepth((splash, splashSprite), sprite.DrawDepth + 1);
        _sprite.SetVisible((splash, splashSprite), sprite.Visible);
    }

    private void OnVisualsShutdown(Entity<LuaMWaterVisualsComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Splash != null)
            QueueDel(ent.Comp.Splash);

        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _sprite.RemovePostShader((ent.Owner, sprite), ContentPostShaderIds.WaterSubmersion);
        if (sprite.Offset == ent.Comp.LastOffset)
            _sprite.SetOffset((ent.Owner, sprite), ent.Comp.BaseOffset);
    }

    // -- Пена дочерняя к машине и вращается вместе с ней: движок выбирает ей тот же ракурс, что и кузову
    private void UpdateFoam(Entity<LuaMWaterVisualsComponent> ent, SpriteComponent sprite, LuaMWaterVehicleComponent vehicle, bool visible)
    {
        if (!visible)
        {
            if (ent.Comp.Splash != null)
                QueueDel(ent.Comp.Splash);
            ent.Comp.Splash = null;
            return;
        }

        if (ent.Comp.Splash is not { } foam || !Exists(foam))
        {
            foam = SpawnAttachedTo(FoamVisual, new EntityCoordinates(ent, Vector2.Zero));
            ent.Comp.Splash = foam;
            _sprite.LayerSetSprite(foam, 0, vehicle.Foam);
        }

        var foamSprite = Comp<SpriteComponent>(foam);
        // -- Мелкая техника не вращается спрайтом, а только меняет ракурс: пена должна вести себя так же
        foamSprite.NoRotation = sprite.NoRotation;
        _sprite.SetSnapCardinals((foam, foamSprite), sprite.SnapCardinals);
        _sprite.SetDrawDepth((foam, foamSprite), sprite.DrawDepth + 1);
        _sprite.SetVisible((foam, foamSprite), sprite.Visible);
    }

    private void OnBeforeShader(Entity<LuaMWaterVisualsComponent> ent, ref BeforePostShaderRenderEvent args)
    {
        if (args.Id != ContentPostShaderIds.WaterSubmersion)
            return;

        var viewport = args.Viewport;
        var eyeRotation = viewport.Eye?.Rotation ?? Angle.Zero;
        var position = _transform.GetWorldPosition(ent);
        var height = args.Sprite.BaseRSI?.Size.Y ?? 32;
        var baseline = (FeetLine - height / 2f) * args.Sprite.Scale.Y / EyeManager.PixelsPerMeter;
        var feet = viewport.WorldToLocal(position + (-eyeRotation).RotateVec(ent.Comp.BaseOffset + new Vector2(0, baseline)));
        var origin = viewport.WorldToLocal(position);
        var unit = viewport.WorldToLocal(position + (-eyeRotation).RotateVec(Vector2.UnitX));
        var pixelsPerMeter = Math.Max(1, Vector2.Distance(origin, unit));
        args.Shader.SetParameter("water_line", viewport.Size.Y - feet.Y);
        args.Shader.SetParameter("surface_origin", new Vector2(origin.X, viewport.Size.Y - origin.Y));
        args.Shader.SetParameter("pixels_per_meter", pixelsPerMeter);
        args.Shader.SetParameter("immersed", ent.Comp.Immersed);
        args.Shader.SetParameter("water_light", viewport.LightRenderTarget.Texture);

        // -- Берём текущий кадр анимации воды и её цвет
        Texture? texture = null;
        var color = Color.White;
        if (ent.Comp.Water is { } water &&
            TryComp<SpriteComponent>(water, out var waterSprite) &&
            _sprite.TryGetLayer((water, waterSprite), 0, out var layer, false))
        {
            color = waterSprite.Color * layer.Color;
            if (layer.ActualRsi is { } rsi && rsi.TryGetState(_sprite.LayerGetRsiState((water, waterSprite), 0), out var state))
                texture = state.GetFrame(layer.EffectiveDirection(_transform.GetWorldRotation(water)), layer.AnimationFrame);
            else
                texture = layer.Texture;
        }

        if (texture == null)
        {
            args.Shader.SetParameter("water_texture", Texture.Transparent);
            args.Shader.SetParameter("water_uv", new Vector4(0, 0, 1, 1));
            args.Shader.SetParameter("water_color", Color.Transparent);
            return;
        }

        var region = new Vector4(0, 0, 1, 1);
        if (texture is AtlasTexture atlas)
        {
            texture = atlas.SourceTexture;
            region = new Vector4(atlas.SubRegion.Left / texture.Width, 1 - atlas.SubRegion.Bottom / texture.Height,
                atlas.SubRegion.Right / texture.Width, 1 - atlas.SubRegion.Top / texture.Height);
        }

        args.Shader.SetParameter("water_texture", texture);
        args.Shader.SetParameter("water_uv", region);
        args.Shader.SetParameter("water_color", color);
    }
}
