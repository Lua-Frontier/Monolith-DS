// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Bed.Sleep;
using Content.Shared.Buckle.Components;
using Content.Shared.CombatMode;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Gravity;
using Content.Shared.Input;
using Content.Shared.Mech.Components;
using Content.Shared.Mobs;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Zombies;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Goobstation.Shared.Sprinting;

public abstract class SharedSprintingSystem : EntitySystem
{
    [Dependency] private readonly StaminaSystem _staminaSystem = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedGravitySystem _gravity = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedMoverController _moverController = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SprinterComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.Sprint, new SprintInputCmdHandler(this))
            .Register<SharedSprintingSystem>();
        SubscribeLocalEvent<SprinterComponent, SprintToggleEvent>(OnSprintToggle);
        SubscribeLocalEvent<SprinterComponent, MobStateChangedEvent>(OnMobStateChangedEvent);
        SubscribeLocalEvent<SprinterComponent, SleepStateChangedEvent>(OnSleep);
        SubscribeLocalEvent<SprinterComponent, KnockedDownEvent>(OnSprintDisablingEvent);
        SubscribeLocalEvent<SprinterComponent, StunnedEvent>(OnSprintDisablingEvent);
        SubscribeLocalEvent<SprinterComponent, DownedEvent>(OnSprintDisablingEvent);
        SubscribeLocalEvent<CuffableComponent, SprintAttemptEvent>(OnCuffableSprintAttempt);
        SubscribeLocalEvent<MechPilotComponent, SprintAttemptEvent>(OnMechPilotSprintAttempt);
        SubscribeLocalEvent<StandingStateComponent, SprintAttemptEvent>(OnStandingStateSprintAttempt);
        SubscribeLocalEvent<BuckleComponent, SprintAttemptEvent>(OnBuckleSprintAttempt);
        SubscribeLocalEvent<SprinterComponent, EntityZombifiedEvent>(OnZombified);
        SubscribeLocalEvent<SprinterComponent, DisarmedEvent>(OnDisarm);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<SharedSprintingSystem>();
    }

    #region Core Functions

    private sealed class SprintInputCmdHandler(SharedSprintingSystem system) : InputCmdHandler
    {
        public override bool HandleCmdMessage(IEntityManager entManager, ICommonSession? session, IFullInputCmdMessage message)
        {
            if (session?.AttachedEntity == null)
                return false;

            system.HandleSprintInput(session, message);
            return false;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Goobstation port: our stamina has no continuous drains, so sprinting drains it here.
        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<SprinterComponent>();
        while (query.MoveNext(out var uid, out var sprinter))
        {
            if (!sprinter.IsSprinting)
                continue;

            _staminaSystem.TakeStaminaDamage(uid, sprinter.StaminaDrainRate * frameTime, visual: false);
        }
    }

    private void OnRefreshSpeed(Entity<SprinterComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (!ent.Comp.IsSprinting)
            return;

        args.ModifySpeed(ent.Comp.SprintSpeedMultiplier, ent.Comp.SprintSpeedMultiplier);
    }

    private void HandleSprintInput(ICommonSession? session, IFullInputCmdMessage message)
    {
        if (session?.AttachedEntity == null
            || !TryComp<SprinterComponent>(session.AttachedEntity, out var sprinterComponent)
            || !TryComp<InputMoverComponent>(session.AttachedEntity, out var inputMoverComponent)
            || !sprinterComponent.IsSprinting
            // We check this instead of physics so that we can gatekeep sprinting to only work when you are moving intentionally, and not walking.
            && _moverController.GetVelocityInput(inputMoverComponent).Sprinting == Vector2.Zero)
            return;

        if (!sprinterComponent.CanSprint)
        {
            if (message.State == BoundKeyState.Down) // Without this check the message triggers when holding and releasing.
                _popupSystem.PopupClient(Loc.GetString("sprint-disabled"), session.AttachedEntity.Value, session.AttachedEntity.Value, PopupType.Medium);

            return;
        }

        RaiseLocalEvent(session.AttachedEntity.Value, new SprintToggleEvent(!sprinterComponent.IsSprinting && message.State == BoundKeyState.Down));
    }

    private void OnSprintToggle(EntityUid uid, SprinterComponent component, SprintToggleEvent args) =>
        ToggleSprint(uid, component, args.IsSprinting);

    public void ToggleSprint(EntityUid uid, SprinterComponent component, bool newSprintState, bool gracefulStop = true)
    {
        // Breaking these into two separate if's for better readability
        if (newSprintState == component.IsSprinting)
            return;

        if (newSprintState
            && (!CanSprint(uid, component)
            || _timing.CurTime - component.LastSprint < component.TimeBetweenSprints))
            return;

        component.LastSprint = _timing.CurTime;
        component.IsSprinting = newSprintState;

        if (newSprintState)
        {
            RaiseLocalEvent(uid, new SprintStartEvent());
            _audio.PlayPredicted(component.SprintStartupSound, uid, uid);
        }

        if (!gracefulStop)
            _damageable.TryChangeDamage(uid, component.SprintDamageSpecifier);

        _movementSpeed.RefreshMovementSpeedModifiers(uid);
        Dirty(uid, component);
    }

    #endregion

    #region Conditionals

    private bool CanSprint(EntityUid uid, SprinterComponent component)
    {
        if (_gravity.IsWeightless(uid))
        {
            _popupSystem.PopupClient(Loc.GetString("no-sprint-while-weightless"), uid, uid, PopupType.Medium);
            return false;
        }

        var ev = new SprintAttemptEvent();
        RaiseLocalEvent(uid, ref ev);

        return !ev.Cancelled;
    }

    private void OnCuffableSprintAttempt(EntityUid uid, CuffableComponent component, ref SprintAttemptEvent args)
    {
        if (component.CanStillInteract)
            return;

        _popupSystem.PopupClient(Loc.GetString("no-sprint-while-restrained"), uid, uid, PopupType.Medium);
        args.Cancel();
    }

    private void OnStandingStateSprintAttempt(EntityUid uid, StandingStateComponent component, ref SprintAttemptEvent args)
    {
        if (!_standing.IsDown(uid))
            return;

        _popupSystem.PopupClient(Loc.GetString("no-sprint-while-lying"), uid, uid, PopupType.Medium);
        args.Cancel();
    }

    private void OnBuckleSprintAttempt(EntityUid uid, BuckleComponent component, ref SprintAttemptEvent args)
    {
        if (component.BuckledTo == null
            || !TryComp<SprinterComponent>(component.BuckledTo, out var sprinterComponent)
            || sprinterComponent.IsSprinting)
            return;

        args.Cancel();
    }

    private void OnMechPilotSprintAttempt(EntityUid uid, MechPilotComponent component, ref SprintAttemptEvent args)
    {
        if (!TryComp<SprinterComponent>(component.Mech, out var sprinterComponent)
            || sprinterComponent.IsSprinting)
            return;

        args.Cancel();
    }

    #endregion

    #region Misc.Handlers

    private void OnMobStateChangedEvent(EntityUid uid, SprinterComponent component, MobStateChangedEvent args)
    {
        if (!component.IsSprinting
            || args.NewMobState is not (MobState.Critical or MobState.Dead)) // Goobstation port: stop sprinting when going down, not when getting up
            return;

        ToggleSprint(args.Target, component, false, gracefulStop: false);
    }

    private void OnSleep(EntityUid uid, SprinterComponent component, ref SleepStateChangedEvent args)
    {
        if (!component.IsSprinting
            || !args.FellAsleep)
            return;

        ToggleSprint(uid, component, false, gracefulStop: false);
    }

    private void OnSprintDisablingEvent<T>(EntityUid uid, SprinterComponent component, ref T args) where T : notnull
    {
        if (!component.IsSprinting)
            return;

        ToggleSprint(uid, component, false, gracefulStop: false);
    }

    private void OnZombified(EntityUid uid, SprinterComponent component, ref EntityZombifiedEvent args) =>
        component.SprintSpeedMultiplier *= 0.5f; // We dont want super fast zombies do we?

    private void OnDisarm(EntityUid uid, SprinterComponent sprinter, ref DisarmedEvent args)
    {
        if (!sprinter.IsSprinting)
            return;

        _staminaSystem.TakeStaminaDamage(uid, sprinter.StaminaPenaltyOnShove);
        ToggleSprint(uid, sprinter, false, gracefulStop: true);
    }

    #endregion
}
