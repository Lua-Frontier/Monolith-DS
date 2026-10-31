using Content.Shared._LuaM.Fluids;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._LuaM.Fluids;

/// <summary>
/// Dries out the reagents of puddles that don't evaporate on their own, see <see cref="PuddleDryingComponent"/>.
/// </summary>
public sealed partial class PuddleDryingSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;

    private static readonly TimeSpan TickCooldown = TimeSpan.FromSeconds(1);

    private TimeSpan _nextTick;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PuddleDryingComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<PuddleDryingComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.DryingStart = _timing.CurTime + ent.Comp.Delay;
        Dirty(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        if (curTime < _nextTick)
            return;

        _nextTick = curTime + TickCooldown;

        var query = EntityQueryEnumerator<PuddleDryingComponent, PuddleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var drying, out var puddle, out var xform))
        {
            if (drying.DryingStart > curTime)
                continue;

            if (!_solutionContainer.ResolveSolution(uid, puddle.SolutionName, ref puddle.Solution, out var solution))
                continue;

            var dried = false;
            foreach (var reagent in solution.GetReagentPrototypes(_prototype).Keys)
            {
                // Water and the like evaporate on their own, see PuddleSystem.Evaporation
                if (reagent.EvaporationSpeed > FixedPoint2.Zero)
                    continue;

                solution.SplitSolutionWithOnly(drying.Speed * TickCooldown.TotalSeconds, reagent.ID);
                dried = true;
            }

            if (!dried)
                continue;

            // Dried out completely, same as an evaporated puddle
            if (solution.Volume == FixedPoint2.Zero)
            {
                Spawn("PuddleSparkle", xform.Coordinates);
                QueueDel(uid);
            }

            _solutionContainer.UpdateChemicals(puddle.Solution.Value);
        }
    }
}
