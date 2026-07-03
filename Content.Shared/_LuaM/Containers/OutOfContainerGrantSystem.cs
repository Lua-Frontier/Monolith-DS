using Content.Shared._LuaM.Container.Components;
using Robust.Shared.Containers;
using Robust.Shared.Serialization.Manager;

namespace Content.Shared._LuaM.Container.Systems; // idk how name that.

// A simple system that add components to entity if he not inside a container
// Following the example of goob <see cref="Content.Shared._Goobstation.Clothing.Systems.ClothingGrantingSystem"/>
public sealed partial class OutOfContainerGrantSystem : EntitySystem // this name is suck, i know
{
    [Dependency] private readonly ISerializationManager _serializationManager = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OutOfContainerGrantComponent, ComponentStartup>(OnStartup);

        SubscribeLocalEvent<OutOfContainerGrantComponent, EntGotInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<OutOfContainerGrantComponent, EntGotRemovedFromContainerMessage>(OnContainerChanged);
    }

    private void OnStartup(EntityUid uid, OutOfContainerGrantComponent component, ComponentStartup args)
    {
        UpdateComp(uid, component);
    }

    private void OnContainerChanged(EntityUid uid, OutOfContainerGrantComponent component, EntityEventArgs args)
    {
        UpdateComp(uid, component);
    }

    private void UpdateComp(EntityUid uid, OutOfContainerGrantComponent component)
    {
        if (_container.IsEntityInContainer(uid))
            RemoveComp(uid, component);
        else
            AddComp(uid, component);
    }

    private void AddComp(EntityUid uid, OutOfContainerGrantComponent component)
    {
        foreach (var (name, data) in component.Components)
        {
            if (component.Active.TryGetValue(name, out var active) && active)
                continue;

            var newComp = (Component)Factory.GetComponent(name);

            if (HasComp(uid, newComp.GetType()))
            {
                component.Active[name] = true;
                continue;
            }

            object? temp = newComp;
            _serializationManager.CopyTo(data.Component, ref temp);
            EntityManager.AddComponent(uid, (Component)temp!);

            component.Active[name] = true;
        }
    }

    private void RemoveComp(EntityUid uid, OutOfContainerGrantComponent component)
    {
        foreach (var (name, _) in component.Components)
        {
            if (!component.Active.TryGetValue(name, out var active) || !active)
                continue;

            var newComp = (Component)Factory.GetComponent(name);
            RemComp(uid, newComp.GetType());

            component.Active[name] = false;
        }
    }
}
