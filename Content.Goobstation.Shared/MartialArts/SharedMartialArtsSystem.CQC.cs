// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Goobstation.Common.MartialArts;
using Content.Goobstation.Shared.MartialArts.Components;
using Content.Goobstation.Shared.MartialArts.Events;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Bed.Sleep;
using Content.Shared.Damage.Components;

namespace Content.Goobstation.Shared.MartialArts;

public abstract partial class SharedMartialArtsSystem
{
    private const float CqcDisarmStaminaDamage = 25f;
    private static readonly TimeSpan CqcLegSweepTime = TimeSpan.FromSeconds(5);

    private void InitializeCqc()
    {
        SubscribeLocalEvent<CanPerformComboComponent, CqcSlamPerformedEvent>(OnCqcSlam);
        SubscribeLocalEvent<CanPerformComboComponent, CqcKickPerformedEvent>(OnCqcKick);
        SubscribeLocalEvent<CanPerformComboComponent, CqcRestrainPerformedEvent>(OnCqcRestrain);
        SubscribeLocalEvent<CanPerformComboComponent, CqcPressurePerformedEvent>(OnCqcPressure);
        SubscribeLocalEvent<CanPerformComboComponent, CqcConsecutivePerformedEvent>(OnCqcConsecutive);
    }

    private void OnCqcAttackPerformed(EntityUid ent, ComboAttackPerformedEvent args)
    {
        if (args.Weapon != args.Performer || args.Target == args.Performer)
            return;

        switch (args.Type)
        {
            case ComboAttackType.Disarm:
                _stamina.TakeStaminaDamage(args.Target, CqcDisarmStaminaDamage, source: ent);
                break;
            case ComboAttackType.Harm:
                // Leg sweep: get up from the floor while knocking the target down
                if (!_standing.IsDown(ent) || _standing.IsDown(args.Target))
                    break;

                StandUp(ent);
                _stun.TryKnockdown(args.Target, CqcLegSweepTime, true);
                ComboPopup(ent, args.Target, "martial-arts-combo-cqc-leg-sweep");
                break;
        }
    }

    private void OnCqcSlam(Entity<CanPerformComboComponent> ent, ref CqcSlamPerformedEvent args)
    {
        if (!TryStartCombo(ent, out var proto, out var target, out var downed)
            || downed)
            return;

        DoDamage(ent, target, proto.DamageType, proto.ExtraDamage);
        _stun.TryKnockdown(target, proto.ParalyzeTime, true);
        StopPull(target, ent);
        FinishCombo(ent, target, proto, args);
    }

    private void OnCqcKick(Entity<CanPerformComboComponent> ent, ref CqcKickPerformedEvent args)
    {
        if (!TryStartCombo(ent, out var proto, out var target, out var downed))
            return;

        if (downed)
        {
            if (TryComp<StaminaComponent>(target, out var stamina) && stamina.Critical)
                _status.TryAddStatusEffect<ForcedSleepingComponent>(target, "ForcedSleep", args.ForcedSleepTime, false);

            DoDamage(ent, target, proto.DamageType, proto.ExtraDamage, TargetBodyPart.Head);
            _stamina.TakeStaminaDamage(target, proto.StaminaDamage * 2 + 5, source: ent);
        }
        else
        {
            _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);
        }

        StopPull(target, ent);

        var direction = GetDirection(ent, target);
        if (direction.LengthSquared() > 0f)
            ThrowTarget(target, ent, Vector2.Normalize(direction), proto.ThrownSpeed);

        FinishCombo(ent, target, proto, args);
    }

    private void OnCqcRestrain(Entity<CanPerformComboComponent> ent, ref CqcRestrainPerformedEvent args)
    {
        if (!TryStartCombo(ent, out var proto, out var target, out _))
            return;

        _stun.TryKnockdown(target, proto.ParalyzeTime, true);
        _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);
        FinishCombo(ent, target, proto, args);
    }

    private void OnCqcPressure(Entity<CanPerformComboComponent> ent, ref CqcPressurePerformedEvent args)
    {
        if (!TryStartCombo(ent, out var proto, out var target, out _))
            return;

        _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);
        FinishCombo(ent, target, proto, args);

        // Take the weapon away
        if (!_hands.TryGetActiveItem(target, out var item)
            || !_hands.TryDrop(target, item.Value)
            || !_hands.TryGetEmptyHand(ent, out var emptyHand)
            || !_hands.TryPickup(ent, item.Value, emptyHand))
            return;

        _hands.SetActiveHand(ent, emptyHand);
    }

    private void OnCqcConsecutive(Entity<CanPerformComboComponent> ent, ref CqcConsecutivePerformedEvent args)
    {
        if (!TryStartCombo(ent, out var proto, out var target, out _))
            return;

        DoDamage(ent, target, proto.DamageType, proto.ExtraDamage);
        _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);
        FinishCombo(ent, target, proto, args);
    }
}
