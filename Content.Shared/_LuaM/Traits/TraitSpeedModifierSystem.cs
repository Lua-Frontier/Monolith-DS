using Content.Shared.Movement.Systems;

namespace Content.Shared._LuaM.Traits;

public sealed partial class TraitSpeedModifierSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _movement = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TraitSpeedModifierComponent, RefreshMovementSpeedModifiersEvent>(OnRefresh);
        SubscribeLocalEvent<TraitSpeedModifierComponent, ComponentStartup>(OnStartup);
    }

    private void OnRefresh(Entity<TraitSpeedModifierComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.WalkModifier, ent.Comp.SprintModifier);
    }

    private void OnStartup(Entity<TraitSpeedModifierComponent> ent, ref ComponentStartup args)
    {
        _movement.RefreshMovementSpeedModifiers(ent);
    }
}
