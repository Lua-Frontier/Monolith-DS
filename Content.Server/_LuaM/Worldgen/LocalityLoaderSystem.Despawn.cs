using Content.Server._NF.Salvage;
using Content.Shared.Humanoid;
using Content.Shared.Mech.Components;
using Content.Shared.Mind.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Worldgen.Systems;

public sealed partial class LocalityLoaderSystem
{
    [Dependency] private IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, List<EntityUid>> _restrictionsByGrid = new();
    private readonly Stack<EntityUid> _reparentWalk = new();
    private GameTick _restrictionsTick = GameTick.Zero;

    private void DespawnRestrictions(EntityUid grid)
    {
        if (_restrictionsTick != _timing.CurTick)
        {
            _restrictionsTick = _timing.CurTick;
            _restrictionsByGrid.Clear();
            var query = AllEntityQuery<NFSalvageMobRestrictionsComponent>();
            while (query.MoveNext(out var uid, out var restrictions))
            {
                if (!restrictions.DespawnIfOffLinkedGrid)
                    continue;

                if (!_restrictionsByGrid.TryGetValue(restrictions.LinkedGridEntity, out var list))
                    _restrictionsByGrid[restrictions.LinkedGridEntity] = list = new List<EntityUid>();

                list.Add(uid);
            }
        }

        if (!_restrictionsByGrid.Remove(grid, out var linked))
            return;

        foreach (var uid in linked)
        {
            QueueDel(uid);
        }
    }

    private bool MayHoldPlayers(EntityUid grid)
    {
        _reparentWalk.Clear();
        _reparentWalk.Push(grid);
        while (_reparentWalk.Count > 0)
        {
            var parent = _reparentWalk.Pop();
            var children = Transform(parent).ChildEnumerator;
            while (children.MoveNext(out var child))
            {
                if (HasComp<ActorComponent>(child)
                    || HasComp<HumanoidAppearanceComponent>(child)
                    || HasComp<BorgChassisComponent>(child)
                    || HasComp<RiderComponent>(child)
                    || HasComp<MechPilotComponent>(child)
                    || TryComp<PullerComponent>(child, out var puller) && puller.Pulling != null
                    || TryComp<MindContainerComponent>(child, out var mind) && mind.HasMind)
                {
                    _reparentWalk.Clear();
                    return true;
                }

                _reparentWalk.Push(child);
            }
        }

        return false;
    }
}
