using Content.Server.Body.Systems;
using Content.Shared._LuaM.Clothing;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._LuaM.Clothing;

public sealed class CritInjectorClothingSystem : EntitySystem
{
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Critical)
            return;

        var user = args.Target;
        if (!_inventory.TryGetContainerSlotEnumerator(user, out var slots, SlotFlags.WITHOUT_POCKET))
            return;

        while (slots.MoveNext(out var slot))
        {
            if (slot.ContainedEntity is not { } item || !TryComp<CritInjectorClothingComponent>(item, out var injector))
                continue;

            if (_timing.CurTime < injector.NextInject)
                continue;

            if (!_bloodstream.TryAddToChemicals(user, injector.Solution.Clone()))
                continue;

            injector.NextInject = _timing.CurTime + injector.Cooldown;
            _audio.PlayPvs(injector.InjectSound, user);
            _popup.PopupEntity(Loc.GetString("crit-injector-clothing-used"), user, user);
        }
    }
}
