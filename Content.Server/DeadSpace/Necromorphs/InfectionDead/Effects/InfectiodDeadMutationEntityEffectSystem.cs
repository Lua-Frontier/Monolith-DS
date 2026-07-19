using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Server.EntityEffects.Effects;

/// <summary>
/// Applies a mutagen's mutation strength to an existing necromorph infection.
/// </summary>
public sealed partial class InfectiodDeadMutation : EntityEffect
{
    [DataField]
    public float MutationStrength;

    [DataField]
    public bool IsStableMutation;

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-mutate-infection-dead", ("chance", Probability));

    public override void Effect(EntityEffectBaseArgs args)
    {
        var necromorf = args.EntityManager.EntitySysManager.GetEntitySystem<
            DeadSpace.Necromorphs.InfectionDead.NecromorfSystem>();
        necromorf.MutateVirus(args.TargetEntity, MutationStrength, IsStableMutation);
    }
}
