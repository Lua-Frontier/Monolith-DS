using Content.Shared.Construction;
using Content.Shared.FixedPoint; // LuaM
using Content.Shared.Interaction;
using Content.Shared.Stacks; // LuaM
using Content.Shared.Storage;
using Content.Shared.Tools.Components;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared.Tools.Systems;

public sealed partial class ToolRefinablSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedToolSystem _toolSystem = default!;
    [Dependency] private SharedStackSystem _stack = default!; // LuaM

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ToolRefinableComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ToolRefinableComponent, WelderRefineDoAfterEvent>(OnDoAfter);
    }

    private void OnInteractUsing(EntityUid uid, ToolRefinableComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var count = GetRefineCount(uid, component, args.Used); // LuaM

        args.Handled = _toolSystem.UseTool(
            args.Used,
            args.User,
            uid,
            component.RefineTime,
            component.QualityNeeded,
            new WelderRefineDoAfterEvent(count), // LuaM: count
            fuel: component.RefineFuel * count); // LuaM: * count
    }

    // LuaM-start: a stack is refined whole, as far as the welder's fuel goes
    /// <summary>
    /// How many units of a stack one welding refines: all of them, or as many as the fuel in the welder pays for.
    /// The fuel the welder burns just by being lit during the weld is kept aside, otherwise the weld would run dry
    /// halfway. With not even enough fuel for one unit the usual "not enough fuel" message shows.
    /// </summary>
    private int GetRefineCount(EntityUid uid, ToolRefinableComponent component, EntityUid tool)
    {
        if (!TryComp<StackComponent>(uid, out var stack) || stack.Count <= 1)
            return 1;

        if (component.RefineFuel <= 0 || !TryComp<WelderComponent>(tool, out var welder))
            return stack.Count;

        var (fuel, _) = _toolSystem.GetWelderFuelAndCapacity(tool, welder);
        FixedPoint2 consumption = welder.FuelConsumption; // read the welder's field, calling methods on it needs execute access
        var burned = consumption.Float() * (component.RefineTime + welder.WelderUpdateTimer);
        var affordable = (int) ((fuel.Float() - burned) / component.RefineFuel + 0.001f);
        return Math.Clamp(affordable, 1, stack.Count);
    }
    // LuaM-end

    private void OnDoAfter(EntityUid uid, ToolRefinableComponent component, WelderRefineDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        if (_net.IsClient)
            return;

        var xform = Transform(uid);

        // LuaM-start: refine the counted units of a stack, the results come out as stacks too
        var count = args.Count;
        TryComp<StackComponent>(uid, out var stack);
        if (stack != null)
            count = Math.Clamp(count, 1, stack.Count);

        var results = new Dictionary<string, int>();
        for (var i = 0; i < count; i++)
        {
            foreach (var spawn in EntitySpawnCollection.GetSpawns(component.RefineResult, _random))
            {
                results[spawn] = results.GetValueOrDefault(spawn) + 1;
            }
        }

        foreach (var (spawn, amount) in results)
        {
            var left = amount;
            while (left > 0)
            {
                var result = SpawnNextToOrDrop(spawn, uid, xform);
                if (!TryComp<StackComponent>(result, out var resultStack))
                {
                    left--;
                    continue;
                }

                var put = Math.Min(left, _stack.GetMaxCount(resultStack));
                _stack.SetCount(result, put, resultStack);
                left -= put;
            }
        }

        if (stack != null && stack.Count > count)
        {
            _stack.SetCount(uid, stack.Count - count, stack);
            return;
        }
        // LuaM-end

        Del(uid);
    }
}
