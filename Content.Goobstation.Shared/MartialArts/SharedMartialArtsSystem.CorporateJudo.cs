// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.Grab;
using Content.Goobstation.Shared.GrabIntent;
using Content.Goobstation.Shared.MartialArts.Components;
using Content.Goobstation.Shared.MartialArts.Events;
using Content.Shared.Clothing;
using Content.Shared.Damage.Systems;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Standing;

namespace Content.Goobstation.Shared.MartialArts;

public abstract partial class SharedMartialArtsSystem
{
    private void InitializeCorporateJudo()
    {
        SubscribeLocalEvent<CanPerformComboComponent, JudoDiscombobulatePerformedEvent>(OnJudoDiscombobulate);
        SubscribeLocalEvent<CanPerformComboComponent, JudoEyePokePerformedEvent>(OnJudoEyePoke);
        SubscribeLocalEvent<CanPerformComboComponent, JudoThrowPerformedEvent>(OnJudoThrow);
        SubscribeLocalEvent<CanPerformComboComponent, JudoArmbarPerformedEvent>(OnJudoArmbar);
        SubscribeLocalEvent<CanPerformComboComponent, JudoWheelThrowPerformedEvent>(OnJudoWheelThrow);

        SubscribeLocalEvent<GrantMartialArtOnEquipComponent, ClothingGotEquippedEvent>(OnGrantOnEquip);
        SubscribeLocalEvent<GrantMartialArtOnEquipComponent, ClothingGotUnequippedEvent>(OnRemoveOnUnequip);

        SubscribeLocalEvent<ArmbarredComponent, StoodEvent>(OnArmbarredStood);
        SubscribeLocalEvent<ArmbarredComponent, PullStoppedMessage>(OnArmbarStopped);
    }

    #region Learning

    private void OnGrantOnEquip(Entity<GrantMartialArtOnEquipComponent> ent, ref ClothingGotEquippedEvent args)
    {
        TryGrantMartialArt(args.Wearer, ent.Comp.MartialArtsForm, ent.Comp.LearnMessage);
    }

    private void OnRemoveOnUnequip(Entity<GrantMartialArtOnEquipComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        if (_net.IsClient)
            return;

        RevokeMartialArt(args.Wearer, ent.Comp.MartialArtsForm);
    }

    #endregion

    #region Combos

    private void OnJudoDiscombobulate(Entity<CanPerformComboComponent> ent, ref JudoDiscombobulatePerformedEvent args)
    {
        if (!TryStartCombo(ent, out var proto, out var target, out _))
            return;

        ApplySlowdown(target, args.SpeedMultiplier, args.SlowdownTime);
        _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);
        FinishCombo(ent, target, proto, args);
    }

    private void OnJudoEyePoke(Entity<CanPerformComboComponent> ent, ref JudoEyePokePerformedEvent args)
    {
        if (!TryStartCombo(ent, out var proto, out var target, out _))
            return;

        _status.TryAddStatusEffect<TemporaryBlindnessComponent>(target, "TemporaryBlindness", args.BlindnessTime, true);
        DoDamage(ent, target, proto.DamageType, proto.ExtraDamage);
        FinishCombo(ent, target, proto, args);
    }

    private void OnJudoThrow(Entity<CanPerformComboComponent> ent, ref JudoThrowPerformedEvent args)
    {
        if (!TryStartCombo(ent, out var proto, out var target, out var downed)
            || downed)
            return;

        _stun.TryKnockdown(target, proto.ParalyzeTime * GetStaminaResistance(target), true);
        _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);
        StopPull(target, ent);
        FinishCombo(ent, target, proto, args);
    }

    private void OnJudoArmbar(Entity<CanPerformComboComponent> ent, ref JudoArmbarPerformedEvent args)
    {
        if (!TryStartCombo(ent, out var proto, out var target, out var downed)
            || !downed
            || !TryComp<PullableComponent>(target, out var pullable)
            || pullable.Puller != ent.Owner)
            return;

        if (!HasComp<ArmbarredComponent>(target))
        {
            _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);
            var armbarred = AddComp<ArmbarredComponent>(target);
            armbarred.Puller = ent;
            Dirty(target, armbarred);
        }

        _stun.TryKnockdown(target, proto.ParalyzeTime * GetStaminaResistance(target), true);

        // An armbar holds like a choke
        if (TryComp<PullerComponent>(ent, out var puller)
            && TryComp<GrabIntentComponent>(ent, out var grabIntent)
            && TryComp<GrabbableComponent>(target, out var grabbable)
            && grabIntent.GrabStage != GrabStage.Suffocate)
            _grab.TrySetGrabStage((ent.Owner, puller, grabIntent), (target, pullable, grabbable), GrabStage.Suffocate);

        FinishCombo(ent, target, proto, args);
    }

    private void OnJudoWheelThrow(Entity<CanPerformComboComponent> ent, ref JudoWheelThrowPerformedEvent args)
    {
        if (!TryStartCombo(ent, out var proto, out var target, out var downed)
            || !downed
            || !TryComp<ArmbarredComponent>(target, out var armbarred)
            || armbarred.Puller != ent.Owner)
            return;

        _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);
        StopPull(target, ent);

        // Over the shoulder
        var direction = GetDirection(target, ent);
        if (direction.LengthSquared() > 0f)
            ThrowTarget(target, ent, direction, args.ThrowDistance);

        StandUp(ent);
        FinishCombo(ent, target, proto, args);
    }

    /// <summary>
    /// Stamina damage multiplier of the target, used to shorten knockdowns of resistant targets.
    /// </summary>
    private float GetStaminaResistance(EntityUid target)
    {
        var ev = new BeforeStaminaDamageEvent(1f);
        RaiseLocalEvent(target, ref ev);
        return ev.Cancelled ? 0f : MathF.Max(ev.Value, 0f);
    }

    #endregion

    #region Armbar

    private void OnArmbarredStood(Entity<ArmbarredComponent> ent, ref StoodEvent args)
    {
        StopPull(ent, ent.Comp.Puller);
        RemCompDeferred<ArmbarredComponent>(ent);
    }

    private void OnArmbarStopped(Entity<ArmbarredComponent> ent, ref PullStoppedMessage args)
    {
        if (args.PullerUid != ent.Comp.Puller || args.PulledUid != ent.Owner)
            return;

        if (!_status.HasStatusEffect(ent, "Stun"))
            _status.TryRemoveStatusEffect(ent, KnockedDownKey);

        RemCompDeferred<ArmbarredComponent>(ent);
    }

    #endregion
}
