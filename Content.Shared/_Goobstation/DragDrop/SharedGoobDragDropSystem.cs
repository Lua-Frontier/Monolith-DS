using Content.Shared.Chemistry.Components; // LuaM
using Content.Shared.DragDrop;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Robust.Shared.Timing;

namespace Content.Shared._Goobstation.DragDrop;

public abstract partial class SharedGoobDragDropSystem : EntitySystem
{
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ItemComponent, CanDragEvent>(CanDragItem);
    }

    // so you can drag-drop items
    // doesn't need CanDragDrop check
    private void CanDragItem(Entity<ItemComponent> ent, ref CanDragEvent args)
    {
        args.Handled = true;
    }

    public bool CanDragDrop(EntityUid uid)
    {
        return HasComp<HandsComponent>(uid);
    }

    // copypaste avoidance methods
    protected void OnDragDrop(EntityUid uid, ref DragDropTargetEvent args)
    {
        if (!_timing.IsFirstTimePredicted || args.Handled || !CanDragDrop(args.User))
            return;

        // LuaM-start: a beaker or jug dragged onto something that takes its contents, like a ChemMaster,
        // only pours them in and is not inserted as well
        if (TryComp<RefillableSolutionComponent>(args.Dragged, out var refillable)
            && !refillable.PreventTransferOut
            && HasComp<DumpableSolutionComponent>(uid))
            return;
        // LuaM-end

        args.Handled = _interaction.InteractUsing(args.User, args.Dragged, uid, Transform(uid).Coordinates);
    }

    protected void CanDropTarget(EntityUid uid, ref CanDropTargetEvent args)
    {
        if (HasComp<ItemComponent>(args.Dragged) && CanDragDrop(args.User))
        {
            args.CanDrop = true;
            args.Handled = true;
        }
    }
}
