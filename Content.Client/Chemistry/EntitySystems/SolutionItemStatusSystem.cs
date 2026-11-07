using Content.Client.Chemistry.Components;
using Content.Client.Chemistry.UI;
using Content.Client.Items;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Nutrition.Components; // LuaM

namespace Content.Client.Chemistry.EntitySystems;

/// <summary>
/// Wires up item status logic for <see cref="SolutionItemStatusComponent"/>.
/// </summary>
/// <seealso cref="SolutionStatusControl"/>
public sealed partial class SolutionItemStatusSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutionContainerSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        Subs.ItemStatus<SolutionItemStatusComponent>(
            entity => new SolutionStatusControl(entity, EntityManager, _solutionContainerSystem));
        SubscribeLocalEvent<DrinkComponent, ComponentStartup>(OnDrinkStartup); // LuaM
    }

    // LuaM start
    // -- Бутылки, банки и прочие напитки показывают объём в статусе предмета, как мензурки
    private void OnDrinkStartup(Entity<DrinkComponent> ent, ref ComponentStartup args)
    {
        if (HasComp<SolutionItemStatusComponent>(ent))
            return;

        var status = AddComp<SolutionItemStatusComponent>(ent);
        status.Solution = ent.Comp.Solution;
    }
    // LuaM end
}
