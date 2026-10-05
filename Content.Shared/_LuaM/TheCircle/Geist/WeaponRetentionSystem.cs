// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions.Events;
using Content.Shared.Standing;

namespace Content.Shared._LuaM.TheCircle.Geist;

public sealed class WeaponRetentionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WeaponRetentionComponent, DropHandItemsEvent>(OnDropHandItems);
        SubscribeLocalEvent<WeaponRetentionComponent, DisarmAttemptEvent>(OnDisarmAttempt);
    }

    private void OnDropHandItems(Entity<WeaponRetentionComponent> ent, ref DropHandItemsEvent args)
    {
        args.Cancelled = true;
    }

    private void OnDisarmAttempt(Entity<WeaponRetentionComponent> ent, ref DisarmAttemptEvent args)
    {
        args.Cancel();
    }
}
