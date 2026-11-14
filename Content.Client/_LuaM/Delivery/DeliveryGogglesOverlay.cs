using System.Numerics;
using Content.Shared._LuaM.Delivery;
using Content.Shared.Inventory;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Graphics.RSI;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._LuaM.Delivery;

public sealed class DeliveryGogglesOverlay : Overlay
{
    private static readonly ResPath Rsi = new("/Textures/_LuaM/Effects/delivery_target.rsi");

    private readonly IEntityManager _entMan;
    private readonly IPlayerManager _player;
    private readonly IGameTiming _timing;
    private readonly SharedTransformSystem _transform;
    private readonly InventorySystem _inventory;
    private readonly IRsiStateLike _outer;
    private readonly Texture _inner;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public DeliveryGogglesOverlay(IEntityManager entMan, IPlayerManager player, IGameTiming timing)
    {
        _entMan = entMan;
        _player = player;
        _timing = timing;
        _transform = entMan.System<SharedTransformSystem>();
        _inventory = entMan.System<InventorySystem>();

        var sprite = entMan.System<SpriteSystem>();
        _outer = sprite.RsiStateLike(new SpriteSpecifier.Rsi(Rsi, "outer"));
        _inner = sprite.Frame0(new SpriteSpecifier.Rsi(Rsi, "inner"));
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } local
            || !_entMan.TryGetComponent(local, out DeliveryTargetsComponent? targets)
            || targets.Targets.Count == 0)
            return false;

        return _inventory.TryGetSlotEntity(local, "eyes", out var eyes)
               && _entMan.HasComponent<DeliveryGogglesComponent>(eyes);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var targets = _entMan.GetComponent<DeliveryTargetsComponent>(_player.LocalEntity!.Value);
        var handle = args.WorldHandle;
        var time = (float) _timing.RealTime.TotalSeconds;
        var outerAngle = new Angle(time * 0.9f);
        var innerAngle = new Angle(-time * 1.6f);
        var pulse = 0.55f + 0.12f * MathF.Sin(time * 2.2f);
        var outer = _outer.GetFrame(RsiDirection.South, GetFrame(time));
        var outerSize = new Vector2(outer.Width, outer.Height) / EyeManager.PixelsPerMeter;
        var innerSize = new Vector2(_inner.Width, _inner.Height) / EyeManager.PixelsPerMeter;

        foreach (var net in targets.Targets)
        {
            if (!_entMan.TryGetEntity(net, out var target)
                || !_entMan.TryGetComponent(target, out TransformComponent? xform)
                || xform.MapID != args.MapId)
                continue;

            var pos = _transform.GetWorldPosition(xform);
            if (!args.WorldAABB.Enlarged(1f).Contains(pos))
                continue;

            handle.SetTransform(Matrix3Helpers.CreateTransform(pos, outerAngle));
            handle.DrawTextureRect(outer, Box2.CenteredAround(Vector2.Zero, outerSize), Color.White.WithAlpha(pulse));
            handle.SetTransform(Matrix3Helpers.CreateTransform(pos, innerAngle));
            handle.DrawTextureRect(_inner, Box2.CenteredAround(Vector2.Zero, innerSize), Color.White.WithAlpha(pulse));
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    private int GetFrame(float time)
    {
        var count = _outer.AnimationFrameCount;
        if (count <= 1)
            return 0;

        var total = 0f;
        for (var i = 0; i < count; i++)
        {
            total += _outer.GetDelay(i);
        }

        var t = time % total;
        for (var i = 0; i < count; i++)
        {
            t -= _outer.GetDelay(i);
            if (t < 0f)
                return i;
        }

        return count - 1;
    }
}

public sealed partial class DeliveryGogglesSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;

    private DeliveryGogglesOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new DeliveryGogglesOverlay(EntityManager, _player, _timing);
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_overlay != null)
            _overlays.RemoveOverlay(_overlay);
    }
}
