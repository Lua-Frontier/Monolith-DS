// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.MartialArts.Components;
using Content.Goobstation.Shared.MartialArts.Events;
using Content.Goobstation.Shared.Projectiles;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Random;

namespace Content.Goobstation.Shared.MartialArts;

public abstract partial class SharedMartialArtsSystem
{
    private void InitializeSleepingCarp()
    {
        SubscribeLocalEvent<CanPerformComboComponent, SleepingCarpGnashingTeethPerformedEvent>(OnSleepingCarpGnashing);
        SubscribeLocalEvent<CanPerformComboComponent, SleepingCarpKneeHaulPerformedEvent>(OnSleepingCarpKneeHaul);
        SubscribeLocalEvent<CanPerformComboComponent, SleepingCarpCrashingWavesPerformedEvent>(OnSleepingCarpCrashingWaves);

        SubscribeLocalEvent<GrantSleepingCarpComponent, UseInHandEvent>(OnGrantSleepingCarp);

        SubscribeLocalEvent<MartialArtsKnowledgeComponent, ShotAttemptedEvent>(OnShotAttempt);
    }

    /// <summary>
    /// Makes the entity say something in the chat, server only.
    /// </summary>
    protected virtual void Say(EntityUid uid, LocId saying)
    {
    }

    #region Learning

    private void OnGrantSleepingCarp(Entity<GrantSleepingCarpComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (_net.IsClient)
            return;

        var user = args.User;

        if (ent.Comp.MaximumUses <= ent.Comp.CurrentUses)
        {
            _popup.PopupEntity(Loc.GetString("cqc-fail-used", ("manual", Identity.Entity(ent, EntityManager))), user, user);
            return;
        }

        if (TryComp<MartialArtsKnowledgeComponent>(user, out var knowledge))
        {
            var message = knowledge.MartialArtsForm == MartialArtsForms.SleepingCarp ? "cqc-fail-already" : "cqc-fail-knowanother";
            _popup.PopupEntity(Loc.GetString(message), user, user);
            return;
        }

        var student = EnsureComp<SleepingCarpStudentComponent>(user);
        if (_timing.CurTime < student.UseAgainTime)
        {
            _popup.PopupEntity(Loc.GetString("carp-scroll-waiting"), ent, user, PopupType.MediumCaution);
            return;
        }

        student.Stage++;
        if (student.Stage < ent.Comp.Stages)
        {
            var delay = _random.Next(student.MinUseDelay, student.MaxUseDelay);
            student.UseAgainTime = _timing.CurTime + delay;
            _popup.PopupEntity(Loc.GetString("carp-scroll-advance"), user, user, PopupType.Medium);
            return;
        }

        if (!TryGrantMartialArt(user, MartialArtsForms.SleepingCarp, null))
            return;

        RemComp<SleepingCarpStudentComponent>(user);
        _faction.AddFaction(user, ent.Comp.FactionToAdd);

        // Deflects shots, guns can't be used anyway
        var dodge = EnsureComp<AutoDodgeComponent>(user);
        dodge.DodgeMelee = false;
        Dirty(user, dodge);

        _popup.PopupEntity(Loc.GetString(ent.Comp.LearnMessage), ent, user, PopupType.LargeCaution);
        ent.Comp.CurrentUses++;
    }

    private void OnShotAttempt(Entity<MartialArtsKnowledgeComponent> ent, ref ShotAttemptedEvent args)
    {
        if (ent.Comp.MartialArtsForm != MartialArtsForms.SleepingCarp)
            return;

        _popup.PopupClient(Loc.GetString("gun-disabled"), ent, ent);
        args.Cancel();
    }

    #endregion

    #region Combos

    private void OnSleepingCarpGnashing(Entity<CanPerformComboComponent> ent, ref SleepingCarpGnashingTeethPerformedEvent args)
    {
        if (!TryStartCombo(ent, out var proto, out var target, out var downed))
            return;

        DoDamage(ent, target, proto.DamageType, proto.ExtraDamage + ent.Comp.ConsecutiveGnashes * args.ConsecutiveDamage);
        ent.Comp.ConsecutiveGnashes++;

        if (TryGetArt(proto.MartialArtsForm, out var art))
        {
            var sayings = downed ? art.RandomSayingsDowned : art.RandomSayings;
            if (sayings.Count > 0)
                Say(ent, _random.Pick(sayings));
        }

        // Done all the time, no popup spam
        FinishCombo(ent, target, proto, args, false);
    }

    private void OnSleepingCarpKneeHaul(Entity<CanPerformComboComponent> ent, ref SleepingCarpKneeHaulPerformedEvent args)
    {
        if (!TryStartCombo(ent, out var proto, out var target, out var downed))
            return;

        if (!downed)
        {
            DoDamage(ent, target, proto.DamageType, proto.ExtraDamage);
            _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);
            _stun.TryKnockdown(target, proto.ParalyzeTime, true);
        }
        else
        {
            DoDamage(ent, target, proto.DamageType, proto.ExtraDamage / 2);
            _stamina.TakeStaminaDamage(target, proto.StaminaDamage - 20, source: ent);
            _hands.TryDrop(target);
        }

        StopPull(target, ent);
        FinishCombo(ent, target, proto, args);
    }

    private void OnSleepingCarpCrashingWaves(Entity<CanPerformComboComponent> ent, ref SleepingCarpCrashingWavesPerformedEvent args)
    {
        if (!TryStartCombo(ent, out var proto, out var target, out var downed)
            || downed)
            return;

        DoDamage(ent, target, proto.DamageType, proto.ExtraDamage);
        _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);
        StopPull(target, ent);
        ThrowTarget(target, ent, GetDirection(ent, target), proto.ThrownSpeed);
        FinishCombo(ent, target, proto, args);
    }

    #endregion
}
