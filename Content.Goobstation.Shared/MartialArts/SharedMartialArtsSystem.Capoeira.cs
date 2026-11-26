// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Goobstation.Common.MartialArts;
using Content.Goobstation.Shared.MartialArts.Components;
using Content.Goobstation.Shared.MartialArts.Events;
using Content.Goobstation.Shared.Sprinting;
using Content.Shared.Emoting;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Goobstation.Shared.MartialArts;

public abstract partial class SharedMartialArtsSystem
{
    private const string VulnerableKey = "MartialArtsVulnerable";

    private void InitializeCapoeira()
    {
        SubscribeLocalEvent<CanPerformComboComponent, PushKickPerformedEvent>(OnPushKick);
        SubscribeLocalEvent<CanPerformComboComponent, CircleKickPerformedEvent>(OnCircleKick);
        SubscribeLocalEvent<CanPerformComboComponent, SweepKickPerformedEvent>(OnSweepKick);
        SubscribeLocalEvent<CanPerformComboComponent, SpinKickPerformedEvent>(OnSpinKick);
        SubscribeLocalEvent<CanPerformComboComponent, KickUpPerformedEvent>(OnKickUp);
    }

    private void OnCapoeiraMeleeHit(EntityUid uid, MeleeHitEvent ev)
    {
        if (ev.HitEntities.Count > 0 || ev.Weapon != uid)
            return;

        // A miss makes the next hits stronger
        ApplyMultiplier(uid,
            1f,
            2f,
            TimeSpan.FromSeconds(3),
            MartialArtModifierType.Damage | MartialArtModifierType.Unarmed);

        // and recovers faster
        if (!TryComp(uid, out MeleeWeaponComponent? melee))
            return;

        melee.NextAttack -= TimeSpan.FromSeconds(0.75f / _melee.GetAttackRate(uid, uid, melee));
        Dirty(uid, melee);
    }

    private void OnCapoeiraAttackPerformed(EntityUid ent, ComboAttackPerformedEvent args)
    {
        if (args.Performer == args.Target)
            return;

        if (args.Type == ComboAttackType.Grab)
        {
            ApplyMultiplier(ent, 1.2f, 0f, TimeSpan.FromSeconds(4), MartialArtModifierType.MoveSpeed);

            if (!TryComp(args.Target, out SprinterComponent? sprinter))
                return;

            // The grabbed one can't sprint away for a while
            _sprinting.ToggleSprint(args.Target, sprinter, false, false);
            sprinter.LastSprint = _timing.CurTime + TimeSpan.FromSeconds(2);
            Dirty(args.Target, sprinter);
            return;
        }

        if (args.Weapon != args.Performer
            || args.Type is not (ComboAttackType.Disarm or ComboAttackType.Harm))
            return;

        var multiplier = Math.Clamp(MathF.Pow(GetVelocity(ent), 0.2f), 1f, 1.5f);
        ApplyMultiplier(ent, multiplier, 0f, TimeSpan.FromSeconds(3));
    }

    private void OnKickUp(Entity<CanPerformComboComponent> ent, ref KickUpPerformedEvent args)
    {
        if (!TryStartCombo(ent, out _, out var target, out _)
            || target != ent.Owner)
            return;

        StandUp(ent);
        ent.Comp.LastAttacks.Clear();
        Dirty(ent);
    }

    private void OnSpinKick(Entity<CanPerformComboComponent> ent, ref SpinKickPerformedEvent args)
    {
        if (!TryStartCombo(ent, out var proto, out var target, out var downed))
            return;

        if (downed)
        {
            _popup.PopupEntity(Loc.GetString("martial-arts-fail-target-down"), ent, ent);
            return;
        }

        if (!TryPerformCapoeiraMove(ent, args, out var power))
            return;

        _stun.TryKnockdown(target, proto.ParalyzeTime * power, true);
        StopPull(target, ent);
        DoDamage(ent, target, proto.DamageType, proto.ExtraDamage * power);
        _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);
        ApplyMultiplier(ent, args.AttackSpeedMultiplier, 0f, args.AttackSpeedMultiplierTime);

        if (args.Emote != null && TryComp(ent, out AnimatedEmotesComponent? emotes))
        {
            emotes.Emote = args.Emote.Value;
            Dirty(ent, emotes);
        }

        FinishCombo(ent, target, proto, args);
    }

    private void OnSweepKick(Entity<CanPerformComboComponent> ent, ref SweepKickPerformedEvent args)
    {
        if (!TryStartCombo(ent, out var proto, out var target, out _)
            || !TryPerformCapoeiraMove(ent, args, out var power))
            return;

        _stun.TryKnockdown(target, proto.ParalyzeTime * power, true);
        DoDamage(ent, target, proto.DamageType, proto.ExtraDamage * power);
        _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);
        ApplyMultiplier(ent, args.AttackSpeedMultiplier, 0f, args.AttackSpeedMultiplierTime);
        FinishCombo(ent, target, proto, args);
    }

    private void OnCircleKick(Entity<CanPerformComboComponent> ent, ref CircleKickPerformedEvent args)
    {
        if (!TryStartCombo(ent, out var proto, out var target, out _)
            || !TryPerformCapoeiraMove(ent, args, out var power))
            return;

        ApplySlowdown(target, 1f / MathF.Max(1f, power), args.SlowDownTime * power);
        DoDamage(ent, target, proto.DamageType, proto.ExtraDamage * power);
        FinishCombo(ent, target, proto, args);
    }

    private void OnPushKick(Entity<CanPerformComboComponent> ent, ref PushKickPerformedEvent args)
    {
        if (!TryStartCombo(ent, out var proto, out var target, out var downed))
            return;

        if (downed)
        {
            _popup.PopupEntity(Loc.GetString("martial-arts-fail-target-down"), ent, ent);
            return;
        }

        if (!TryPerformCapoeiraMove(ent, args, out var power))
            return;

        var time = proto.ParalyzeTime * power;

        StopPull(target, ent);

        // The new kick replaces the old vulnerability so they don't stack
        if (_status.TryAddStatusEffect<MartialArtsVulnerableComponent>(target, VulnerableKey, time, true)
            && TryComp<MartialArtsVulnerableComponent>(target, out var vulnerable))
        {
            vulnerable.ModifierSets.Clear();
            vulnerable.ModifierSets.Add(args.ModifierSet);
        }

        _stun.TryKnockdown(target, time, true);

        var direction = GetDirection(ent, target);
        if (direction.LengthSquared() > 0f)
            ThrowTarget(target, ent, Vector2.Normalize(direction) * args.ThrowRange * power, proto.ThrownSpeed);

        FinishCombo(ent, target, proto, args);
    }

    private bool TryPerformCapoeiraMove(EntityUid uid, BaseCapoeiraEvent ev, out float power)
    {
        var velocity = GetVelocity(uid);
        power = 0f;

        if (velocity < ev.MinVelocity)
        {
            _popup.PopupEntity(Loc.GetString("capoeira-fail-low-velocity"), uid, uid);
            return false;
        }

        power = Math.Clamp(velocity * ev.VelocityPowerMultiplier, ev.MinPower, ev.MaxPower);
        _stamina.TakeStaminaDamage(uid, ev.StaminaToHeal, visual: false);
        return true;
    }
}
