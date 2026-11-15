using System.Linq;
using Content.Shared.Clothing.Components;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Inventory;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.Announcements;

/// <summary>Copies visible appearance without changing the speaker or reading carried storage.</summary>
public sealed partial class AnnouncementPortraitCaptureSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public AnnouncementPortraitSnapshot? Capture(EntityUid? speaker)
    {
        if (speaker is not { } uid || !Exists(uid) ||
            !TryComp<HumanoidAppearanceComponent>(uid, out var appearance))
            return null;

        var snapshot = new AnnouncementPortraitSnapshot
        {
            Name = Name(uid),
            Prototype = MetaData(uid).EntityPrototype?.ID ?? string.Empty,
            Species = appearance.Species.Id,
            Sex = appearance.Sex,
            Gender = appearance.Gender,
            Age = appearance.Age,
            SkinColor = appearance.SkinColor,
            EyeColor = appearance.EyeColor,
            Height = appearance.Height,
            Width = appearance.Width,
            Markings = new MarkingSet(appearance.MarkingSet),
            CustomBaseLayers = new(appearance.CustomBaseLayers),
            PermanentlyHidden = new(appearance.PermanentlyHidden),
        };

        if (!_inventory.TryGetSlots(uid, out var slots))
            return snapshot;

        foreach (var slot in slots)
        {
            // Pockets, suit storage and hidden utility slots are not worn visual clothing.
            if ((slot.SlotFlags & (SlotFlags.POCKET | SlotFlags.SUITSTORAGE | SlotFlags.WALLET | SlotFlags.PREVENTEQUIP)) != 0 ||
                !_inventory.TryGetSlotEntity(uid, slot.Name, out var item))
                continue;

            EntityUid? visualItem = item;
            if ((slot.SlotFlags & SlotFlags.HEAD) != 0 && IsSuitHelmet(item.Value))
            {
                visualItem = null;
                if (TryComp<AttachedClothingComponent>(item, out var attached) &&
                    HasComp<ToggleableClothingComponent>(attached.AttachedUid) &&
                    slots.Any(s => _inventory.TryGetSlotEntity(uid, s.Name, out var parent) && parent == attached.AttachedUid))
                {
                    // Read the original headwear saved by ToggleableClothingSystem; never toggle the real suit.
                    var replaced = attached.ClothingContainer;
                    if (replaced == null && _containers.TryGetContainer(item.Value, attached.ClothingContainerId, out var container))
                        replaced = container as ContainerSlot;
                    visualItem = replaced?.ContainedEntity;
                }
            }

            if (visualItem is not { } clothingUid ||
                !TryComp<ClothingComponent>(clothingUid, out var clothing) ||
                (clothing.Slots & slot.SlotFlags) == 0 ||
                MetaData(clothingUid).EntityPrototype is not { } prototype)
                continue;

            snapshot.Equipment.Add(new AnnouncementPortraitItem
            {
                Slot = slot.Name,
                Prototype = prototype.ID,
                EquippedPrefix = clothing.EquippedPrefix,
            });
        }

        return snapshot;
    }

    private bool IsSuitHelmet(EntityUid uid)
    {
        if (MetaData(uid).EntityPrototype is not { } prototype)
            return false;
        return _prototypes.EnumerateAllParents<EntityPrototype>(prototype.ID, includeSelf: true)
            .Any(p => p.id is "ClothingHeadHardsuitBase" or "ClothingHeadEVAHelmetBase" or "BaseClothingModsuitHelmet");
    }
}
