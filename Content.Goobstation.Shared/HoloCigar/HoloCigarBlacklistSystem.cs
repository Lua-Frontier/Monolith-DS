using Content.Goobstation.Shared.HoloCigar;
using Content.Goobstation.Common.TheManWhoSoldTheWorld;
using Content.Goobstation.Common.Weapons.Multishot;

namespace Content.Goobstation.Shared.HoloCigar;

public sealed class HoloCigarBlacklistSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HoloCigarBlacklistComponent, ComponentAdd>(OnBlacklistAdded);
        SubscribeLocalEvent<HoloCigarBlacklistComponent, ComponentInit>(OnBlacklistInit);
        SubscribeLocalEvent<HoloCigarAffectedGunComponent, ComponentAdd>(OnAffectedGunAdded);
    }

    private void OnBlacklistAdded(Entity<HoloCigarBlacklistComponent> ent, ref ComponentAdd args)
    {
        RemoveHoloCigarEffects(ent);
    }

    private void OnBlacklistInit(Entity<HoloCigarBlacklistComponent> ent, ref ComponentInit args)
    {
        RemoveHoloCigarEffects(ent);
    }

    private void OnAffectedGunAdded(Entity<HoloCigarAffectedGunComponent> ent, ref ComponentAdd args)
    {
        if (HasComp<HoloCigarBlacklistComponent>(ent))
        {
            RemoveHoloCigarEffects(ent);
        }
    }

    private void RemoveHoloCigarEffects(EntityUid entity)
    {
        if (HasComp<HoloCigarAffectedGunComponent>(entity))
            RemComp<HoloCigarAffectedGunComponent>(entity);

        if (HasComp<MultishotComponent>(entity))
            RemComp<MultishotComponent>(entity);
    }
}