using System.Numerics;
using Content.Shared._LuaM.Vehicles.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;

namespace Content.Client._LuaM.Vehicles;

public sealed class VehicleHeadingOverlay : Overlay
{
    private static readonly Color ArrowColor = Color.White.WithAlpha(0.2f);
    private const float StemStart = 0.62f;
    private const float HeadStart = 0.8f;
    private const float Tip = 1.08f;
    private const float StemHalfWidth = 0.05f;
    private const float HeadHalfWidth = 0.19f;

    private readonly IEntityManager _entMan;
    private readonly IPlayerManager _player;
    private readonly SharedTransformSystem _transform;
    private readonly Vector2[] _vertices = new Vector2[9];
    private EntityUid _vehicle;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public VehicleHeadingOverlay(IEntityManager entMan, IPlayerManager player)
    {
        _entMan = entMan;
        _player = player;
        _transform = entMan.System<SharedTransformSystem>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (_player.LocalEntity is not { } local ||
            !_entMan.TryGetComponent(local, out VehicleOccupantComponent? occupant) ||
            occupant.Role != VehicleSeatRole.Driver ||
            !_entMan.TryGetComponent(occupant.Vehicle, out SpriteComponent? sprite) ||
            !sprite.NoRotation ||
            !_entMan.TryGetComponent(occupant.Vehicle, out TransformComponent? xform) ||
            xform.MapID != args.MapId)
        {
            return false;
        }

        _vehicle = occupant.Vehicle;
        return true;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var (pos, rot) = _transform.GetWorldPositionRotation(_vehicle);
        var forward = rot.ToWorldVec();
        var side = new Vector2(-forward.Y, forward.X);

        var stemBack = pos + forward * StemStart;
        var stemFront = pos + forward * HeadStart;
        _vertices[0] = stemBack + side * StemHalfWidth;
        _vertices[1] = stemBack - side * StemHalfWidth;
        _vertices[2] = stemFront + side * StemHalfWidth;
        _vertices[3] = stemFront + side * StemHalfWidth;
        _vertices[4] = stemBack - side * StemHalfWidth;
        _vertices[5] = stemFront - side * StemHalfWidth;
        _vertices[6] = stemFront + side * HeadHalfWidth;
        _vertices[7] = stemFront - side * HeadHalfWidth;
        _vertices[8] = pos + forward * Tip;

        var handle = args.WorldHandle;
        handle.SetTransform(Matrix3x2.Identity);
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _vertices, ArrowColor);
    }
}

public sealed partial class VehicleHeadingSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _player = default!;

    private VehicleHeadingOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new VehicleHeadingOverlay(EntityManager, _player);
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_overlay != null)
            _overlays.RemoveOverlay(_overlay);
    }
}
