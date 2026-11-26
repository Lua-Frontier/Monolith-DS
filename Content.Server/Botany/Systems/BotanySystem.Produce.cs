using Content.Server.Botany.Components;
using Content.Shared.Botany; // LuaM
using Content.Shared.Chemistry.Components; // LuaM
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;

namespace Content.Server.Botany.Systems;

public sealed partial class BotanySystem
{
    public void ProduceGrown(EntityUid uid, ProduceComponent produce)
    {
        if (!TryGetSeed(produce, out var seed))
            return;

        foreach (var mutation in seed.Mutations)
        {
            if (mutation.AppliesToProduce)
            {
                var args = new EntityEffectBaseArgs(uid, EntityManager);
                mutation.Effect.Effect(args);
            }
        }

        if (!_solutionContainerSystem.EnsureSolution(uid,
                produce.SolutionName,
                out var solutionContainer,
                FixedPoint2.Zero))
            return;

        solutionContainer.RemoveAllSolution();
        foreach (var (chem, quantity) in seed.Chemicals)
        {
            var amount = FixedPoint2.New(quantity.Min);
            if (quantity.PotencyDivisor > 0 && seed.Potency > 0)
                amount += FixedPoint2.New(seed.Potency / quantity.PotencyDivisor);
            amount = FixedPoint2.New(MathHelper.Clamp(amount.Float(), quantity.Min, quantity.Max));
            solutionContainer.MaxVolume += amount;
            solutionContainer.AddReagent(chem, amount);
        }
    }

    // LuaM-start: produce stacks
    /// <summary>
    /// Makes a unit split off a produce stack the same as the stack: same seed, mutations, contents and name.
    /// </summary>
    public void CopyProduce(EntityUid source, EntityUid target)
    {
        if (!TryComp<ProduceComponent>(source, out var sourceProduce))
            return;

        var produce = EnsureComp<ProduceComponent>(target);
        produce.Seed = sourceProduce.Seed;
        produce.SeedId = sourceProduce.SeedId;
        ProduceGrown(target, produce);

        if (sourceProduce.Seed != null)
            _appearance.SetData(target, ProduceVisuals.Potency, sourceProduce.Seed.Potency);

        if (_solutionContainerSystem.TryGetSolution(source, sourceProduce.SolutionName, out _, out var sourceSolution)
            && _solutionContainerSystem.TryGetSolution(target, produce.SolutionName, out var targetSolution, out _))
        {
            _solutionContainerSystem.RemoveAllSolution(targetSolution.Value);
            _solutionContainerSystem.SetCapacity(targetSolution.Value, sourceSolution.MaxVolume);
            _solutionContainerSystem.TryAddSolution(targetSolution.Value, new Solution(sourceSolution));
        }

        var meta = MetaData(source);
        _metaData.SetEntityName(target, meta.EntityName);
        _metaData.SetEntityDescription(target, meta.EntityDescription);
    }
    // LuaM-end
}
