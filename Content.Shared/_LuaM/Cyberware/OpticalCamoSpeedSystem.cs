using Content.Shared.Movement.Systems;

namespace Content.Shared._LuaM.Cyberware;

/// <summary>
/// Speed boost of an active optical camo. Shared so the client predicts the movement.
/// </summary>
public sealed partial class OpticalCamoSpeedSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _movement = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OpticalCamoComponent, RefreshMovementSpeedModifiersEvent>(OnRefresh);
        SubscribeLocalEvent<OpticalCamoComponent, AfterAutoHandleStateEvent>(OnState);
    }

    private void OnRefresh(Entity<OpticalCamoComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Active && ent.Comp.LifeStage <= ComponentLifeStage.Running)
            args.ModifySpeed(ent.Comp.SpeedModifier, ent.Comp.SpeedModifier);
    }

    private void OnState(Entity<OpticalCamoComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _movement.RefreshMovementSpeedModifiers(ent);
    }
}
