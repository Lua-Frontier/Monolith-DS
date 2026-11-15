using Content.Shared.DoAfter;
using Content.Shared.Gravity;
using Content.Shared.Input;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Traits.Assorted; //Mono: Wheelchair user check
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;
using Robust.Shared.Serialization;

namespace Content.Shared._White.Standing;

public abstract partial class SharedLayingDownSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private StandingStateSystem _standing = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedGravitySystem _gravity = default!;
    public override void Initialize()
    {
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.ToggleStanding, InputCmdHandler.FromDelegate(ToggleStanding, handle: false, outsidePrediction: false)) // LuaM: predicted input instead of a network event
            .Register<SharedLayingDownSystem>();

        SubscribeLocalEvent<StandingStateComponent, StandingUpDoAfterEvent>(OnStandingUpDoAfter);
        SubscribeLocalEvent<LayingDownComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovementSpeed);
        SubscribeLocalEvent<LayingDownComponent, RefreshWeightlessModifiersEvent>(OnRefreshWeightlessModifier);
        SubscribeLocalEvent<LayingDownComponent, EntParentChangedMessage>(OnParentChanged);

    }

    public override void Shutdown()
    {
        base.Shutdown();

        CommandBinds.Unregister<SharedLayingDownSystem>();
    }

    // LuaM: runs on the client as a predicted input and on the server, so lying down no longer snaps the camera back
    private void ToggleStanding(ICommonSession? session)
    {
        if (session?.AttachedEntity is not { } uid ||
            !HasComp<LayingDownComponent>(uid) ||
            !CanLieDown(uid)) // Mono
        {
            return;
        }

        if (HasComp<LegsParalyzedComponent>(uid)) //Mono: Stop wheelchair user trait from standing
            return;

        // TODO: Wizard
        //if (HasComp<FrozenComponent>(uid))
        //   return;

        if (!TryComp(uid, out StandingStateComponent? standing) ||
            !TryComp(uid, out LayingDownComponent? layingDown))
        {
            return;
        }

        if (HasComp<KnockedDownComponent>(uid) || !_mobState.IsAlive(uid))
            return;

        if (_standing.IsDown(uid, standing))
            TryStandUp(uid, layingDown, standing);
        else
            TryLieDown(uid, layingDown, standing);
    }

    private void OnStandingUpDoAfter(EntityUid uid, StandingStateComponent component, StandingUpDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || HasComp<KnockedDownComponent>(uid) ||
            _mobState.IsIncapacitated(uid) || !_standing.Stand(uid))
        {
            component.CurrentState = StandingState.Lying;
            return;
        }

        component.CurrentState = StandingState.Standing;
    }

    private void OnRefreshMovementSpeed(EntityUid uid, LayingDownComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (_standing.IsDown(uid))
            args.ModifySpeed(component.SpeedModify, component.SpeedModify);
        else
            args.ModifySpeed(1f, 1f);
    }

    // Mono edit
    private void OnRefreshWeightlessModifier(EntityUid uid, LayingDownComponent component, ref RefreshWeightlessModifiersEvent args)
    {
        if (!_standing.IsDown(uid))
            return;
        args.ModifyAcceleration(1f, 0.10f);
    }

    private void OnParentChanged(EntityUid uid, LayingDownComponent component, EntParentChangedMessage args)
    {
        // If the entity is not on a grid, try to make it stand up to avoid issues
        if (!TryComp<StandingStateComponent>(uid, out var standingState)
            || standingState.CurrentState is StandingState.Standing
            || CanLieDown(uid) // Mono
            || HasComp<LegsParalyzedComponent>(uid)) // Mono
        {
            return;
        }

        _standing.Stand(uid, standingState);
    }

    // LuaM-start
    public virtual bool ShouldAutoGetUp(EntityUid uid, LayingDownComponent layingDown)
    {
        return layingDown.AutoGetUp;
    }
    // LuaM-end

    public bool TryStandUp(EntityUid uid, LayingDownComponent? layingDown = null, StandingStateComponent? standingState = null)
    {
        if (!Resolve(uid, ref standingState, false) ||
            !Resolve(uid, ref layingDown, false) ||
            standingState.CurrentState is not StandingState.Lying ||
            !_mobState.IsAlive(uid) ||
            TerminatingOrDeleted(uid))
        {
            return false;
        }

        var args = new DoAfterArgs(EntityManager, uid, layingDown.StandingUpTime, new StandingUpDoAfterEvent(), uid)
        {
            BreakOnDamage = true,
            BreakOnHandChange = false,
            RequireCanInteract = false,
            MultiplyDelay = false, // Goobstation
        };

        if (!_doAfter.TryStartDoAfter(args))
            return false;

        standingState.CurrentState = StandingState.GettingUp;
        return true;
    }

    public bool TryLieDown(EntityUid uid, LayingDownComponent? layingDown = null, StandingStateComponent? standingState = null, DropHeldItemsBehavior behavior = DropHeldItemsBehavior.NoDrop)
    {
        if (!Resolve(uid, ref standingState, false) ||
            !Resolve(uid, ref layingDown, false) ||
            standingState.CurrentState is not StandingState.Standing ||
            !CanLieDown(uid)) // Mono
        {
            if (behavior == DropHeldItemsBehavior.AlwaysDrop)
            {
                var ev = new DropHandItemsEvent();
                RaiseLocalEvent(uid, ref ev, false);
            }
            return false;
        }

        _standing.Down(uid, true, behavior != DropHeldItemsBehavior.NoDrop, false, standingState);
        return true;
    }

    // Mono
    private bool CanLieDown(EntityUid uid)
    {
        return _gravity.EntityOnGravitySupportingGridOrMap(uid);
    }
}

[Serializable, NetSerializable]
public sealed partial class StandingUpDoAfterEvent : SimpleDoAfterEvent;

public enum DropHeldItemsBehavior : byte
{
    NoDrop,
    DropIfStanding,
    AlwaysDrop
}
