using Content.Shared._LuaM.Vehicles.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Popups;
using Robust.Shared.Network;

namespace Content.Shared._LuaM.Vehicles;

public sealed partial class VehicleFuelSystem : EntitySystem
{
    [Dependency] private VehicleEngineSystem _engine = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleFuelComponent, VehicleCanMoveEvent>(OnCanMove);
        SubscribeLocalEvent<VehicleFuelComponent, VehicleEngineStartAttemptEvent>(OnStartAttempt);
        SubscribeLocalEvent<VehicleFuelComponent, SolutionTransferAttemptEvent>(OnTransferAttempt);
        SubscribeLocalEvent<VehicleFuelComponent, SolutionTransferredEvent>(OnTransferred);
        SubscribeLocalEvent<VehicleFuelComponent, ExaminedEvent>(OnExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<VehicleFuelComponent, VehicleEngineComponent, GridVehicleMoverComponent>();
        while (query.MoveNext(out var uid, out var fuel, out var engine, out var mover))
        {
            if (!fuel.Enabled || !engine.EngineOn)
            {
                fuel.Accumulator = 0f;
                continue;
            }

            fuel.Accumulator += frameTime;
            if (fuel.Accumulator < fuel.UpdateInterval)
                continue;

            var elapsed = fuel.Accumulator;
            fuel.Accumulator = 0f;

            var load = mover.MaxSpeed > 0f ? Math.Clamp(MathF.Abs(mover.CurrentSpeed) / mover.MaxSpeed, 0f, 1f) : 0f;
            var rate = fuel.ConsumptionPerMinute / 60f * (fuel.IdleFraction + (1f - fuel.IdleFraction) * load);
            if (!_solution.TryGetSolution(uid, fuel.Solution, out var soln, out _))
                continue;

            _solution.RemoveReagent(soln.Value, fuel.Reagent, FixedPoint2.New(rate * elapsed));
            if (GetFuel(uid, fuel) > FixedPoint2.Zero)
                continue;

            _engine.Stall(uid);
            if (TryComp<VehicleSeatsComponent>(uid, out var seats) && seats.Driver is { } driver)
                _popup.PopupEntity(Loc.GetString("luam-vehicle-fuel-ran-out"), uid, driver, PopupType.MediumCaution);
        }
    }

    public FixedPoint2 GetFuel(EntityUid uid, VehicleFuelComponent fuel)
    {
        return _solution.TryGetSolution(uid, fuel.Solution, out _, out var solution)
            ? solution.GetTotalPrototypeQuantity(fuel.Reagent)
            : FixedPoint2.Zero;
    }

    private void OnCanMove(Entity<VehicleFuelComponent> ent, ref VehicleCanMoveEvent args)
    {
        if (ent.Comp.Enabled && GetFuel(ent, ent.Comp) <= FixedPoint2.Zero)
            args.Cancelled = true;
    }

    private void OnStartAttempt(Entity<VehicleFuelComponent> ent, ref VehicleEngineStartAttemptEvent args)
    {
        if (!ent.Comp.Enabled || GetFuel(ent, ent.Comp) > FixedPoint2.Zero)
            return;

        args.Cancelled = true;
        _popup.PopupEntity(Loc.GetString("luam-vehicle-fuel-empty"), ent, args.Driver, PopupType.MediumCaution);
    }

    private void OnTransferAttempt(Entity<VehicleFuelComponent> ent, ref SolutionTransferAttemptEvent args)
    {
        if (!ent.Comp.Enabled || args.To != ent.Owner || args.CancelReason != null ||
            !_solution.TryGetDrainableSolution(args.From, out _, out var source))
        {
            return;
        }

        foreach (var reagent in source.Contents)
        {
            if (reagent.Reagent.Prototype == ent.Comp.Reagent)
                continue;

            args.Cancel(Loc.GetString("luam-vehicle-fuel-wrong-reagent"));
            return;
        }
    }

    private void OnTransferred(Entity<VehicleFuelComponent> ent, ref SolutionTransferredEvent args)
    {
        if (ent.Comp.Enabled)
            _engine.TryStart(ent);
    }

    private void OnExamined(Entity<VehicleFuelComponent> ent, ref ExaminedEvent args)
    {
        if (!ent.Comp.Enabled || !_solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var solution))
            return;

        var amount = GetFuel(ent, ent.Comp);
        var color = amount <= solution.MaxVolume * 0.2f ? "red" : amount <= solution.MaxVolume * 0.5f ? "yellow" : "lightgreen";
        args.PushMarkup(Loc.GetString("luam-vehicle-fuel-examine",
            ("color", color),
            ("amount", amount.Int()),
            ("max", solution.MaxVolume.Int())));
    }
}
