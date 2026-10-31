using Content.Server._Mono.Cleanup;
using Content.Server.GameTicking.Rules;
using Robust.Server.Physics;

namespace Content.Server._LuaM.Cleanup;

/// <summary>
/// Hands out grid cleanup settings: to the grids loaded by a game rule with <see cref="RuleGridsCleanupComponent"/>,
/// and from a grid to the pieces it breaks into, so wrecks get cleaned up like the grid they came from.
/// </summary>
public sealed class GridCleanupInheritSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RuleGridsCleanupComponent, RuleLoadedGridsEvent>(OnRuleLoadedGrids);
        SubscribeLocalEvent<GridSplitEvent>(OnGridSplit);
    }

    private void OnRuleLoadedGrids(Entity<RuleGridsCleanupComponent> ent, ref RuleLoadedGridsEvent args)
    {
        foreach (var grid in args.Grids)
        {
            var state = EnsureComp<GridCleanupGridComponent>(grid);
            state.CleanupAcceleration = ent.Comp.CleanupAcceleration;
            state.DistanceOverride = ent.Comp.DistanceOverride;
            state.IgnorePowered = ent.Comp.IgnorePowered;
            state.IgnorePrice = ent.Comp.IgnorePrice;
            state.IgnoreIFF = ent.Comp.IgnoreIFF;
        }
    }

    private void OnGridSplit(ref GridSplitEvent args)
    {
        if (!TryComp<GridCleanupGridComponent>(args.Grid, out var source))
            return;

        foreach (var grid in args.NewGrids)
        {
            var state = EnsureComp<GridCleanupGridComponent>(grid);
            state.CleanupAcceleration = source.CleanupAcceleration;
            state.DistanceOverride = source.DistanceOverride;
            state.IgnorePowered = source.IgnorePowered;
            state.IgnorePrice = source.IgnorePrice;
            state.IgnoreIFF = source.IgnoreIFF;
        }
    }
}
