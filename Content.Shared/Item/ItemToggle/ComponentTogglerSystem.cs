using Content.Shared.Item.ItemToggle.Components;
using Robust.Shared.Timing; // LuaM

namespace Content.Shared.Item.ItemToggle;

/// <summary>
/// Handles <see cref="ComponentTogglerComponent"/> component manipulation.
/// </summary>
public sealed partial class ComponentTogglerSystem : EntitySystem // LuaM: public sealed class > public sealed partial class
{
    [Dependency] private IGameTiming _timing = default!; // LuaM: predict err fix

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ComponentTogglerComponent, ItemToggledEvent>(OnToggled);
    }

    private void OnToggled(Entity<ComponentTogglerComponent> ent, ref ItemToggledEvent args)
    {
        ToggleComponent(ent, args.Activated);
    }

    // Goobstation - Make this system more flexible
    public void ToggleComponent(EntityUid uid, bool activate)
    {
        if (!_timing.IsFirstTimePredicted) // LuaM: !TryComp<ComponentTogglerComponent>(uid, out var component) > _timing.IsFirstTimePredicted
            return;

//         var target = component.Parent ? Transform(uid).ParentUid : uid; // Commented by LuaM
        if (!TryComp<ComponentTogglerComponent>(uid, out var component)) // LuaM
            return;

        if (activate)
// LuaM-start: add target for the correct remove component, in o.w. - wizden logic
        {
            var target = component.Parent ? Transform(uid).ParentUid : uid;
            if (TerminatingOrDeleted(target))
                return;

            component.Target = target;
// LuaM-end.
            EntityManager.AddComponents(target, component.Components);
        }
        else
//             EntityManager.RemoveComponents(target, component.RemoveComponents ?? component.Components); // Commented by LuaM
// LuaM-start:
        {
            if (component.Target == null)
                return;

            if (TerminatingOrDeleted(component.Target.Value))
                return;

            EntityManager.RemoveComponents(component.Target.Value, component.RemoveComponents ?? component.Components);
        }
 // LuaM-end
    }
}
