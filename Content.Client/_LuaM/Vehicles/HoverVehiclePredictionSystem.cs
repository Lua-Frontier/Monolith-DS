using Content.Shared._LuaM.Vehicles.Components;
using Robust.Client.Physics;
using Robust.Client.Player;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;

namespace Content.Client._LuaM.Vehicles;

public sealed partial class HoverVehiclePredictionSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IPlayerManager _player = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HoverVehicleComponent, UpdateIsPredictedEvent>(OnUpdateIsPredicted);
        SubscribeLocalEvent<VehicleSeatsComponent, AfterAutoHandleStateEvent>(OnSeatsState);
        SubscribeLocalEvent<VehicleOccupantComponent, LocalPlayerAttachedEvent>(OnLocalPlayerChanged);
        SubscribeLocalEvent<VehicleOccupantComponent, LocalPlayerDetachedEvent>(OnLocalPlayerChanged);
    }

    private void OnUpdateIsPredicted(Entity<HoverVehicleComponent> ent, ref UpdateIsPredictedEvent args)
    {
        if (_player.LocalEntity is { } local &&
            TryComp<VehicleSeatsComponent>(ent, out var seats) && seats.Driver == local)
        {
            args.IsPredicted = true;
        }
    }

    private void OnSeatsState(Entity<VehicleSeatsComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (HasComp<HoverVehicleComponent>(ent))
            _physics.UpdateIsPredicted(ent);
    }

    private void OnLocalPlayerChanged<T>(Entity<VehicleOccupantComponent> ent, ref T args)
    {
        if (HasComp<HoverVehicleComponent>(ent.Comp.Vehicle))
            _physics.UpdateIsPredicted(ent.Comp.Vehicle);
    }
}
