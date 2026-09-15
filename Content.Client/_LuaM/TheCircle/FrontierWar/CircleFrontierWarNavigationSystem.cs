using System.Numerics;
using Content.Shared._LuaM.TheCircle.FrontierWar;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;

namespace Content.Client._LuaM.TheCircle.FrontierWar;

public sealed class CircleFrontierWarNavigationSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IResourceCache _resources = default!;

    private CircleFrontierWarNavigationOverlay _overlay = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new CircleFrontierWarNavigationOverlay(EntityManager, _players, _resources);
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        _overlays.RemoveOverlay(_overlay);
        base.Shutdown();
    }
}

public sealed class CircleFrontierWarNavigationOverlay(
    IEntityManager entities,
    IPlayerManager players,
    IResourceCache resources) : Overlay
{
    private readonly SharedTransformSystem _transform = entities.System<SharedTransformSystem>();
    private readonly Font _font = resources.GetFont("/Fonts/NotoSans/NotoSans-Bold.ttf", 16);

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null || players.LocalEntity is not { } player ||
            !entities.TryGetComponent<CircleFrontierWarNavigationComponent>(player, out var navigation) ||
            !navigation.Visible || !entities.TryGetComponent<TransformComponent>(player, out var playerTransform))
            return;

        var center = args.ViewportControl.WorldToScreen(_transform.GetWorldPosition(playerTransform));
        args.ScreenHandle.DrawString(_font,
            center + new Vector2(-125f, -155f),
            $"CREW {navigation.CrewReinforcements}  |  CIRCLE {navigation.CircleReinforcements}",
            Color.LightBlue);
        var query = entities.EntityQueryEnumerator<CircleFrontierWarNavigationTargetComponent, TransformComponent>();
        while (query.MoveNext(out _, out var target, out var targetTransform))
        {
            if (target.Side != 0 && target.Side != navigation.Side ||
                !navigation.ObjectivesVisible && target.Kind != 2)
                continue;

            var targetScreen = args.ViewportControl.WorldToScreen(_transform.GetWorldPosition(targetTransform));
            var offset = targetScreen - center;
            if (offset.LengthSquared() < 1f)
                continue;

            var direction = Vector2.Normalize(offset);
            var marker = center + direction * 105f;
            var color = target.Kind == 2
                ? Color.MediumPurple
                : navigation.Side == 2 ? Color.CornflowerBlue : Color.Cyan;
            args.ScreenHandle.DrawLine(center + direction * 62f, marker - direction * 8f, color);
            args.ScreenHandle.DrawString(_font, marker, $"▲ {target.Label}", color);
        }
    }
}
