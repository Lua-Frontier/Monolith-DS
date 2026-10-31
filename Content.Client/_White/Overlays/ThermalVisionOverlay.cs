using System.Linq;
using System.Numerics;
using Content.Client.Stealth;
using Content.Shared._White.Overlays;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Stealth.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes; // LuaM
using Robust.Shared.Timing;

namespace Content.Client._White.Overlays;

public sealed partial class ThermalVisionOverlay : Overlay
{
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!; // LuaM

    private readonly TransformSystem _transform;
    private readonly StealthSystem _stealth;
    private readonly ContainerSystem _container;
    private readonly SharedPointLightSystem _light;
    private readonly SpriteSystem _sprite; // LuaM

    private static readonly ProtoId<ShaderPrototype> ThermalShader = "ThermalVision"; // LuaM: Goobstation thermal shader

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly List<ThermalVisionRenderEntry> _entries = [];

    private EntityUid? _lightEntity;

    public float LightRadius;

    public ThermalVisionComponent? Comp;

    public ThermalVisionOverlay()
    {
        IoCManager.InjectDependencies(this);

        _container = _entity.System<ContainerSystem>();
        _transform = _entity.System<TransformSystem>();
        _stealth = _entity.System<StealthSystem>();
        _light = _entity.System<SharedPointLightSystem>();
        _sprite = _entity.System<SpriteSystem>(); // LuaM

        ZIndex = -1;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture is null || Comp is null)
            return;

        var worldHandle = args.WorldHandle;
        var eye = args.Viewport.Eye;

        if (eye == null)
            return;

        var player = _player.LocalEntity;

        if (!_entity.TryGetComponent(player, out TransformComponent? playerXform))
            return;

        var accumulator = Math.Clamp(Comp.PulseAccumulator, 0f, Comp.PulseTime);
        var alpha = Comp.PulseTime <= 0f ? 1f : float.Lerp(1f, 0f, accumulator / Comp.PulseTime);

        // Thermal vision grants some night vision (clientside light)
        if (LightRadius > 0)
        {
            _lightEntity ??= _entity.SpawnAttachedTo(null, playerXform.Coordinates);
            _transform.SetParent(_lightEntity.Value, player.Value);
            var light = _entity.EnsureComponent<PointLightComponent>(_lightEntity.Value);
            _light.SetRadius(_lightEntity.Value, LightRadius, light);
            _light.SetEnergy(_lightEntity.Value, alpha, light);
            _light.SetColor(_lightEntity.Value, Comp.Color, light);
        }
        else
            ResetLight();

        var mapId = eye.Position.MapId;
        var eyeRot = eye.Rotation;

        _entries.Clear();
        var entities = _entity.EntityQueryEnumerator<BodyComponent, SpriteComponent, TransformComponent>();
        while (entities.MoveNext(out var uid, out var body, out var sprite, out var xform))
        {
            if (!CanSee(uid, sprite) || !body.ThermalVisibility)
                continue;

            // Mono - teargas hides you from smoke
            if (_entity.HasComponent<SmokeAffectedComponent>(uid))
                continue; // LuaM: return > continue, one entity in smoke hid everyone from thermals

            var entity = uid;

            if (_container.TryGetOuterContainer(uid, xform, out var container))
            {
                continue; // Mono

                // Mono edit, Thermals don't reveal people in lockers
                /*
                var owner = container.Owner;
                if (_entity.TryGetComponent<SpriteComponent>(owner, out var ownerSprite)
                    && _entity.TryGetComponent<TransformComponent>(owner, out var ownerXform))
                {
                    entity = owner;
                    sprite = ownerSprite;
                    xform = ownerXform;
                }
                */
                // Mono End
            }

            if (_entries.Any(e => e.Ent.Owner == entity))
                continue;

            _entries.Add(new ThermalVisionRenderEntry((entity, sprite, xform), mapId, eyeRot));
        }

        foreach (var entry in _entries)
        {
            Render(entry.Ent, entry.Map, worldHandle, entry.EyeRot, Comp.Color, alpha);
        }

        worldHandle.SetTransform(Matrix3x2.Identity);
    }

    private void Render(Entity<SpriteComponent, TransformComponent> ent,
        MapId? map,
        DrawingHandleWorld handle,
        Angle eyeRot,
        Color color,
        float alpha)
    {
        var (uid, sprite, xform) = ent;
        if (xform.MapID != map || !CanSee(uid, sprite))
            return;

        var position = _transform.GetWorldPosition(xform);
        var rotation = _transform.GetWorldRotation(xform);


        // LuaM start: Goobstation thermal shader instead of a flat tint
        // Layer shaders reset the handle shader, so they are taken off for the draw. Displaced clothing layers
        // need their shader to render right, those are hidden instead.
        var originalColor = sprite.Color;
        var layerData = new Dictionary<int, (ShaderInstance? Shader, Color Color)>();
        var allLayers = sprite.AllLayers.ToList();
        for (var i = 0; i < allLayers.Count; i++)
        {
            if (allLayers[i] is not SpriteComponent.Layer { Visible: true } layer)
                continue;

            if (layer.ShaderPrototype?.Id is "DisplacedDraw" or "DisplacedStencilDraw")
                _sprite.LayerSetVisible((uid, sprite), i, false);

            layerData[i] = (layer.Shader, layer.Color);
            layer.Shader = null;
            _sprite.LayerSetColor(layer, Color.White.WithAlpha(layer.Color.A));
        }

        _sprite.SetColor((uid, sprite), Color.White.WithAlpha(alpha));
        handle.UseShader(_prototype.Index(ThermalShader).Instance());
        _sprite.RenderSprite((uid, sprite), handle, eyeRot, rotation, position);
        handle.UseShader(null);
        _sprite.SetColor((uid, sprite), originalColor);

        foreach (var (index, data) in layerData)
        {
            ((SpriteComponent.Layer) sprite[index]).Shader = data.Shader;
            _sprite.LayerSetColor((uid, sprite), index, data.Color);
            _sprite.LayerSetVisible((uid, sprite), index, true);
        }
        // LuaM end
    }

    private bool CanSee(EntityUid uid, SpriteComponent sprite)
    {
        // LuaM: thermals see the body heat of stealthed people too, the stealth visibility check was removed
        return sprite.Visible;
    }

    public void ResetLight(bool checkFirstTimePredicted = true)
    {
        if (_lightEntity == null || checkFirstTimePredicted && !_timing.IsFirstTimePredicted)
            return;

        _entity.DeleteEntity(_lightEntity);
        _lightEntity = null;
    }
}

public record struct ThermalVisionRenderEntry(
    Entity<SpriteComponent, TransformComponent> Ent,
    MapId? Map,
    Angle EyeRot);
