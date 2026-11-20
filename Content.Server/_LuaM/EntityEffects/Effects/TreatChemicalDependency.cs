using Content.Server._LuaM.Traits;
using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Server._LuaM.EntityEffects.Effects;

/// <summary>
/// Holds chemical withdrawal off and cures chemical dependencies. Every metabolized unit brings each dependency closer
/// to a cure by its share of the dependency's treatment amount; the one from the trait takes far more.
/// </summary>
[UsedImplicitly]
public sealed partial class TreatChemicalDependency : EntityEffect
{
    /// <summary>
    /// How long withdrawal stays off after the reagent was last metabolized.
    /// </summary>
    [DataField]
    public TimeSpan Relief = TimeSpan.FromSeconds(30);

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-treat-chemical-dependency", ("chance", Probability));

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args is not EntityEffectReagentArgs reagentArgs)
            return;

        args.EntityManager.System<ChemicalDependencySystem>()
            .Treat(args.TargetEntity, reagentArgs.Quantity, Relief);
    }
}
