using System.Linq;
using Content.Shared._Goobstation.Wires.Components;
using Content.Shared._Goobstation.Wires.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Item;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Shared._LuaM.Clothing.ModsuitStorage;

public sealed class ModsuitStorageUpgradeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;

    private EntityQuery<ItemComponent> _itemQuery;

    public override void Initialize()
    {
        base.Initialize();

        _itemQuery = GetEntityQuery<ItemComponent>();

        SubscribeLocalEvent<ModsuitStorageUpgradeComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ModsuitStorageUpgradeComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ModsuitStorageUpgradeComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<ModsuitStorageUpgradeComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<ModsuitStorageUpgradeComponent, EntRemovedFromContainerMessage>(OnRemoved);
        // RequirePanelSystem overwrites Cancelled, so this has to run after it
        SubscribeLocalEvent<ModsuitStorageUpgradeComponent, ItemSlotEjectAttemptEvent>(OnEjectAttempt,
            after: new[] { typeof(RequirePanelSystem) });
        SubscribeLocalEvent<ModsuitStorageUpgradeComponent, ExaminedEvent>(OnExamined);
    }

    private void OnInit(Entity<ModsuitStorageUpgradeComponent> ent, ref ComponentInit args)
    {
        _itemSlots.AddItemSlot(ent, ModsuitStorageUpgradeComponent.SlotId, ent.Comp.ModuleSlot);

        // the module slot is only reachable with the panel open, like the power cell
        if (TryComp<ItemSlotsRequirePanelComponent>(ent, out var panel))
            panel.Slots.TryAdd(ModsuitStorageUpgradeComponent.SlotId, true);
    }

    private void OnStartup(Entity<ModsuitStorageUpgradeComponent> ent, ref ComponentStartup args)
    {
        UpdateGrid(ent);
    }

    private void OnRemove(Entity<ModsuitStorageUpgradeComponent> ent, ref ComponentRemove args)
    {
        _itemSlots.RemoveItemSlot(ent, ent.Comp.ModuleSlot);
    }

    private void OnInserted(Entity<ModsuitStorageUpgradeComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == ModsuitStorageUpgradeComponent.SlotId)
            UpdateGrid(ent);
    }

    private void OnRemoved(Entity<ModsuitStorageUpgradeComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == ModsuitStorageUpgradeComponent.SlotId)
            UpdateGrid(ent);
    }

    private void OnEjectAttempt(Entity<ModsuitStorageUpgradeComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        if (args.Cancelled || args.Slot.ID != ModsuitStorageUpgradeComponent.SlotId)
            return;

        // items lying outside of the base grid would be lost from the storage
        if (TryComp<StorageComponent>(ent, out var storage) && !ItemsFitGrid(storage, ent.Comp.BaseGrid))
            args.Cancelled = true;
    }

    private void OnExamined(Entity<ModsuitStorageUpgradeComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (ent.Comp.ModuleSlot.Item is { } module)
            args.PushMarkup(Loc.GetString("modsuit-storage-module-examine-installed", ("module", Name(module))));
        else
            args.PushMarkup(Loc.GetString("modsuit-storage-module-examine-empty"));
    }

    private void UpdateGrid(Entity<ModsuitStorageUpgradeComponent> ent)
    {
        if (!TryComp<StorageComponent>(ent, out var storage))
            return;

        var grid = ent.Comp.ModuleSlot.Item is { } module
            && TryComp<ModsuitStorageModuleComponent>(module, out var moduleComp)
            ? moduleComp.Grid
            : ent.Comp.BaseGrid;

        if (storage.Grid.SequenceEqual(grid))
            return;

        storage.Grid.Clear();
        storage.Grid.AddRange(grid);

        if (!_timing.ApplyingState)
            Dirty(ent, storage);

        _storage.UpdateUI((ent.Owner, storage));
    }

    private bool ItemsFitGrid(StorageComponent storage, List<Box2i> grid)
    {
        foreach (var (item, location) in storage.StoredItems)
        {
            if (!_itemQuery.TryComp(item, out var itemComp))
                continue;

            foreach (var box in _item.GetAdjustedItemShape((item, itemComp), location))
            {
                for (var y = box.Bottom; y <= box.Top; y++)
                {
                    for (var x = box.Left; x <= box.Right; x++)
                    {
                        var cell = new Vector2i(x, y);
                        if (!grid.Any(gridBox => gridBox.Contains(cell)))
                            return false;
                    }
                }
            }
        }

        return true;
    }
}
