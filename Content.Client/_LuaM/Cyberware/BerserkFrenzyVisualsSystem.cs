using Content.Client.Graphics;
using Content.Shared._LuaM.Cyberware;
using Content.Shared.Mobs.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._LuaM.Cyberware;

/// <summary>
/// Local visuals of the player's own berserk heart: the blood screen effect while it runs,
/// and every other creature drawn as a black shadow once the frenzy starts. Nobody else sees any of it.
/// </summary>
public sealed partial class BerserkFrenzyVisualsSystem : EntitySystem
{
    private static readonly ProtoId<ShaderPrototype> ShadowShader = "BerserkShadow";

    [Dependency] private IOverlayManager _overlayMan = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private BerserkBloodOverlay _overlay = default!;
    private ShaderInstance _shadow = default!;

    private bool _overlayShown;
    private bool _shadowsShown;

    public override void Initialize()
    {
        base.Initialize();

        _overlay = new();
        _shadow = _proto.Index(ShadowShader).InstanceUnique();
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlayMan.RemoveOverlay(_overlay);
        RemoveShadows();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var local = _player.LocalEntity;
        TryComp<BerserkHeartComponent>(local, out var heart);

        var active = heart is { Active: true };
        if (active != _overlayShown)
        {
            _overlayShown = active;
            if (active)
                _overlayMan.AddOverlay(_overlay);
            else
                _overlayMan.RemoveOverlay(_overlay);
        }

        if (heart is not { Frenzy: true })
        {
            if (_shadowsShown)
                RemoveShadows();

            return;
        }

        // Every frame, so creatures that come into view later turn into shadows too.
        _shadowsShown = true;
        var query = EntityQueryEnumerator<MobStateComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out var sprite))
        {
            if (uid == local || _sprite.TryGetPostShader(sprite, ContentPostShaderIds.BerserkShadow, out _))
                continue;

            // Before stealth, so a cloaked creature is still hidden and not a black blob.
            _sprite.SetPostShader((uid, sprite), new SpriteComponent.PostShaderArgs(ContentPostShaderIds.BerserkShadow, _shadow)
            {
                Before = ShadowBefore,
            });
        }
    }

    private static readonly string[] ShadowBefore =
    {
        ContentPostShaderIds.Stealth,
        ContentPostShaderIds.InteractionOutline,
        ContentPostShaderIds.TargetOutline,
        ContentPostShaderIds.DragDropOutline,
    };

    /// <summary>
    /// Takes the shadow off every creature that has it. The sprites themselves were never changed,
    /// so nothing can be left black after this.
    /// </summary>
    private void RemoveShadows()
    {
        _shadowsShown = false;

        var query = EntityQueryEnumerator<MobStateComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out var sprite))
        {
            if (_sprite.TryGetPostShader(sprite, ContentPostShaderIds.BerserkShadow, out _))
                _sprite.RemovePostShader((uid, sprite), ContentPostShaderIds.BerserkShadow);
        }
    }
}
