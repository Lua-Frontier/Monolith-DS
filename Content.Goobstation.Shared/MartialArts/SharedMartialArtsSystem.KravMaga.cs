// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.MartialArts;
using Content.Goobstation.Shared.MartialArts.Components;
using Content.Goobstation.Shared.MartialArts.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Speech.Muting;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Goobstation.Shared.MartialArts;

public abstract partial class SharedMartialArtsSystem
{
    private const string BlockedBreathingKey = "KravMagaBlockedBreathing";

    private void InitializeKravMaga()
    {
        SubscribeLocalEvent<KravMagaComponent, MapInitEvent>(OnKravMagaMapInit);
        SubscribeLocalEvent<KravMagaComponent, ComponentShutdown>(OnKravMagaShutdown);
        SubscribeLocalEvent<KravMagaComponent, KravMagaActionEvent>(OnKravMagaAction);
        SubscribeLocalEvent<KravMagaComponent, MeleeHitEvent>(OnKravMagaMeleeHit);
    }

    private void OnKravMagaMapInit(Entity<KravMagaComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient || HasComp<MartialArtsKnowledgeComponent>(ent))
            return;

        foreach (var actionId in ent.Comp.BaseKravMagaMoves)
        {
            if (_actions.AddAction(ent.Owner, actionId) is { } action)
                ent.Comp.KravMagaMoveEntities.Add(action);
        }
    }

    private void OnKravMagaShutdown(Entity<KravMagaComponent> ent, ref ComponentShutdown args)
    {
        if (_net.IsClient)
            return;

        foreach (var action in ent.Comp.KravMagaMoveEntities)
        {
            _actions.RemoveAction(ent.Owner, action);
        }

        ent.Comp.KravMagaMoveEntities.Clear();
    }

    private void OnKravMagaAction(Entity<KravMagaComponent> ent, ref KravMagaActionEvent args)
    {
        var actionEnt = args.Action.Owner;
        if (args.Handled || !TryComp<KravMagaActionComponent>(actionEnt, out var kravAction))
            return;

        args.Handled = true;

        ent.Comp.SelectedMove = kravAction.Configuration;
        ent.Comp.SelectedMoveAction = actionEnt;
        _popup.PopupClient(Loc.GetString("krav-maga-ready", ("action", Loc.GetString(kravAction.Name))), ent, ent);
    }

    /// <summary>
    /// The component is on the user, so only unarmed hits get here.
    /// </summary>
    private void OnKravMagaMeleeHit(Entity<KravMagaComponent> ent, ref MeleeHitEvent args)
    {
        if (args.Handled || args.HitEntities.Count == 0)
            return;

        foreach (var hit in args.HitEntities)
        {
            if (hit == ent.Owner || !HasComp<MobStateComponent>(hit))
                continue;

            DoKravMaga(ent, hit);
            // The move is used up by the first hit
            if (ent.Comp.SelectedMove == null)
                break;
        }
    }

    private void DoKravMaga(Entity<KravMagaComponent> ent, EntityUid target)
    {
        var move = ent.Comp.SelectedMove;
        var actionEnt = ent.Comp.SelectedMoveAction;
        ent.Comp.SelectedMove = null;
        ent.Comp.SelectedMoveAction = null;

        if (_net.IsClient)
            return;

        if (move == null || !TryComp<KravMagaActionComponent>(actionEnt, out var action))
        {
            var damage = ent.Comp.BaseDamage;
            if (_standing.IsDown(target))
                damage *= ent.Comp.DownedDamageModifier;

            DoDamage(ent, target, "Blunt", damage);
            return;
        }

        switch (move.Value)
        {
            case KravMagaMoves.LegSweep:
                if (!_standing.IsDown(target))
                    _stun.TryKnockdown(target, ent.Comp.LegSweepKnockdownTime, true);
                break;
            case KravMagaMoves.NeckChop:
                _status.TryAddStatusEffect<MutedComponent>(target, "Muted", action.EffectTime, true);
                break;
            case KravMagaMoves.LungPunch:
                _stamina.TakeStaminaDamage(target, action.StaminaDamage, source: ent);
                _status.TryAddStatusEffect<KravMagaBlockedBreathingComponent>(target, BlockedBreathingKey, action.EffectTime, true);
                break;
        }

        ComboPopup(ent, target, action.Name);
    }
}
