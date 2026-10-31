using Content.Client.Graphics;
using Content.Shared.Chat.TypingIndicator;
using Content.Shared.Holopad;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Linq;
using System.Numerics;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client.Holopad;

public sealed partial class HolopadSystem : SharedHolopadSystem
{
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SpriteSystem _sprite = default!; // LuaM

    // LuaM - the shader of a hologram is kept instead of making a new one on every rebuild
    private readonly Dictionary<EntityUid, ShaderInstance> _shaders = new();

    // LuaM
    // -- слои показываемого копируются 4 раза в секунду, полосы шейдера по-прежнему бегут каждый кадр
    private static readonly TimeSpan RebuildInterval = TimeSpan.FromSeconds(0.25);
    private readonly Dictionary<EntityUid, (TimeSpan Next, EntityUid? Target)> _rebuilds = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HolopadHologramComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<HolopadHologramComponent, ComponentShutdown>(OnComponentShutdown); // LuaM
        SubscribeLocalEvent<HolopadHologramComponent, BeforePostShaderRenderEvent>(OnShaderRender);
        SubscribeAllEvent<TypingChangedEvent>(OnTypingChanged);
    }

    private void OnComponentStartup(Entity<HolopadHologramComponent> entity, ref ComponentStartup ev)
    {
        UpdateHologramSprite(entity, entity.Comp.LinkedEntity);
    }

    // LuaM
    private void OnComponentShutdown(Entity<HolopadHologramComponent> entity, ref ComponentShutdown ev)
    {
        if (_shaders.Remove(entity, out var shader))
            shader.Dispose();

        _rebuilds.Remove(entity);
    }

    private void OnShaderRender(Entity<HolopadHologramComponent> entity, ref BeforePostShaderRenderEvent ev)
    {
        if (ev.Id != ContentPostShaderIds.Holopad) // LuaM: per-id post shaders since RT 286
            return;

        // LuaM-start
        var now = _timing.RealTime;
        if (_rebuilds.TryGetValue(entity, out var rebuild) && now < rebuild.Next && rebuild.Target == entity.Comp.LinkedEntity)
        {
            UpdateHologramShader(entity, ev.Sprite, entity.Comp);
            return;
        }

        _rebuilds[entity] = (now + RebuildInterval, entity.Comp.LinkedEntity);
        // LuaM-end

        UpdateHologramSprite(entity, entity.Comp.LinkedEntity);
    }

    private void OnTypingChanged(TypingChangedEvent ev, EntitySessionEventArgs args)
    {
        var uid = args.SenderSession.AttachedEntity;

        if (!Exists(uid))
            return;

        if (!HasComp<HolopadUserComponent>(uid))
            return;

        var netEv = new HolopadUserTypingChangedEvent(GetNetEntity(uid.Value), ev.State);
        RaiseNetworkEvent(netEv);
    }

    private void UpdateHologramSprite(EntityUid hologram, EntityUid? target)
    {
        // Get required components
        if (!TryComp<SpriteComponent>(hologram, out var hologramSprite) ||
            !TryComp<HolopadHologramComponent>(hologram, out var holopadhologram))
            return;

        // Remove all sprite layers
        for (int i = hologramSprite.AllLayers.Count() - 1; i >= 0; i--)
            hologramSprite.RemoveLayer(i);

        if (TryComp<SpriteComponent>(target, out var targetSprite))
        {
            // Use the target's holographic avatar (if available)
            if (TryComp<HolographicAvatarComponent>(target, out var targetAvatar) &&
                targetAvatar.LayerData != null)
            {
                for (int i = 0; i < targetAvatar.LayerData.Length; i++)
                {
                    var layer = targetAvatar.LayerData[i];
                    hologramSprite.AddLayer(targetAvatar.LayerData[i], i);
                }
            }

            // Otherwise copy the target's current physical appearance
            else
            {
                hologramSprite.CopyFrom(targetSprite);
            }
        }

        // There is no target, display a default sprite instead (if available)
        else
        {
            if (string.IsNullOrEmpty(holopadhologram.RsiPath) || string.IsNullOrEmpty(holopadhologram.RsiState))
                return;

            var layer = new PrototypeLayerData();
            layer.RsiPath = holopadhologram.RsiPath;
            layer.State = holopadhologram.RsiState;

            hologramSprite.AddLayer(layer);
        }

        // Override specific values
        hologramSprite.Color = Color.White;
        hologramSprite.Offset = holopadhologram.Offset;
        hologramSprite.DrawDepth = (int)DrawDepth.Mobs;
        hologramSprite.NoRotation = true;
        hologramSprite.DirectionOverride = Direction.South;
        hologramSprite.EnableDirectionOverride = true;

        // Remove shading from all layers (except displacement maps)
        for (int i = 0; i < hologramSprite.AllLayers.Count(); i++)
        {
            if (!hologramSprite.TryGetLayer(i, out var layer)) // LuaM
                continue;

            // LuaM - the bent clothes keep their bend without the stencil test, in the comms window it cut them off and the one shown looked naked
            hologramSprite.LayerSetShader(i, layer.ShaderPrototype == "DisplacedStencilDraw" ? "HologramDisplacedDraw" : "unshaded");
        }

        // LuaM - drawn only through a UI with its own shader, copying the sprite of the target makes it visible again
        if (holopadhologram.HideInWorld)
        {
            _sprite.ClearPostShaders((hologram, hologramSprite)); // LuaM: per-id post shaders since RT 286
            _sprite.SetVisible((hologram, hologramSprite), false);
            return;
        }

        // LuaM - copying the sprite also copies whether the target is drawn, the hologram must stay up even when it is not
        _sprite.SetVisible((hologram, hologramSprite), true);

        UpdateHologramShader(hologram, hologramSprite, holopadhologram);
    }

    /// <summary>
    /// LuaM - rebuilds the sprite of a hologram that is only drawn through a UI.
    /// In the world this happens before every draw.
    /// </summary>
    public void RefreshHologram(Entity<HolopadHologramComponent> hologram)
    {
        UpdateHologramSprite(hologram, hologram.Comp.LinkedEntity);
    }

    private void UpdateHologramShader(EntityUid uid, SpriteComponent sprite, HolopadHologramComponent holopadHologram)
    {
        // Find the texture height of the largest layer
        float texHeight = sprite.AllLayers.Max(x => x.PixelSize.Y);

        // LuaM - one shader per hologram > a new one every frame
        if (!_shaders.TryGetValue(uid, out var instance))
        {
            instance = _prototypeManager.Index<ShaderPrototype>(holopadHologram.ShaderName).InstanceUnique();
            _shaders.Add(uid, instance);
        }

        instance.SetParameter("color1", new Vector3(holopadHologram.Color1.R, holopadHologram.Color1.G, holopadHologram.Color1.B));
        instance.SetParameter("color2", new Vector3(holopadHologram.Color2.R, holopadHologram.Color2.G, holopadHologram.Color2.B));
        instance.SetParameter("alpha", holopadHologram.Alpha);
        instance.SetParameter("intensity", holopadHologram.Intensity);
        instance.SetParameter("texHeight", texHeight);
        instance.SetParameter("t", (float)_timing.CurTime.TotalSeconds * holopadHologram.ScrollRate);

        // LuaM start: per-id post shaders since RT 286, the hologram keeps only its own shader like the legacy single PostShader did
        if (_sprite.TryGetPostShader((uid, sprite), ContentPostShaderIds.Holopad, out var entry)
            && entry.Shader == instance
            && _sprite.GetPostShaders((uid, sprite)).Count == 1)
        {
            return;
        }

        _sprite.ClearPostShaders((uid, sprite));
        _sprite.SetPostShader((uid, sprite), new SpriteComponent.PostShaderArgs(ContentPostShaderIds.Holopad, instance)
        {
            RaiseShaderEvent = true,
            Before = ContentPostShaderIds.BeforeOutlines,
        });
        // LuaM end
    }
}
