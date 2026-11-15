using Content.Shared._LuaM.Hearing;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Robust.Shared.Containers;

namespace Content.Server._LuaM.Hearing;

/// <summary>
/// Makes <see cref="DeafTraitComponent"/> owners deaf unless they wear a switched on hearing aid of their kind around the neck.
/// </summary>
public sealed partial class HearingAidSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private DeafnessSystem _deafness = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;

    private const string DeafSource = "DeafTrait";
    private const string Slot = "neck";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeafTraitComponent, ComponentStartup>(OnTraitStartup);
        SubscribeLocalEvent<DeafTraitComponent, ComponentShutdown>(OnTraitShutdown);
        SubscribeLocalEvent<HearingAidComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<HearingAidComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<HearingAidComponent, ItemToggledEvent>(OnToggled);
    }

    private void OnTraitStartup(Entity<DeafTraitComponent> ent, ref ComponentStartup args)
    {
        Refresh(ent);
    }

    private void OnTraitShutdown(Entity<DeafTraitComponent> ent, ref ComponentShutdown args)
    {
        _deafness.RemoveSource(ent, DeafSource);
    }

    private void OnEquipped(Entity<HearingAidComponent> ent, ref GotEquippedEvent args)
    {
        Refresh(args.Equipee);
    }

    private void OnUnequipped(Entity<HearingAidComponent> ent, ref GotUnequippedEvent args)
    {
        Refresh(args.Equipee);
    }

    private void OnToggled(Entity<HearingAidComponent> ent, ref ItemToggledEvent args)
    {
        if (_container.TryGetContainingContainer((ent, null, null), out var container))
            Refresh(container.Owner);
    }

    private void Refresh(EntityUid uid)
    {
        if (!TryComp<DeafTraitComponent>(uid, out var trait))
            return;

        if (HasWorkingAid(uid, trait))
            _deafness.RemoveSource(uid, DeafSource);
        else
            _deafness.AddSource(uid, DeafSource, trait.SoundVolume);
    }

    private bool HasWorkingAid(EntityUid uid, DeafTraitComponent trait)
    {
        return _inventory.TryGetSlotEntity(uid, Slot, out var item)
            && TryComp<HearingAidComponent>(item, out var aid)
            && aid.Kind == trait.AidKind
            && _toggle.IsActivated(item.Value);
    }
}
