// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._LuaM.TheCircle.Geist;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Timing;

namespace Content.Server._LuaM.TheCircle.Geist;

public sealed class GeistLethalStrikeSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GeistLethalStrikeComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<GeistLethalStrikeComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnUseInHand(Entity<GeistLethalStrikeComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (ent.Comp.Armed)
        {
            _popup.PopupEntity(Loc.GetString("geist-lethal-strike-already-armed"), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        if (_timing.CurTime < ent.Comp.NextReady)
        {
            var seconds = Math.Ceiling((ent.Comp.NextReady - _timing.CurTime).TotalSeconds);
            _popup.PopupEntity(Loc.GetString("geist-lethal-strike-cooldown", ("seconds", seconds)), args.User, args.User, PopupType.SmallCaution);
            return;
        }

        ent.Comp.Armed = true;
        _popup.PopupEntity(Loc.GetString("geist-lethal-strike-armed"), args.User, args.User, PopupType.MediumCaution);
    }

    private void OnMeleeHit(Entity<GeistLethalStrikeComponent> ent, ref MeleeHitEvent args)
    {
        if (!ent.Comp.Armed || !args.IsHit || args.HitEntities.Count == 0)
            return;

        args.BonusDamage += ent.Comp.Damage;
        ent.Comp.Armed = false;
        ent.Comp.NextReady = _timing.CurTime + ent.Comp.Cooldown;
    }
}
