// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.MartialArts.Components;
using Content.Goobstation.Shared.MartialArts.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffect;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Physics.Components;

namespace Content.Goobstation.Shared.MartialArts;

public abstract partial class SharedMartialArtsSystem
{
    private const string DragonPowerKey = "DragonPower";

    private void InitializeDragon()
    {
        SubscribeLocalEvent<CanPerformComboComponent, DragonClawPerformedEvent>(OnDragonClaw);
        SubscribeLocalEvent<CanPerformComboComponent, DragonTailPerformedEvent>(OnDragonTail);
        SubscribeLocalEvent<CanPerformComboComponent, DragonStrikePerformedEvent>(OnDragonStrike);

        SubscribeLocalEvent<DragonPowerBuffComponent, AttackedEvent>(OnDragonPowerAttacked);
    }

    /// <summary>
    /// Standing still for a while gives Dragon Power.
    /// </summary>
    private void UpdateDragon(TimeSpan curTime)
    {
        var query = EntityQueryEnumerator<DragonKungFuTimerComponent, StatusEffectsComponent, MobStateComponent, PhysicsComponent>();
        while (query.MoveNext(out var uid, out var timer, out var status, out var mobState, out var physics))
        {
            if (mobState.CurrentState != MobState.Alive)
                continue;

            if (physics.LinearVelocity.LengthSquared() > timer.MinVelocitySquared)
            {
                timer.LastMoveTime = curTime;
                continue;
            }

            if (curTime < timer.LastMoveTime + timer.PauseDuration
                || !_blocker.CanInteract(uid, null))
                continue;

            // Checked once per pause instead of every tick
            timer.LastMoveTime = curTime;

            // Only renewed when about to run out, every renewal sends the status effects and the alert
            if (_status.TryGetTime(uid, DragonPowerKey, out var time, status)
                && time.Value.Item2 - curTime > timer.PauseDuration * 2)
                continue;

            _status.TryAddStatusEffect<DragonPowerBuffComponent>(uid, DragonPowerKey, timer.BuffLength, true, status);
        }
    }

    private void OnDragonPowerAttacked(Entity<DragonPowerBuffComponent> ent, ref AttackedEvent args)
    {
        // Only unarmed and conscious
        if (_hands.TryGetActiveItem(ent.Owner, out _)
            || !_blocker.CanInteract(ent, null))
            return;

        args.ModifiersList.Add(ent.Comp.ModifierSet);

        if (_net.IsClient)
            return;

        // Hits back harder, armed or not
        ApplyMultiplier(ent,
            ent.Comp.DamageMultiplier,
            0f,
            ent.Comp.AttackDamageBuffDuration,
            MartialArtModifierType.Damage);
    }

    private void OnDragonStrike(Entity<CanPerformComboComponent> ent, ref DragonStrikePerformedEvent args)
    {
        if (!TryStartCombo(ent, out var proto, out var target, out var downed))
            return;

        if (!downed)
        {
            _popup.PopupEntity(Loc.GetString("martial-arts-fail-target-standing"), ent, ent);
            return;
        }

        _stun.TryParalyze(target, proto.ParalyzeTime, true);
        DoDamage(ent, target, proto.DamageType, proto.ExtraDamage);
        FinishCombo(ent, target, proto, args);
    }

    private void OnDragonTail(Entity<CanPerformComboComponent> ent, ref DragonTailPerformedEvent args)
    {
        if (!TryStartCombo(ent, out var proto, out var target, out var downed))
            return;

        StopPull(target, ent);

        if (downed)
        {
            // A short stun, no stunlocks
            _stun.TryStun(target, args.DownedStunTime, true);
        }
        else
        {
            _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);
            _stun.TryKnockdown(target, proto.ParalyzeTime, true);
            DoDamage(ent, target, proto.DamageType, proto.ExtraDamage);
        }

        FinishCombo(ent, target, proto, args);
    }

    private void OnDragonClaw(Entity<CanPerformComboComponent> ent, ref DragonClawPerformedEvent args)
    {
        if (!TryStartCombo(ent, out var proto, out var target, out _))
            return;

        ApplySlowdown(target, args.SpeedModifier, args.SlowdownTime);
        _stamina.TakeStaminaDamage(target, proto.StaminaDamage, source: ent);
        DoDamage(ent, target, proto.DamageType, proto.ExtraDamage);
        FinishCombo(ent, target, proto, args);
    }
}
