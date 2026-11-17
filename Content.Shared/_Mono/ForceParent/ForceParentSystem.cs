using Robust.Shared.Map;

namespace Content.Shared._Mono.ForceParent;

public sealed partial class ForceParentSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ForceParentComponent, MoveEvent>(OnMove);
    }

    private void OnMove(Entity<ForceParentComponent> ent, ref MoveEvent args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        // LuaM-start
        if (!ent.Comp.Position.EntityId.IsValid())
            return;
        // LuaM-end

        var newPos = args.NewPosition;
        if (newPos.EntityId != ent.Comp.Position.EntityId || newPos.Position != ent.Comp.Position.Position)
        { // LuaM
            _transform.SetParent(ent, ent.Comp.Position.EntityId);
            _transform.SetLocalPositionNoLerp(ent, ent.Comp.Position.Position);
        } // LuaM
    }

    public void SetForceParent(EntityUid uid, EntityCoordinates coord)
    {
        var comp = EnsureComp<ForceParentComponent>(uid);
        comp.Position = coord;
        Transform(uid).GridTraversal = false;
        _transform.SetCoordinates(uid, coord); // if this fails the other method shouldn't
    }
}
