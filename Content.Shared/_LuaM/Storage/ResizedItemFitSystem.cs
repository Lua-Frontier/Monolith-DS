using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.Storage;

/// <summary>
/// Items that grow when toggled on (energy and hardlight shields, e-swords, telescopic shields) used to stay
/// inside a bag or pocket they no longer fit in and broke the storage grid. After the size change the item is moved
/// to a free spot of the same storage, and if there is none it drops to the floor.
/// </summary>
public sealed partial class ResizedItemFitSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly ProtoId<ItemSizePrototype> PocketableItemSize = "Small";

    public override void Initialize()
    {
        base.Initialize();

        // ItemToggleSizeComponent is already subscribed by the item system, which must run first to apply the new size
        SubscribeLocalEvent<ItemComponent, ItemToggledEvent>(OnToggled, after: [typeof(SharedItemSystem)]);
    }

    private void OnToggled(Entity<ItemComponent> ent, ref ItemToggledEvent args)
    {
        if (!args.Activated || !HasComp<ItemToggleSizeComponent>(ent))
            return;

        var item = ent.Comp;

        if (!_container.TryGetContainingContainer(ent.Owner, out var container))
            return;

        if (TryComp<StorageComponent>(container.Owner, out var storage) && storage.Container == container)
        {
            if (FitsInStorage((ent, item), (container.Owner, storage)))
                return;

            Drop(ent, container);
            return;
        }

        if (_inventory.TryGetContainingSlot(ent.Owner, out var slot) &&
            slot.SlotFlags.HasFlag(SlotFlags.POCKET) &&
            _item.GetSizePrototype(item.Size) > _item.GetSizePrototype(PocketableItemSize))
        {
            Drop(ent, container);
        }
    }

    private bool FitsInStorage(Entity<ItemComponent> item, Entity<StorageComponent> storage)
    {
        if (_item.GetSizePrototype(item.Comp.Size) > _storage.GetMaxItemSize((storage, storage.Comp)))
            return false;

        if (storage.Comp.StoredItems.TryGetValue(item, out var location) &&
            _storage.ItemFitsInGridLocation((item, item.Comp), (storage, storage.Comp), location))
            return true;

        // try to find another free spot in the same storage
        return _storage.TryGetAvailableGridSpace((storage, storage.Comp), (item, item.Comp), out var newLocation) &&
               _storage.TrySetItemStorageLocation((item, item.Comp), (storage, storage.Comp), newLocation.Value);
    }

    private void Drop(EntityUid uid, BaseContainer container)
    {
        var coordinates = _transform.GetMoverCoordinates(container.Owner);
        _container.Remove(uid, container, destination: coordinates);
    }
}
