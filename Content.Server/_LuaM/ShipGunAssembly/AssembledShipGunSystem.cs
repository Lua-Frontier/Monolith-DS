using Content.Server.Cargo.Systems;
using Content.Server.Construction;

namespace Content.Server._LuaM.ShipGunAssembly;

public sealed class AssembledShipGunSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AssembledShipGunComponent, ConstructionChangeEntityEvent>(OnChangeEntity);
        SubscribeLocalEvent<AssembledShipGunComponent, PriceCalculationEvent>(OnPriceCalculation);
    }

    private void OnChangeEntity(EntityUid uid, AssembledShipGunComponent comp, ConstructionChangeEntityEvent args)
    {
        if (args.New == uid)
            return;

        EnsureComp<AssembledShipGunComponent>(args.New).Price = comp.Price;
    }

    private void OnPriceCalculation(Entity<AssembledShipGunComponent> ent, ref PriceCalculationEvent args)
    {
        if (args.Handled)
            return;

        args.Price += ent.Comp.Price;
        args.Handled = true;
    }
}
