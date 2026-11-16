using Content.Client.Weapons.Ranged.Components; // LuaM
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Map;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    protected override void InitializeBallistic()
    {
        base.InitializeBallistic();
        SubscribeLocalEvent<BallisticAmmoProviderComponent, UpdateAmmoCounterEvent>(OnBallisticAmmoCount);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, AfterAutoHandleStateEvent>(OnBallisticAfterState); // LuaM
    }

    // LuaM-start
    private readonly HashSet<EntityUid> _ammoCounterRefresh = new();

    private void OnBallisticAfterState(EntityUid uid, BallisticAmmoProviderComponent component, ref AfterAutoHandleStateEvent args)
    {
        if (HasComp<AmmoCounterComponent>(uid))
            _ammoCounterRefresh.Add(uid);

        if (Containers.TryGetContainingContainer((uid, null, null), out var container) && HasComp<AmmoCounterComponent>(container.Owner))
            _ammoCounterRefresh.Add(container.Owner);
    }

    private void RefreshAmmoCounters()
    {
        if (_ammoCounterRefresh.Count == 0)
            return;

        foreach (var uid in _ammoCounterRefresh)
        {
            if (TryComp<AmmoCounterComponent>(uid, out var counter))
                UpdateAmmoCount(uid, counter);
        }

        _ammoCounterRefresh.Clear();
    }
    // LuaM-end

    private void OnBallisticAmmoCount(EntityUid uid, BallisticAmmoProviderComponent component, UpdateAmmoCounterEvent args)
    {
        if (args.Control is DefaultStatusControl control)
        {
            control.Update(GetBallisticShots(component), component.Capacity);
        }
    }

    protected override void Cycle(EntityUid uid, BallisticAmmoProviderComponent component, MapCoordinates coordinates)
    {
        if (!Timing.IsFirstTimePredicted)
            return;

        EntityUid? ent = null;

        // TODO: Combine with TakeAmmo
        if (component.Entities.Count > 0)
        {
            var index = component.FireInLoadOrder ? 0 : component.Entities.Count - 1;
            var existing = component.Entities[index];
            component.Entities.RemoveAt(index);

            Containers.Remove(existing, component.Container);
            EnsureShootable(existing);
        }
        else if (component.UnspawnedCount > 0 && !component.InfiniteUnspawned)
        {
            component.UnspawnedCount--;
            ent = Spawn(component.Proto, coordinates);
            EnsureShootable(ent.Value);
        }

        if (ent != null && IsClientSide(ent.Value))
            Del(ent.Value);

        var cycledEvent = new GunCycledEvent();
        RaiseLocalEvent(uid, ref cycledEvent);
    }
}
