// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using Content.Goobstation.Common.MartialArts;
using Content.Goobstation.Shared.MartialArts.Components;
using Content.Goobstation.Shared.MartialArts.Events;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Alert;
using Content.Shared.Damage;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Speech.Muting;
using Content.Shared.StatusEffect;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.MartialArts;

public abstract partial class SharedMartialArtsSystem
{
    private static readonly ProtoId<AlertCategoryPrototype> NinjutsuAlertCategory = "Ninjutsu";
    private const string LossOfSurpriseKey = "LossOfSurprise";

    /// <summary>
    /// Attack speed of a high-frequency blade, swift strikes don't go faster than that.
    /// </summary>
    private static readonly TimeSpan NinjutsuMinAttackDelay = TimeSpan.FromSeconds(1f / 8f);

    private void InitializeNinjutsu()
    {
        SubscribeLocalEvent<CanPerformComboComponent, BiteTheDustPerformedEvent>(OnBiteTheDust);
        SubscribeLocalEvent<CanPerformComboComponent, DirtyKillPerformedEvent>(OnDirtyKill);

        SubscribeLocalEvent<NinjutsuSneakAttackComponent, ComponentStartup>(OnSneakAttackStartup);
        SubscribeLocalEvent<NinjutsuSneakAttackComponent, ComponentShutdown>(OnSneakAttackShutdown);
        SubscribeLocalEvent<NinjutsuSneakAttackComponent, StatusEffectEndedEvent>(OnLossOfSurpriseEnded);
        SubscribeLocalEvent<NinjutsuSneakAttackComponent, SelfBeforeGunShotEvent>(OnBeforeGunShot);

        SubscribeLocalEvent<ThrownEvent>(OnThrown);
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<InteractHandEvent>(OnInteractHand);
    }

    #region Surprise

    private void OnSneakAttackStartup(Entity<NinjutsuSneakAttackComponent> ent, ref ComponentStartup args)
    {
        _alerts.ShowAlert(ent, ent.Comp.Alert);
    }

    private void OnSneakAttackShutdown(Entity<NinjutsuSneakAttackComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        _status.TryRemoveStatusEffect(ent, LossOfSurpriseKey);
        _alerts.ClearAlertCategory(ent, NinjutsuAlertCategory);
    }

    private void OnLossOfSurpriseEnded(EntityUid uid, NinjutsuSneakAttackComponent comp, StatusEffectEndedEvent args)
    {
        if (args.Key == LossOfSurpriseKey)
            _alerts.ShowAlert(uid, comp.Alert);
    }

    private void OnBeforeGunShot(Entity<NinjutsuSneakAttackComponent> ent, ref SelfBeforeGunShotEvent args)
    {
        if (_net.IsServer)
            ResetSurprise(ent);
    }

    private void OnThrown(ref ThrownEvent ev)
    {
        if (_net.IsServer && ev.User is { } user && TryComp<NinjutsuSneakAttackComponent>(user, out var sneakAttack))
            ResetSurprise((user, sneakAttack));
    }

    /// <summary>
    /// Any noticeable action reveals the ninja for a while.
    /// </summary>
    private void ResetSurprise(Entity<NinjutsuSneakAttackComponent> ent)
    {
        _status.TryAddStatusEffect<NinjutsuLossOfSurpriseComponent>(ent, LossOfSurpriseKey, ent.Comp.LossOfSurpriseTime, true);
    }

    #endregion

    private void OnMobStateChanged(MobStateChangedEvent ev)
    {
        if (_net.IsClient
            || ev.NewMobState != MobState.Dead
            || ev.Origin is not { } origin
            || !TryComp(origin, out MartialArtsKnowledgeComponent? knowledge)
            || knowledge.MartialArtsForm != MartialArtsForms.Ninjutsu)
            return;

        ApplyMultiplier(origin, 1.2f, 0f, TimeSpan.FromSeconds(3), MartialArtModifierType.MoveSpeed);
    }

    /// <summary>
    /// Takedown: grabbing someone from behind by hand mutes and slows them.
    /// </summary>
    private void OnInteractHand(InteractHandEvent args)
    {
        if (_net.IsClient
            || args.User == args.Target
            || !TryComp<NinjutsuSneakAttackComponent>(args.User, out var sneakAttack)
            || !HasComp<MobStateComponent>(args.Target))
            return;

        var user = args.User;
        var target = args.Target;
        var surprised = !HasComp<NinjutsuLossOfSurpriseComponent>(user);
        ResetSurprise((user, sneakAttack));

        if (!surprised)
        {
            _popup.PopupEntity(Loc.GetString("ninjutsu-fail-loss-of-surprise"), user, user);
            return;
        }

        if (_standing.IsDown(target))
        {
            _popup.PopupEntity(Loc.GetString("martial-arts-fail-target-down"), user, user);
            return;
        }

        var slowdownTime = sneakAttack.TakedownSlowdownTime;
        var muteTime = sneakAttack.TakedownMuteTime;
        if (_backstab.TryBackstab(target, user, Angle.FromDegrees(45d), true, false, false))
        {
            slowdownTime *= sneakAttack.TakedownBackstabMultiplier;
            muteTime *= sneakAttack.TakedownBackstabMultiplier;
        }

        ApplySlowdown(target, sneakAttack.TakedownSpeedModifier, slowdownTime);
        _status.TryAddStatusEffect<MutedComponent>(target, "Muted", muteTime, true);

        _audio.PlayPvs(sneakAttack.AssassinateSoundUnarmed, target);
        ComboPopup(user, target, sneakAttack.TakedownComboName);
    }

