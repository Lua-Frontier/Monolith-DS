using System.Numerics;
using Content.Server._LuaM.Procedural.GridAssembly;
using Robust.Shared.Map;

namespace Content.Server._LuaM.TheCircle.FrontierWar;

public sealed partial class CircleFrontierWarSystem
{
    [Dependency] private GridAssemblySystem _gridAssembly = default!;

    private bool TryBeginBattlefieldGeneration(EntityUid ruleUid, CircleFrontierWarComponent rule)
    {
        if (rule.BattlefieldGenerationStarted || rule.BattlefieldReady)
            return true;

        var circleAnchors = EntityQueryEnumerator<CircleFrontierWarCircleAnchorComponent, TransformComponent>();
        if (!circleAnchors.MoveNext(out _, out _, out var sectorAnchor))
        {
            Log.Error("Circle Frontier War cannot generate its vgroid: no CircleFrontierWarCircleAnchor exists.");
            return false;
        }

        rule.BattlefieldGenerationStarted = true;
        Dirty(ruleUid, rule);

        GenerateBattlefieldAsync(ruleUid, rule, sectorAnchor);
        return true;
    }

    private async void GenerateBattlefieldAsync(
        EntityUid ruleUid,
        CircleFrontierWarComponent rule,
        TransformComponent sectorAnchor)
    {
        // The entry terminal remains in the sector. It transfers accepted players to the generated assembly.
        var gates = EntityQueryEnumerator<CircleFrontierWarGateComponent>();
        if (!gates.MoveNext(out _, out _))
            Spawn("CircleFrontierWarGate", sectorAnchor.Coordinates.Offset(new Vector2(2f, 0f)));

        try
        {
            var anchor = new MapCoordinates(_transform.GetWorldPosition(sectorAnchor), sectorAnchor.MapID);
            var result = await _gridAssembly.GenerateAsync(rule.BattlefieldAssembly, anchor, RobustRandom.Next());

            if (Deleted(ruleUid))
            {
                if (!Deleted(result.Grid.Owner))
                    QueueDel(result.Grid.Owner);

                return;
            }

            rule.BattlefieldGrid = result.Grid.Owner;
            rule.MergedModuleCount = result.ModuleOffsets.Count;
            rule.BattlefieldGenerationStarted = false;
            rule.BattlefieldReady = true;
            rule.NextStageTime = Timing.CurTime + rule.BattlefieldPreparationTime;
            Dirty(ruleUid, rule);

            Log.Info($"Circle Frontier War received grid assembly {result.Grid.Owner} with {rule.MergedModuleCount} merged modules.");
        }
        catch (Exception exception)
        {
            Log.Error($"Circle Frontier War grid assembly generation failed: {exception}");
            if (Deleted(ruleUid))
                return;

            rule.BattlefieldGrid = null;
            rule.BattlefieldGenerationStarted = false;
            rule.BattlefieldReady = false;
            rule.MergedModuleCount = 0;
            rule.Stage = CircleFrontierWarStage.Infiltration;
            rule.NextStageTime = Timing.CurTime + TimeSpan.FromMinutes(1);
            Dirty(ruleUid, rule);
        }
    }
}
