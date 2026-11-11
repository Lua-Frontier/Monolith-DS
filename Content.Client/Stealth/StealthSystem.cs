using Content.Client.Graphics;
using Content.Client.Interactable.Components;
using Content.Client.StatusIcon;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player; // LuaM
using Robust.Shared.Prototypes;
using Content.Shared._LuaM.Cyberware; // LuaM

namespace Content.Client.Stealth;

public sealed partial class StealthSystem : SharedStealthSystem
{
    private static readonly ProtoId<ShaderPrototype> StealthShader = "Stealth";

    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private SharedTransformSystem _transformSystem = default!;
    [Dependency] private SpriteSystem _sprite = default!; // LuaM
    [Dependency] private IPlayerManager _player = default!; // LuaM

    private ShaderInstance _shader = default!;

    public override void Initialize()
    {
        base.Initialize();

        _shader = _protoMan.Index<ShaderPrototype>(StealthShader).InstanceUnique();

        SubscribeLocalEvent<StealthComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<StealthComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<StealthComponent, BeforePostShaderRenderEvent>(OnShaderRender);
        SubscribeLocalEvent<StealthComponent, OpticalCamoStateChangedEvent>(OnCamoStateChanged); // LuaM
    }

    // LuaM-start: stealth starts up before the camo state arrives, redo the shader once it does
    private void OnCamoStateChanged(Entity<StealthComponent> ent, ref OpticalCamoStateChangedEvent args)
    {
        SetShader(ent, ent.Comp.Enabled, ent.Comp);
    }
    // LuaM-end

    public override void SetEnabled(EntityUid uid, bool value, StealthComponent? component = null)
    {
        if (!Resolve(uid, ref component) || component.Enabled == value)
            return;

        base.SetEnabled(uid, value, component);
        SetShader(uid, value, component);
    }

    private void SetShader(EntityUid uid, bool enabled, StealthComponent? component = null, SpriteComponent? sprite = null)
    {
        if (!Resolve(uid, ref component, ref sprite, false))
            return;

        _sprite.SetColor((uid, sprite), Color.White);

        // LuaM-start: optical camo is plain transparency without the distortion, the owner sees a faint outline of themselves
        // Only while the camo is on, stealth from clothing on an implant holder keeps the normal shader.
        var camo = enabled && TryComp<OpticalCamoComponent>(uid, out var camoComp) && camoComp.Active ? camoComp : null;
        if (camo != null)
            _sprite.SetColor((uid, sprite), Color.White.WithAlpha(uid == _player.LocalEntity ? camo.SelfAlpha : 0f));
        // LuaM-end

        // LuaM start: per-id post shaders since RT 286, the legacy flags are not read anymore
        if (enabled && camo == null) // LuaM: no distortion for optical camo
        {
            _sprite.SetPostShader((uid, sprite), new SpriteComponent.PostShaderArgs(ContentPostShaderIds.Stealth, _shader)
            {
                GetScreenTexture = true,
                RaiseShaderEvent = true,
                Before = ContentPostShaderIds.BeforeOutlines,
            });
        }
        else
        {
            _sprite.RemovePostShader((uid, sprite), ContentPostShaderIds.Stealth);
        }
        // LuaM end

        if (!enabled)
        {
            if (component.HadOutline && !TerminatingOrDeleted(uid))
                EnsureComp<InteractionOutlineComponent>(uid);
            return;
        }

        if (TryComp(uid, out InteractionOutlineComponent? outline))
        {
            RemCompDeferred(uid, outline);
            component.HadOutline = true;
        }
    }

    private void OnStartup(EntityUid uid, StealthComponent component, ComponentStartup args)
    {
        SetShader(uid, component.Enabled, component);
    }

    private void OnShutdown(EntityUid uid, StealthComponent component, ComponentShutdown args)
    {
        if (!Terminating(uid))
            SetShader(uid, false, component);
    }

    private void OnShaderRender(EntityUid uid, StealthComponent component, BeforePostShaderRenderEvent args)
    {
        // Distortion effect uses screen coordinates. If a player moves, the entities appear to move on screen. this
        // makes the distortion very noticeable.

        // So we need to use relative screen coordinates. The reference frame we use is the parent's position on screen.
        // this ensures that if the Stealth is not moving relative to the parent, its relative screen position remains
        // unchanged.
        var parent = Transform(uid).ParentUid;
        if (!parent.IsValid())
            return; // should never happen, but lets not kill the client.
        var parentXform = Transform(parent);
        var reference = args.Viewport.WorldToLocal(_transformSystem.GetWorldPosition(parentXform));
        reference.X = -reference.X;
        var visibility = GetVisibility(uid, component);

        // Goobstation - Proper invisibility: changes -1 to -1.5
        // actual visual visibility effect is limited to -1.5 to 1.
        visibility = Math.Clamp(visibility, -1.5f, 1f);

        _shader.SetParameter("reference", reference);
        _shader.SetParameter("visibility", visibility);

        visibility = MathF.Max(0, visibility);
        _sprite.SetColor((uid, args.Sprite), new Color(visibility, visibility, 1, 1));
    }
}