    /// <summary>
    /// Server only, called from the broadcast melee hit handler.
    /// </summary>
    private void OnNinjutsuMeleeHit(EntityUid uid, MeleeHitEvent ev)
    {
        if (!ev.IsHit || !TryComp(uid, out NinjutsuSneakAttackComponent? sneakAttack))
            return;

        var surprised = !HasComp<NinjutsuLossOfSurpriseComponent>(uid);
        ResetSurprise((uid, sneakAttack));

        if (!surprised || ev.HitEntities.Count == 0)
            return;

        var target = ev.HitEntities[0];
        if (target == uid
            || !HasComp<MobStateComponent>(target)
            || !IsNinjutsuWeapon(uid, ev.Weapon, out _))
            return;

        if (sneakAttack.Multiplier > 1f)
            ev.BonusDamage += ev.BaseDamage * (sneakAttack.Multiplier - 1f);

        // Assassination is done with a precise light attack only
        if (ev.Direction != null || ev.BaseDamage.GetTotal() <= 0)
            return;

        var unarmed = uid == ev.Weapon;
        var bonusDamage = new DamageSpecifier();
        bonusDamage.DamageDict.Add(unarmed ? "Blunt" : "Slash",
            unarmed ? sneakAttack.AssassinateUnarmedModifier : sneakAttack.AssassinateModifier);

        _damageable.TryChangeDamage(target,
            bonusDamage,
            origin: uid,
            armorPenetration: sneakAttack.AssassinateArmorPierce,
            targetPart: TargetBodyPart.Torso);

        _audio.PlayPvs(unarmed ? sneakAttack.AssassinateSoundUnarmed : sneakAttack.AssassinateSoundArmed, target);
        ComboPopup(uid, target, sneakAttack.AssassinateComboName);
    }

    /// <summary>
    /// Swift strike: hits on downed enemies are faster.
    /// </summary>
    private void OnNinjutsuAttackPerformed(Entity<NinjutsuSneakAttackComponent> ent, ComboAttackPerformedEvent args)
    {
        var surprised = !HasComp<NinjutsuLossOfSurpriseComponent>(ent);
        ResetSurprise(ent);

        if (surprised
            || args.Type != ComboAttackType.Harm
            || args.Target == args.Performer
            || !IsNinjutsuWeapon(args.Performer, args.Weapon, out var melee)
            || !_standing.IsDown(args.Target))
            return;

        if (args.Performer == args.Weapon)
            _stamina.TakeStaminaDamage(args.Target, 30f, source: ent);

        var attackDelay = TimeSpan.FromSeconds(1f / _melee.GetAttackRate(args.Weapon, args.Performer, melee));
        if (attackDelay / 2 <= NinjutsuMinAttackDelay)
            return;

        melee.NextAttack -= attackDelay / 2;
        Dirty(args.Weapon, melee);
    }

    private void OnDirtyKill(Entity<CanPerformComboComponent> ent, ref DirtyKillPerformedEvent args)
    {
        if (!TryStartCombo(ent, out var proto, out var target, out var downed))
            return;

        if (!downed)
        {
            _popup.PopupEntity(Loc.GetString("martial-arts-fail-target-standing"), ent, ent);
            return;
        }

        // Stun for the rest of the knockdown, a new knockdown would be a stunlock
        if (_status.TryGetTime(target, KnockedDownKey, out var knockdown))
        {
            var knockdownLeft = knockdown.Value.Item2 - _timing.CurTime;
            var time = proto.ParalyzeTime < knockdownLeft ? proto.ParalyzeTime : knockdownLeft;
            _stun.TryStun(target, time, true);
        }

        DoDamage(ent, target, proto.DamageType, proto.ExtraDamage * GetNinjutsuDamageMultiplier(ent));
        FinishCombo(ent, target, proto, args);
    }

    private void OnBiteTheDust(Entity<CanPerformComboComponent> ent, ref BiteTheDustPerformedEvent args)
    {
        if (!TryStartCombo(ent, out var proto, out var target, out var downed))
            return;

        if (downed)
        {
            _popup.PopupEntity(Loc.GetString("martial-arts-fail-target-down"), ent, ent);
            return;
        }

        StopPull(target, ent);
        _stun.TryKnockdown(target, proto.ParalyzeTime, true);
        DoDamage(ent, target, proto.DamageType, proto.ExtraDamage * GetNinjutsuDamageMultiplier(ent));
        FinishCombo(ent, target, proto, args);
    }

    private float GetNinjutsuDamageMultiplier(EntityUid uid)
    {
        if (HasComp<NinjutsuLossOfSurpriseComponent>(uid)
            || !TryComp(uid, out NinjutsuSneakAttackComponent? sneakAttack))
            return 1f;

        return sneakAttack.Multiplier;
    }

    /// <summary>
    /// Ninjutsu works with bare hands and slashing weapons.
    /// </summary>
    private bool IsNinjutsuWeapon(EntityUid user, EntityUid weapon, [NotNullWhen(true)] out MeleeWeaponComponent? melee)
    {
        if (!TryComp(weapon, out melee))
            return false;

        return user == weapon || melee.Damage.DamageDict.ContainsKey("Slash");
    }
}
