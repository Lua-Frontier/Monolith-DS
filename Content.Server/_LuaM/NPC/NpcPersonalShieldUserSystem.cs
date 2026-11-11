using Content.Shared._LuaM.NPC;
using Content.Shared._Mono.PersonalShield;
using Content.Shared.Inventory;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._LuaM.NPC;

/// <summary>
/// Turns on the personal energy shield of NPCs with <see cref="NpcPersonalShieldUserComponent"/>.
/// The shield brings itself back after it breaks once it was turned on, so this only has to flip the toggle.
/// </summary>
public sealed partial class NpcPersonalShieldUserSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<NpcPersonalShieldUserComponent>();
        while (query.MoveNext(out var uid, out var user))
        {
            if (now < user.NextCheck)
                continue;

            user.NextCheck = now + user.CheckInterval;

            // A player who took over the mob decides for themselves.
            if (HasComp<ActorComponent>(uid) || !_mobState.IsAlive(uid))
                continue;

            if (!_inventory.TryGetSlotEntity(uid, user.Slot, out var suit)
                || !TryComp<PersonalShieldComponent>(suit, out var shield)
                || !TryComp<ItemToggleComponent>(suit, out var toggle)
                || toggle.Activated)
                continue;

            // Broken or still recovering: the toggle refuses anyway, and the shield turns itself back on later.
            if (shield.Runtime.Offline > 0f || shield.Runtime.Shatter > 0f)
                continue;

            _toggle.TryActivate((suit.Value, toggle), uid, predicted: false);
        }
    }
}
