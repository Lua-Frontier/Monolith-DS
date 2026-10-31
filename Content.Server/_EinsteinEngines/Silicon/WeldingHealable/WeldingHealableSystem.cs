using Content.Server._EinsteinEngines.Silicon.WeldingHealing;
using Content.Shared.Tools.Components;
using Content.Shared._EinsteinEngines.Silicon.WeldingHealing;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body.Systems;
using SharedToolSystem = Content.Shared.Tools.Systems.SharedToolSystem;
using Content.Shared.Interaction.Events; // LuaM

namespace Content.Server._EinsteinEngines.Silicon.WeldingHealable;

public sealed partial class WeldingHealableSystem : SharedWeldingHealableSystem
{
    [Dependency] private SharedToolSystem _toolSystem = default!;
    [Dependency] private DamageableSystem _damageableSystem = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private SharedBodySystem _bodySystem = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<WeldingHealableComponent, InteractUsingEvent>(Repair);
        SubscribeLocalEvent<WeldingHealableComponent, SiliconRepairFinishedEvent>(OnRepairFinished);
        SubscribeLocalEvent<WeldingHealingComponent, UseInHandEvent>(OnUseInHand); //LuaM
    }

    private void OnRepairFinished(EntityUid uid, WeldingHealableComponent healableComponent, SiliconRepairFinishedEvent args)
    {
        if (args.Cancelled || args.Used == null
            || !TryComp<DamageableComponent>(args.Target, out var damageable)
            || !TryComp<WeldingHealingComponent>(args.Used, out var component)
            || damageable.DamageContainerID is null
            || !component.DamageContainers.Contains(damageable.DamageContainerID)
            || !HasDamage((args.Target.Value, damageable), component, args.User)
            || !TryComp<WelderComponent>(args.Used, out var welder)
            || !TryComp<SolutionContainerManagerComponent>(args.Used, out var solutionContainer)
            || !_solutionContainer.TryGetSolution(((EntityUid) args.Used, solutionContainer), welder.FuelSolutionName, out var solution))
            return;

        _damageableSystem.TryChangeDamage(uid, component.Damage, true, false, origin: args.User);

        _solutionContainer.RemoveReagent(solution.Value, welder.FuelReagent, component.FuelCost);

        var str = Loc.GetString("comp-repairable-repair",
            ("target", uid),
            ("tool", args.Used!));
        _popup.PopupEntity(str, uid, args.User);

        if (!args.Used.HasValue
            || _toolSystem.GetWelderFuelAndCapacity(args.Used.Value).fuel < component.FuelCost //Mono: Nanite applicator
            || !HasDamage((args.Target.Value, damageable), component, args.User)) //LuaM HasDamage check 
            return;

            args.Handled = _toolSystem.UseTool
                (args.Used.Value,
                args.User,
                uid,
                args.Delay,
                component.QualityNeeded,
                new SiliconRepairFinishedEvent
                {
                    Delay = args.Delay
                },
                breakOnMove: component.BreakOnMove, // LuaM 
                breakOnDamage: component.BreakOnDamage); //LuaM dedefault value
    }
    private async void Repair(EntityUid uid, WeldingHealableComponent healableComponent, InteractUsingEvent args)
    {
        if (args.Handled
            || !TryComp(args.Used, out WeldingHealingComponent? component)
            || !TryComp(args.Target, out DamageableComponent? damageable)
            || damageable.DamageContainerID is null
            || !component.DamageContainers.Contains(damageable.DamageContainerID)
            || !HasDamage((args.Target, damageable), component, args.User)
            || !_toolSystem.HasQuality(args.Used, component.QualityNeeded)
            || args.User == args.Target && !component.AllowSelfHeal
            || _toolSystem.GetWelderFuelAndCapacity(args.Used).fuel < component.FuelCost) //Mono: Nanite applicator again
            return;

        float delay = args.User == args.Target
            ? component.DoAfterDelay * component.SelfHealPenalty 
            : component.DoAfterDelay ;

        args.Handled = _toolSystem.UseTool
            (args.Used,
            args.User,
            args.Target,
            delay,
            component.QualityNeeded,
            new SiliconRepairFinishedEvent
            {
                Delay = delay,
            },
            breakOnMove: component.BreakOnMove, // LuaM default value
            breakOnDamage: component.BreakOnDamage); // LuaM default value
    }
    // LuaM self-heal with Use in Hand
    //LuaM-start:
    private void OnUseInHand(Entity<WeldingHealingComponent> ent, ref UseInHandEvent args) 
    {
        var component = ent.Comp;

        if (args.Handled
            || !TryComp(args.User, out DamageableComponent? damageable)
            || damageable.DamageContainerID is null
            || !component.DamageContainers.Contains(damageable.DamageContainerID)
            || !HasDamage((args.User, damageable), component, args.User)
            || !_toolSystem.HasQuality(ent.Owner, component.QualityNeeded)
            || !component.AllowSelfHeal
            || _toolSystem.GetWelderFuelAndCapacity(ent.Owner).fuel < component.FuelCost)
            return;

        float delay = component.DoAfterDelay * component.SelfHealPenalty;

        args.Handled = _toolSystem.UseTool(
            ent.Owner,
            args.User,
            args.User,
            delay,
            component.QualityNeeded,
            new SiliconRepairFinishedEvent
            {
                Delay = delay,
            },
            breakOnMove: component.BreakOnMove,
            breakOnDamage: component.BreakOnDamage);
    }
    //LuaM-end

    private bool HasDamage(Entity<DamageableComponent> damageable, WeldingHealingComponent healable, EntityUid user)
    {
        if (healable.Damage.DamageDict is null)
            return false;

        foreach (var type in healable.Damage.DamageDict)
            // if (damageable.Comp.Damage.DamageDict[type.Key].Value > 0) // Commented by LuaM  

            if (damageable.Comp.Damage.DamageDict.TryGetValue(type.Key, out var damage) && // LuaM: Safely handle missing damage types.
                damage.Value > 0) // LuaM  
            {
                return true;
            }

        // In case the healer is a humanoid entity with targeting, we run the check on the targeted parts.
        if (!TryComp(user, out TargetingComponent? targeting))
            return false;

        var (targetType, targetSymmetry) = _bodySystem.ConvertTargetBodyPart(targeting.Target);
        foreach (var part in _bodySystem.GetBodyChildrenOfType(damageable, targetType, symmetry: targetSymmetry))
            if (TryComp<DamageableComponent>(part.Id, out var damageablePart))
                foreach (var type in healable.Damage.DamageDict)
                    // if (damageablePart.Damage.DamageDict[type.Key].Value > 0) // Commented by LuaM  
                    if (damageablePart.Damage.DamageDict.TryGetValue(type.Key, out var damage) // LuaM: Safely handle missing damage types.
                    && damage.Value > 0) // LuaM  
                    {
                        return true;
                    }

        return false;
    }
}
