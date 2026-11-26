// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.Grab;
using Content.Goobstation.Common.MartialArts;
using Content.Goobstation.Shared.MartialArts.Components;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Physics.Components;

namespace Content.Goobstation.Shared.GrabIntent;

public sealed partial class GrabIntentSystem
{
    /// <summary>
    /// Tightens the grab of the puller by one stage. Tightening a choke deals stamina damage instead.
    /// The grab shares the cooldown with attacks.
    /// </summary>
    /// <returns>True if the grab was handled, even when it is on cooldown.</returns>
    public bool TryGrab(EntityUid pullableUid, EntityUid pullerUid)
    {
        if (!TryComp<PullableComponent>(pullableUid, out var pullable)
            || !TryComp<GrabbableComponent>(pullableUid, out var grabbable)
            || !TryComp<PullerComponent>(pullerUid, out var puller)
            || !TryComp<GrabIntentComponent>(pullerUid, out var grabIntent)
            || pullable.Puller != pullerUid
            || puller.Pulling != pullableUid
            || !CanGrab(pullerUid, pullableUid)
            || !TryComp<MeleeWeaponComponent>(pullerUid, out var melee))
            return false;

        // Someone held in a grab can't grab others
        if (TryComp<PullableComponent>(pullerUid, out var pullerPullable) && pullerPullable.Puller != null)
            return false;

        var curTime = _timing.CurTime;
        if (curTime < melee.NextAttack)
            return true;

        melee.NextAttack = curTime + grabIntent.StageChangeCooldown;
        Dirty(pullerUid, melee);

        if (grabIntent.GrabStage == GrabStage.Suffocate)
        {
            _stamina.TakeStaminaDamage(pullableUid, grabIntent.SuffocateGrabStaminaDamage, source: pullerUid);
            _audio.PlayPredicted(grabIntent.GrabSound, pullableUid, pullerUid);
            RaiseGrabCombo(pullerUid, pullableUid);
            return true;
        }

        var newStage = grabIntent.GrabStage + 1;

        // Martial artists grab the downed ones firmly right away
        if (newStage == GrabStage.Soft
            && HasComp<MartialArtsKnowledgeComponent>(pullerUid)
            && _standing.IsDown(pullableUid))
            newStage = GrabStage.Hard;

        if (!TrySetGrabStage((pullerUid, puller, grabIntent), (pullableUid, pullable, grabbable), newStage))
            return true;

        _audio.PlayPredicted(grabIntent.GrabSound, pullableUid, pullerUid);
        FlashGrabbed(pullableUid);
        RaiseGrabCombo(pullerUid, pullableUid);
        return true;
    }

    /// <summary>
    /// Sets the grab stage of both the puller and the pulled one.
    /// </summary>
    /// <returns>False if there are no free hands for the stage.</returns>
    public bool TrySetGrabStage(Entity<PullerComponent, GrabIntentComponent> puller,
        Entity<PullableComponent, GrabbableComponent> pullable,
        GrabStage stage,
        float escapeAttemptModifier = 1f)
    {
        if (!TryUpdateGrabVirtualItems(puller, pullable, stage))
            return false;

        var oldStage = puller.Comp2.GrabStage;
        puller.Comp2.GrabStage = stage;
        pullable.Comp2.GrabStage = stage;
        pullable.Comp2.EscapeAttemptModifier *= escapeAttemptModifier;
        UpdateEscapeChance(pullable, puller);

        _alerts.ShowAlert(puller, puller.Comp1.PullingAlert, (short) stage);
        _alerts.ShowAlert(pullable, pullable.Comp1.PulledAlert, (short) stage);
        _blocker.UpdateCanMove(pullable);
        _movementSpeed.RefreshMovementSpeedModifiers(puller);

        if (stage != oldStage)
            GrabStagePopup(puller, pullable, stage);

        Dirty(puller.Owner, puller.Comp2);
        Dirty(pullable.Owner, pullable.Comp2);
        return true;
    }

    /// <summary>
    /// Loosens the grab by one stage, a soft grab becomes a plain pull.
    /// </summary>
    public void TryLowerGrabStage(EntityUid pullableUid, EntityUid pullerUid)
    {
        if (!TryComp<PullableComponent>(pullableUid, out var pullable)
            || !TryComp<GrabbableComponent>(pullableUid, out var grabbable)
            || !TryComp<PullerComponent>(pullerUid, out var puller)
            || !TryComp<GrabIntentComponent>(pullerUid, out var grabIntent)
            || pullable.Puller != pullerUid
            || grabIntent.GrabStage == GrabStage.No)
            return;

        grabbable.NextEscapeAttempt = _timing.CurTime + TimeSpan.FromSeconds(1);
        TrySetGrabStage((pullerUid, puller, grabIntent), (pullableUid, pullable, grabbable), grabIntent.GrabStage - 1);
    }

