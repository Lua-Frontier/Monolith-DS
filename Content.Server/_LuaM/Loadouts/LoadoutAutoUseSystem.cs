using Content.Server.CartridgeLoader;
using Content.Shared.CartridgeLoader;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;

namespace Content.Server._LuaM.Loadouts;

/// <summary>
/// Uses cartridges and implanters picked in the character loadout right after spawning:
/// cartridges get installed into the PDA, implanters get injected into their owner.
/// </summary>
public sealed class LoadoutAutoUseSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private SharedSubdermalImplantSystem _subdermalImplant = default!;

    private const string IdSlot = "id";

    /// <summary>
    /// Uses the given items spawned from the loadout, wherever they ended up (bag or floor if the bag was full).
    /// Items that cannot be used (no PDA, no disk space, duplicate implant) are left where they are.
    /// </summary>
    public void UseLoadoutItems(EntityUid mob, List<EntityUid> loadoutItems)
    {
        if (loadoutItems.Count == 0)
            return;

        var loader = FindCartridgeLoader(mob);

        foreach (var item in loadoutItems)
        {
            if (TerminatingOrDeleted(item) || !TryUse(mob, item, loader))
                continue;

            QueueDel(item);
        }
    }

    private bool TryUse(EntityUid mob, EntityUid item, EntityUid? loader)
    {
        if (HasComp<CartridgeComponent>(item))
            return loader != null && _cartridgeLoader.InstallCartridge(loader.Value, item);

        // Implant straight from the implanter's prototype, the same way job starting implants are given,
        // instead of relying on the implanter's slot being already filled.
        if (TryComp<ImplanterComponent>(item, out var implanter) && implanter.Implant is { } implantId)
        {
            if (HasImplant(mob, implantId))
                return false;

            return _subdermalImplant.AddImplant(mob, implantId) != null;
        }

        return false;
    }

    private bool HasImplant(EntityUid mob, EntProtoId implantId)
    {
        if (!TryComp<ImplantedComponent>(mob, out var implanted))
            return false;

        foreach (var implant in implanted.ImplantContainer.ContainedEntities)
        {
            if (Prototype(implant)?.ID == implantId.Id)
                return true;
        }

        return false;
    }

    private EntityUid? FindCartridgeLoader(EntityUid mob)
    {
        if (_inventory.TryGetSlotEntity(mob, IdSlot, out var id) && HasComp<CartridgeLoaderComponent>(id))
            return id;

        var slots = _inventory.GetSlotEnumerator(mob);
        while (slots.NextItem(out var slotItem))
        {
            if (HasComp<CartridgeLoaderComponent>(slotItem))
                return slotItem;
        }

        return null;
    }
}
