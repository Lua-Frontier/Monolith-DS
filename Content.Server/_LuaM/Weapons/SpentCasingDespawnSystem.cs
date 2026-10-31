using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Containers;
using Robust.Shared.Spawners;

namespace Content.Server._LuaM.Weapons;

/// <summary>
/// Spent casings despawn after <see cref="CartridgeAmmoComponent.AutoTimedDespawn"/>, but only outside of guns and magazines.
/// A casing deleted from a chamber or a cylinder jams the gun for good, so the timer stops when the casing is put in
/// and starts when it is taken out.
/// </summary>
public sealed partial class SpentCasingDespawnSystem : EntitySystem
{
    [Dependency] private GunSystem _gun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CartridgeAmmoComponent, EntGotInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<CartridgeAmmoComponent, EntGotRemovedFromContainerMessage>(OnRemoved);
    }

    private void OnInserted(Entity<CartridgeAmmoComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (!ent.Comp.Spent || ent.Comp.AutoTimedDespawn == 0 || !_gun.IsInGunOrMagazine(ent))
            return;

        RemComp<TimedDespawnComponent>(ent);
    }

    private void OnRemoved(Entity<CartridgeAmmoComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        // Moved straight into another gun or magazine, or already counting down.
        if (!ent.Comp.Spent
            || ent.Comp.AutoTimedDespawn == 0
            || TerminatingOrDeleted(ent)
            || _gun.IsInGunOrMagazine(ent)
            || HasComp<TimedDespawnComponent>(ent))
            return;

        AddComp<TimedDespawnComponent>(ent).Lifetime = ent.Comp.AutoTimedDespawn;
    }
}