    private void RaiseGrabCombo(EntityUid puller, EntityUid target)
    {
        var ev = new ComboAttackPerformedEvent(puller, target, puller, ComboAttackType.Grab);
        RaiseLocalEvent(puller, ref ev);
    }

    #region Escape chance

    private void UpdateEscapeChance(Entity<GrabbableComponent> ent)
    {
        // Mobs lie down and stand up all the time, only a held one matters
        if (ent.Comp.GrabStage == GrabStage.No
            || !TryComp<PullableComponent>(ent, out var pullable)
            || pullable.Puller is not { } pullerUid
            || !TryComp<PullerComponent>(pullerUid, out var puller)
            || !TryComp<GrabIntentComponent>(pullerUid, out var grabIntent))
            return;

        UpdateEscapeChance((ent.Owner, pullable, ent.Comp), (pullerUid, puller, grabIntent));
        Dirty(ent);
    }

    /// <summary>
    /// Escape chance depends on the stage, the masses and lying on the floor.
    /// </summary>
    private void UpdateEscapeChance(Entity<PullableComponent, GrabbableComponent> pullable,
        Entity<PullerComponent, GrabIntentComponent> puller)
    {
        var stage = puller.Comp2.GrabStage;
        if (stage == GrabStage.No)
        {
            pullable.Comp2.GrabEscapeChance = 1f;
            return;
        }

        var chance = puller.Comp2.EscapeChances.GetValueOrDefault(stage, 1f)
                     * GetMassMultiplier(pullable, puller)
                     * pullable.Comp2.EscapeAttemptModifier;

        if (_standing.IsDown(pullable))
            chance *= puller.Comp2.DownedEscapeChanceMultiplier;

        pullable.Comp2.GrabEscapeChance = Math.Clamp(chance, 0f, 1f);
    }

    /// <summary>
    /// 1 for equal masses, up to 2 for a much heavier grabbed one and down to 0.5 for a much lighter one.
    /// </summary>
    private float GetMassMultiplier(EntityUid pullable, EntityUid puller)
    {
        if (!TryComp<PhysicsComponent>(pullable, out var pullablePhysics)
            || !TryComp<PhysicsComponent>(puller, out var pullerPhysics))
            return 1f;

        var total = pullablePhysics.Mass + pullerPhysics.Mass;
        if (total <= 0f)
            return 1f;

        return Math.Clamp(2f * pullablePhysics.Mass / total, 0.5f, 2f);
    }

    #endregion

    #region Virtual items

    /// <summary>
    /// A choke takes the second hand, the first one is taken by the pull itself.
    /// </summary>
    private bool TryUpdateGrabVirtualItems(Entity<PullerComponent, GrabIntentComponent> puller,
        EntityUid pullable,
        GrabStage stage)
    {
        if (!puller.Comp1.NeedsHands)
            return true;

        var needed = stage == GrabStage.Suffocate ? 1 : 0;
        var items = GetGrabVirtualItems(puller, pullable);
        var delta = needed - items.Count;

        if (delta > 0)
        {
            if (!_hands.TryGetEmptyHand(puller, out _))
            {
                ServerPopup(Loc.GetString("popup-grab-need-hand"), puller, puller, PopupType.Medium);
                return false;
            }

            // Virtual items are spawned by the server, the client only checks the hands
            if (_net.IsServer)
            {
                for (var i = 0; i < delta; i++)
                {
                    if (_virtualItem.TrySpawnVirtualItemInHand(pullable, puller, out var item))
                    {
                        EnsureComp<GrabVirtualItemComponent>(item.Value);
                        continue;
                    }

                    ServerPopup(Loc.GetString("popup-grab-need-hand"), puller, puller, PopupType.Medium);
                    return false;
                }
            }
        }
        else if (delta < 0 && _net.IsServer)
        {
            // Deleted without the virtual item events, those would stop the pull
            for (var i = 0; i < -delta; i++)
            {
                QueueDel(items[items.Count - 1 - i]);
            }
        }

        return true;
    }

    /// <summary>
    /// Virtual items taken by the grab, not the one of the pull itself.
    /// </summary>
    private List<EntityUid> GetGrabVirtualItems(EntityUid puller, EntityUid pullable)
    {
        var items = new List<EntityUid>();
        foreach (var held in _hands.EnumerateHeld(puller))
        {
            if (HasComp<GrabVirtualItemComponent>(held)
                && TryComp<VirtualItemComponent>(held, out var virtualItem)
                && virtualItem.BlockingEntity == pullable)
                items.Add(held);
        }

        return items;
    }

    #endregion
}
