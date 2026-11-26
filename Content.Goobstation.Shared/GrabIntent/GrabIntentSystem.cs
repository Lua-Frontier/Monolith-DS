// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Goobstation.Common.Grab;
using Content.Goobstation.Shared.Grab;
using Content.Shared.ActionBlocker;
using Content.Shared.Alert;
using Content.Shared.CombatMode;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Damage.Systems;
using Content.Shared.Effects;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Random.Helpers;
using Content.Shared.Speech;
using Content.Shared.Standing;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Goobstation.Shared.GrabIntent;

/// <summary>
/// Grabs: pulling someone in combat mode and pulling them again tightens the grab from soft to hard to a choke.
/// Grabbed ones can't walk and have to break free, choked ones can't speak or breathe.
/// </summary>
public sealed partial class GrabIntentSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly GrabThrownSystem _grabThrown = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;
    [Dependency] private readonly SharedCombatModeSystem _combatMode = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedVirtualItemSystem _virtualItem = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GrabIntentComponent, PullStartedMessage>(OnPullStarted);
        SubscribeLocalEvent<GrabIntentComponent, PullStoppedMessage>(OnPullerPullStopped);
        SubscribeLocalEvent<GrabIntentComponent, RegrabAttemptEvent>(OnRegrabAttempt);
        SubscribeLocalEvent<GrabIntentComponent, AttackedEvent>(OnAttacked);
        SubscribeLocalEvent<GrabIntentComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);
        SubscribeLocalEvent<GrabIntentComponent, BeforeThrowEvent>(OnBeforeThrow);

        SubscribeLocalEvent<GrabbableComponent, PullStoppedMessage>(OnPulledPullStopped);
        SubscribeLocalEvent<GrabbableComponent, GrabReleaseAttemptEvent>(OnReleaseAttempt);
        SubscribeLocalEvent<GrabbableComponent, MoveInputEvent>(OnGrabbedMoveInput);
        SubscribeLocalEvent<GrabbableComponent, UpdateCanMoveEvent>(OnGrabbedUpdateCanMove);
        SubscribeLocalEvent<GrabbableComponent, SpeakAttemptEvent>(OnGrabbedSpeakAttempt);
        SubscribeLocalEvent<GrabbableComponent, DownedEvent>(OnGrabbedDowned);
        SubscribeLocalEvent<GrabbableComponent, StoodEvent>(OnGrabbedStood);
    }

    public bool CanGrab(EntityUid puller, EntityUid target)
    {
        return !HasComp<PacifiedComponent>(puller)
               && HasComp<MobStateComponent>(target)
               && HasComp<GrabbableComponent>(target);
    }

    #region Pulling

    private void OnPullStarted(Entity<GrabIntentComponent> ent, ref PullStartedMessage args)
    {
        // A pull in combat mode is a soft grab right away
        if (args.PullerUid == ent.Owner && _combatMode.IsInCombatMode(ent))
            TryGrab(args.PulledUid, ent);
    }

    private void OnRegrabAttempt(Entity<GrabIntentComponent> ent, ref RegrabAttemptEvent args)
    {
        if (args.Handled
            || !_combatMode.IsInCombatMode(ent)
            || !CanGrab(ent, args.Target))
            return;

        args.Handled = true;
        TryGrab(args.Target, ent);
    }

    private void OnPullerPullStopped(Entity<GrabIntentComponent> ent, ref PullStoppedMessage args)
    {
        if (args.PullerUid != ent.Owner)
            return;

        if (ent.Comp.GrabStage != GrabStage.No)
        {
            ent.Comp.GrabStage = GrabStage.No;
            Dirty(ent);
            _movementSpeed.RefreshMovementSpeedModifiers(ent);
        }

        // The pull removes its own virtual item, the grab ones are removed here
        if (_net.IsServer)
        {
            foreach (var item in GetGrabVirtualItems(ent, args.PulledUid))
            {
                QueueDel(item);
            }
        }
    }

    private void OnPulledPullStopped(Entity<GrabbableComponent> ent, ref PullStoppedMessage args)
    {
        if (args.PulledUid != ent.Owner)
            return;

        ent.Comp.GrabStage = GrabStage.No;
        ent.Comp.GrabEscapeChance = 1f;
        ent.Comp.EscapeAttemptModifier = 1f;
        Dirty(ent);
        _blocker.UpdateCanMove(ent);
    }

    #endregion

    #region Breaking free

    private void OnReleaseAttempt(Entity<GrabbableComponent> ent, ref GrabReleaseAttemptEvent args)
    {
        // Only the grabbed one has to fight, the puller lets go freely
        if (!args.Released || args.User != ent.Owner || ent.Comp.GrabStage == GrabStage.No)
            return;

        args.Released = TryBreakFree(ent, args.Puller, true);
    }

    private bool TryBreakFree(Entity<GrabbableComponent> ent, EntityUid puller, bool popup)
    {
        var curTime = _timing.CurTime;
        if (curTime < ent.Comp.NextEscapeAttempt)
        {
            if (popup)
                ServerPopup(Loc.GetString("popup-grab-release-too-soon"), ent, ent, PopupType.SmallCaution);
            return false;
        }

        // Seeded so the client predicts the same result
        var random = new System.Random(SharedRandomExtensions.HashCodeCombine((int) _timing.CurTick.Value, GetNetEntity(ent).Id));
        if (random.NextDouble() >= ent.Comp.GrabEscapeChance)
        {
            ent.Comp.NextEscapeAttempt = curTime + ent.Comp.EscapeAttemptCooldown;
            Dirty(ent);
            ServerPopup(Loc.GetString("popup-grab-release-fail-self"), ent, ent, PopupType.SmallCaution);
            return false;
        }

        ServerPopup(Loc.GetString("popup-grab-release-success-self"), ent, ent, PopupType.SmallCaution);
        ServerPopup(Loc.GetString("popup-grab-release-success-puller", ("target", Identity.Entity(ent, EntityManager))),
            puller,
            puller,
            PopupType.MediumCaution);
        return true;
    }

    /// <summary>
    /// Trying to walk away is trying to break free.
    /// </summary>
    private void OnGrabbedMoveInput(Entity<GrabbableComponent> ent, ref MoveInputEvent args)
    {
        if (ent.Comp.GrabStage == GrabStage.No
            || !args.HasDirectionalMovement
            || _timing.CurTime < ent.Comp.NextEscapeAttempt
            || !TryComp<PullableComponent>(ent, out var pullable)
            || pullable.Puller == null)
            return;

        _pulling.TryStopPull(ent, pullable, ent);
    }

    private void OnGrabbedUpdateCanMove(Entity<GrabbableComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (ent.Comp.GrabStage != GrabStage.No)
            args.Cancel();
    }

    private void OnGrabbedSpeakAttempt(Entity<GrabbableComponent> ent, ref SpeakAttemptEvent args)
    {
        if (ent.Comp.GrabStage != GrabStage.Suffocate)
            return;

        ServerPopup(Loc.GetString("popup-grabbed-cant-speak"), ent, ent, PopupType.MediumCaution);
        args.Cancel();
    }

    /// <summary>
    /// Hitting the one who holds you can loosen the grab.
    /// </summary>
    private void OnAttacked(Entity<GrabIntentComponent> ent, ref AttackedEvent args)
    {
        if (ent.Comp.GrabStage == GrabStage.No
            || !TryComp<PullerComponent>(ent, out var puller)
            || puller.Pulling != args.User
            || !TryComp<GrabbableComponent>(args.User, out var grabbable))
            return;

        var random = new System.Random(SharedRandomExtensions.HashCodeCombine((int) _timing.CurTick.Value, GetNetEntity(ent).Id));
        if (random.NextDouble() < grabbable.GrabEscapeChance)
            TryLowerGrabStage(args.User, ent);
    }

    private void OnGrabbedDowned(Entity<GrabbableComponent> ent, ref DownedEvent args)
    {
        UpdateEscapeChance(ent);
    }

    private void OnGrabbedStood(Entity<GrabbableComponent> ent, ref StoodEvent args)
    {
        UpdateEscapeChance(ent);
    }

    #endregion

    #region Movement and throws

    private void OnRefreshMovespeed(Entity<GrabIntentComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var modifier = ent.Comp.GrabStage switch
        {
            GrabStage.Soft => ent.Comp.SoftGrabSpeedModifier,
            GrabStage.Hard => ent.Comp.HardGrabSpeedModifier,
            GrabStage.Suffocate => ent.Comp.ChokeGrabSpeedModifier,
            _ => 1f,
        };

        if (modifier < 1f)
            args.ModifySpeed(modifier, modifier);
    }

    /// <summary>
    /// Throwing the hand that holds someone in a hard grab throws them.
    /// </summary>
    private void OnBeforeThrow(Entity<GrabIntentComponent> ent, ref BeforeThrowEvent args)
    {
        if (args.Cancelled
            || ent.Comp.GrabStage < GrabStage.Hard
            || !TryComp<VirtualItemComponent>(args.ItemUid, out var virtualItem)
            || !TryComp<PullerComponent>(ent, out var puller)
            || puller.Pulling is not { } target
            || virtualItem.BlockingEntity != target)
            return;

        args.Cancelled = true;

        if (!_combatMode.IsInCombatMode(ent)
            || !TryComp<PullableComponent>(target, out var pullable)
            || !TryComp<MeleeWeaponComponent>(ent, out var melee))
            return;

        var curTime = _timing.CurTime;
        if (curTime < melee.NextAttack)
            return;

        melee.NextAttack = curTime + ent.Comp.ThrowCooldown;
        Dirty(ent.Owner, melee);

        var direction = args.Direction;
        var distance = direction.Length();
        if (distance > ent.Comp.ThrowingDistance)
            direction *= ent.Comp.ThrowingDistance / distance;

        _pulling.TryStopPull(target, pullable, ent);
        _grabThrown.Throw(target, ent, direction, ent.Comp.GrabThrownSpeed, ent.Comp.GrabThrowDamage);
        _audio.PlayPvs(ent.Comp.GrabSound, target);
    }

    #endregion

    #region Helpers

    private void ServerPopup(string message, EntityUid uid, EntityUid recipient, PopupType type = PopupType.Small)
    {
        if (_net.IsServer)
            _popup.PopupEntity(message, uid, recipient, type);
    }

    /// <summary>
    /// Shows the stage change to the puller, the grabbed one and everyone around.
    /// </summary>
    private void GrabStagePopup(EntityUid puller, EntityUid pullable, GrabStage stage)
    {
        if (_net.IsClient)
            return;

        var stageName = stage.ToString().ToLowerInvariant();
        var pullerName = Identity.Entity(puller, EntityManager);
        var targetName = Identity.Entity(pullable, EntityManager);
        var targetType = stage switch
        {
            GrabStage.Hard => PopupType.MediumCaution,
            GrabStage.Suffocate => PopupType.LargeCaution,
            _ => PopupType.Small,
        };

        _popup.PopupEntity(Loc.GetString($"popup-grab-{stageName}-self", ("target", targetName)), pullable, puller);
        _popup.PopupEntity(Loc.GetString($"popup-grab-{stageName}-target", ("puller", pullerName)), pullable, pullable, targetType);

        var others = Filter.PvsExcept(pullable, entityManager: EntityManager).RemovePlayerByAttachedEntity(puller);
        _popup.PopupEntity(Loc.GetString($"popup-grab-{stageName}-others", ("puller", pullerName), ("target", targetName)),
            pullable,
            others,
            true);
    }

    private void FlashGrabbed(EntityUid uid)
    {
        if (_net.IsServer)
            _color.RaiseEffect(Color.Yellow, new List<EntityUid> { uid }, Filter.Pvs(uid, entityManager: EntityManager));
    }

    #endregion
}
