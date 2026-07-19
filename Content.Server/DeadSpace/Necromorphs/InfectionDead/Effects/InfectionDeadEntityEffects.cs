using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DeadSpace.Necromorphs.InfectionDead.Components;
using Content.Shared.DeadSpace.Necromorphs.InfectionDead.Prototypes;
using Content.Shared.DeadSpace.Necromorphs.Sanity;
using Content.Shared.DeadSpace.Necromorphs.Unitology.Components;
using Content.Shared.EntityEffects;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Content.Shared.Zombies;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.EntityEffects.Effects;

public sealed partial class CauseInfectionDead : EntityEffect
{
    [DataField]
    public InfectionDeadStrainData StrainData = new();

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-cause-infection-dead", ("chance", Probability));

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (!args.EntityManager.HasComponent<MobStateComponent>(args.TargetEntity) ||
            args.EntityManager.HasComponent<InfectionDeadComponent>(args.TargetEntity) ||
            args.EntityManager.HasComponent<ImmunitetInfectionDeadComponent>(args.TargetEntity))
            return;

        args.EntityManager.AddComponent(args.TargetEntity, new InfectionDeadComponent(StrainData));
    }
}

public sealed partial class CureInfectionDead : EntityEffect
{
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-cure-infection-dead", ("chance", Probability));

    public override void Effect(EntityEffectBaseArgs args)
    {
        args.EntityManager.RemoveComponent<InfectionDeadComponent>(args.TargetEntity);
    }
}

public sealed partial class NecromorphMutagen : EntityEffect
{
    [DataField]
    public bool IsAnimal;

    [DataField(required: true, customTypeSerializer: typeof(PrototypeIdSerializer<NecromorfPrototype>))]
    public string? NecroPrototype { get; set; }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-necromorph-mutagen", ("chance", Probability));

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (IsAnimal && args.EntityManager.HasComponent<HumanoidAppearanceComponent>(args.TargetEntity))
            return;

        var component = args.EntityManager.EnsureComponent<NecromorfAfterInfectionComponent>(args.TargetEntity);
        component.NecroPrototype = NecroPrototype;
    }
}

public sealed partial class CauseEnslavedUnitology : EntityEffect
{
    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-cause-enslave", ("chance", Probability));

    public override void Effect(EntityEffectBaseArgs args)
    {
        var target = args.TargetEntity;
        var entities = args.EntityManager;

        if (!entities.HasComponent<MobStateComponent>(target) ||
            !entities.HasComponent<HumanoidAppearanceComponent>(target))
            return;

        if (entities.HasComponent<ImmunitetInfectionDeadComponent>(target))
        {
            var damage = new DamageSpecifier();
            damage.DamageDict.Add("Cellular", 5f);
            entities.System<DamageableSystem>().TryChangeDamage(target, damage, true, false);
            return;
        }

        if (entities.HasComponent<UnitologyComponent>(target) ||
            entities.HasComponent<UnitologyEnslavedComponent>(target) ||
            entities.HasComponent<NecromorfComponent>(target) ||
            entities.HasComponent<ZombieComponent>(target) ||
            !entities.HasComponent<SanityComponent>(target))
            return;

        entities.RemoveComponent<InfectionDeadComponent>(target);
        entities.EnsureComponent<UnitologyEnslavedComponent>(target);
    }
}
