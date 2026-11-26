// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.MartialArts;
using Content.Goobstation.Shared.MartialArts.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Melee;

namespace Content.Goobstation.Shared.MartialArts;

/// <summary>
/// Tracks the attacks of martial artists and performs combos.
/// </summary>
public abstract partial class SharedMartialArtsSystem
{
    private void InitializeCombos()
    {
        SubscribeLocalEvent<CanPerformComboComponent, ComboAttackPerformedEvent>(OnComboAttackPerformed);
    }

    private void OnComboAttackPerformed(Entity<CanPerformComboComponent> ent, ref ComboAttackPerformedEvent args)
    {
        // Attacks on yourself skip the usual attack code, the cooldown is taken here
        if (args.Target == ent.Owner && !TryUseSelfAttack(ent))
            return;

        if (_net.IsClient || !HasComp<MobStateComponent>(args.Target))
            return;

        if (TryComp<MartialArtsKnowledgeComponent>(ent, out var knowledge))
        {
            switch (knowledge.MartialArtsForm)
            {
                case MartialArtsForms.CloseQuartersCombat:
                    OnCqcAttackPerformed(ent, args);
                    break;
                case MartialArtsForms.Capoeira:
                    OnCapoeiraAttackPerformed(ent, args);
                    break;
            }
        }

        var combo = ent.Comp;
        if (combo.CurrentTarget != null && args.Target != combo.CurrentTarget)
            combo.LastAttacks.Clear();

        // Combos are unarmed only
        if (args.Weapon == ent.Owner)
        {
            combo.CurrentTarget = args.Target;
            combo.ResetTime = _timing.CurTime + combo.ResetDelay;
            combo.LastAttacks.Add(args.Type);

            var excess = combo.LastAttacks.Count - combo.LastAttacksLimit;
            if (combo.LastAttacksLimit >= 0 && excess > 0)
                combo.LastAttacks.RemoveRange(0, excess);

            CheckCombo(ent, args.Target);
        }
        else
        {
            combo.LastAttacks.Clear();
        }

        if (TryComp<NinjutsuSneakAttackComponent>(ent, out var sneakAttack))
            OnNinjutsuAttackPerformed((ent, sneakAttack), args);

        Dirty(ent);
    }

    /// <summary>
    /// Checks the combos in order. A combo that can't be done right now (e.g. needs a downed target)
    /// lets the next matching one go instead.
    /// </summary>
    private void CheckCombo(Entity<CanPerformComboComponent> ent, EntityUid target)
    {
        var selfTarget = ent.Owner == target;

        foreach (var comboId in ent.Comp.AllowedCombos)
        {
            var attacks = ent.Comp.LastAttacks;
            if (attacks.Count == 0)
                return;

            if (!_proto.TryIndex(comboId, out var proto)
                || proto.ResultEvent == null
                || selfTarget != proto.PerformOnSelf)
                continue;

            var required = proto.AttackTypes;
            var offset = attacks.Count - required.Count;
            if (offset < 0)
                continue;

            var match = true;
            for (var i = 0; i < required.Count; i++)
            {
                if (attacks[offset + i] == required[i])
                    continue;

                match = false;
                break;
            }

            if (!match)
                continue;

            ent.Comp.BeingPerformed = proto.ID;
            RaiseLocalEvent(ent.Owner, proto.ResultEvent);
        }
    }

    private bool TryUseSelfAttack(EntityUid uid)
    {
        if (!TryComp<MeleeWeaponComponent>(uid, out var melee))
            return false;

        var curTime = _timing.CurTime;
        if (curTime < melee.NextAttack)
            return false;

        melee.NextAttack = curTime + TimeSpan.FromSeconds(1f / _melee.GetAttackRate(uid, uid, melee));
        Dirty(uid, melee);
        return true;
    }
}
