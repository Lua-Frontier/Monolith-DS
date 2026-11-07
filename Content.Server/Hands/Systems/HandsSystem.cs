using System.Numerics;
using Content.Server.Inventory;
using Content.Server.Stack;
using Content.Server.Stunnable;
using Content.Shared._LuaM.Throwing; // LuaM
using Content.Shared.ActionBlocker;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems; // Shitmed Change
using Content.Shared._Shitmed.Body.Events; // Shitmed Change
using Content.Shared.CombatMode;
using Content.Shared.Damage.Systems;
using Content.Shared.Explosion;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Input;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Stacks;
using Content.Shared.Standing;
using Content.Shared.Throwing;
using Robust.Shared.Containers;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Hands.Systems
{
    public sealed partial class HandsSystem : SharedHandsSystem
    {
        [Dependency] private IGameTiming _timing = default!;
        [Dependency] private IRobustRandom _random = default!;
        [Dependency] private StackSystem _stackSystem = default!;
        [Dependency] private VirtualItemSystem _virtualItemSystem = default!;
        [Dependency] private ActionBlockerSystem _actionBlockerSystem = default!;
        [Dependency] private SharedTransformSystem _transformSystem = default!;
        [Dependency] private PullingSystem _pullingSystem = default!;
        [Dependency] private ThrowingSystem _throwingSystem = default!;
        [Dependency] private SharedBodySystem _bodySystem = default!; // Shitmed Change
        [Dependency] private PredictedThrowSystem _predictedThrow = default!; // LuaM
        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<HandsComponent, DisarmedEvent>(OnDisarmed, before: new[] {typeof(StunSystem), typeof(StaminaSystem)});

            SubscribeLocalEvent<HandsComponent, PullStartedMessage>(HandlePullStarted);
            SubscribeLocalEvent<HandsComponent, PullStoppedMessage>(HandlePullStopped);

            SubscribeLocalEvent<HandsComponent, BodyPartAddedEvent>(HandleBodyPartAdded);
            SubscribeLocalEvent<HandsComponent, BodyPartRemovedEvent>(HandleBodyPartRemoved);

            SubscribeLocalEvent<HandsComponent, ComponentGetState>(GetComponentState);

            SubscribeLocalEvent<HandsComponent, BeforeExplodeEvent>(OnExploded);
            SubscribeLocalEvent<HandsComponent, BodyPartEnabledEvent>(HandleBodyPartEnabled); // Shitmed Change
            SubscribeLocalEvent<HandsComponent, BodyPartDisabledEvent>(HandleBodyPartDisabled); // Shitmed Change

            SubscribeLocalEvent<HandsComponent, DropHandItemsEvent>(OnDropHandItems);
            // LuaM: ThrowItemInHand moved to the shared PredictedThrowSystem
        }

        private void GetComponentState(EntityUid uid, HandsComponent hands, ref ComponentGetState args)
        {
            args.State = new HandsComponentState(hands);
        }


        private void OnExploded(Entity<HandsComponent> ent, ref BeforeExplodeEvent args)
        {
            if (ent.Comp.DisableExplosionRecursion)
                return;

            foreach (var hand in ent.Comp.Hands.Values)
            {
                if (hand.HeldEntity is { } uid)
                    args.Contents.Add(uid);
            }
        }

        private void OnDisarmed(EntityUid uid, HandsComponent component, DisarmedEvent args)
        {
            if (args.Handled)
                return;

            // Break any pulls
            if (TryComp(uid, out PullerComponent? puller) && TryComp(puller.Pulling, out PullableComponent? pullable))
                _pullingSystem.TryStopPull(puller.Pulling.Value, pullable);

            var offsetRandomCoordinates = _transformSystem.GetMoverCoordinates(args.Target).Offset(_random.NextVector2(1f, 1.5f));
            if (!ThrowHeldItem(args.Target, offsetRandomCoordinates))
                return;

            args.PopupPrefix = "disarm-action-";

            args.Handled = true; // no shove/stun.
        }

        // Shitmed Change Start
        private void TryAddHand(EntityUid uid, HandsComponent component, Entity<BodyPartComponent> part, string slot)
        {
            if (part.Comp is null
                || part.Comp.PartType != BodyPartType.Hand)
                return;

            // If this annoys you, which it should.
            // Ping Smugleaf.
            var location = part.Comp.Symmetry switch
            {
                BodyPartSymmetry.None => HandLocation.Middle,
                BodyPartSymmetry.Left => HandLocation.Left,
                BodyPartSymmetry.Right => HandLocation.Right,
                _ => throw new ArgumentOutOfRangeException(nameof(part.Comp.Symmetry))
            };

            if (part.Comp.Enabled
                && _bodySystem.TryGetParentBodyPart(part, out var _, out var parentPartComp)
                && parentPartComp.Enabled)
                AddHand(uid, slot, location);
        }

        private void HandleBodyPartAdded(EntityUid uid, HandsComponent component, ref BodyPartAddedEvent args)
        {
            TryAddHand(uid, component, args.Part, args.Slot);
        }

        private void HandleBodyPartRemoved(EntityUid uid, HandsComponent component, ref BodyPartRemovedEvent args)
        {
            if (args.Part.Comp is null
                || args.Part.Comp.PartType != BodyPartType.Hand)
                return;
            RemoveHand(uid, args.Slot);
        }

        private void HandleBodyPartEnabled(EntityUid uid, HandsComponent component, ref BodyPartEnabledEvent args) =>
            TryAddHand(uid, component, args.Part, SharedBodySystem.GetPartSlotContainerId(args.Part.Comp.ParentSlot?.Id ?? string.Empty));

        private void HandleBodyPartDisabled(EntityUid uid, HandsComponent component, ref BodyPartDisabledEvent args)
        {
            if (TerminatingOrDeleted(uid)
                || args.Part.Comp is null
                || args.Part.Comp.PartType != BodyPartType.Hand)
                return;

            RemoveHand(uid, SharedBodySystem.GetPartSlotContainerId(args.Part.Comp.ParentSlot?.Id ?? string.Empty));
        }

        // Shitmed Change End

        #region pulling

        private void HandlePullStarted(EntityUid uid, HandsComponent component, PullStartedMessage args)
        {
            if (args.PullerUid != uid)
                return;

            if (TryComp<PullerComponent>(args.PullerUid, out var pullerComp) && !pullerComp.NeedsHands)
                return;

            if (!_virtualItemSystem.TrySpawnVirtualItemInHand(args.PulledUid, uid))
            {
                DebugTools.Assert("Unable to find available hand when starting pulling??");
            }
        }

        private void HandlePullStopped(EntityUid uid, HandsComponent component, PullStoppedMessage args)
        {
            if (args.PullerUid != uid)
                return;

            // Try find hand that is doing this pull.
            // and clear it.
            foreach (var hand in component.Hands.Values)
            {
                if (hand.HeldEntity == null
                    || !TryComp(hand.HeldEntity, out VirtualItemComponent? virtualItem)
                    || virtualItem.BlockingEntity != args.PulledUid)
                {
                    continue;
                }

                TryDrop(args.PullerUid, hand, handsComp: component);
                break;
            }
        }

        #endregion

        #region interactions

        /// <summary>
        /// Throw the player's currently held item.
        /// </summary>
        public bool ThrowHeldItem(EntityUid player, EntityCoordinates coordinates, float minDistance = 0.1f)
        {
            return _predictedThrow.ThrowHeldItem(player, coordinates, minDistance); // LuaM
        }

        private void OnDropHandItems(Entity<HandsComponent> entity, ref DropHandItemsEvent args)
        {
            var direction = TryComp(entity, out PhysicsComponent? comp) ? comp.LinearVelocity / 50 : Vector2.Zero;
            var dropAngle = _random.NextFloat(0.8f, 1.2f);

            var fellEvent = new FellDownEvent(entity);
            RaiseLocalEvent(entity, fellEvent, false);

            var worldRotation = TransformSystem.GetWorldRotation(entity).ToVec();
            foreach (var hand in entity.Comp.Hands.Values)
            {
                if (hand.HeldEntity is not EntityUid held)
                    continue;

                var throwAttempt = new FellDownThrowAttemptEvent(entity);
                RaiseLocalEvent(hand.HeldEntity.Value, ref throwAttempt);

                if (throwAttempt.Cancelled)
                    continue;

                if (!TryDrop(entity, hand, null, checkActionBlocker: false, handsComp: entity.Comp))
                    continue;

                _throwingSystem.TryThrow(held,
                    _random.NextAngle().RotateVec(direction / dropAngle + worldRotation / 50),
                    0.5f * dropAngle * _random.NextFloat(-0.9f, 1.1f),
                    entity, 0);
            }
        }

        #endregion
    }
}
