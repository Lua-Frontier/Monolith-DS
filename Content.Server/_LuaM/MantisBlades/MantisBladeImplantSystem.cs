using System.Diagnostics.CodeAnalysis;
using Content.Server._Goobstation.PairedExtendable;
using Content.Shared._LuaM.MantisBlades;
using Content.Shared.Actions;
using Content.Shared.Body.Events;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Emp;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;

namespace Content.Server._LuaM.MantisBlades;

/// <summary>
/// Handles mantis blade arm implants: one action extends or retracts the blades of every arm at once.
/// </summary>
public sealed partial class MantisBladeImplantSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private PairedExtendableSystem _pairedExtendable = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MantisBladeImplantComponent, OrganAddedToBodyEvent>(OnAddedToBody);
        SubscribeLocalEvent<MantisBladeImplantComponent, OrganRemovedFromBodyEvent>(OnRemovedFromBody);
        SubscribeLocalEvent<MantisBladeImplantComponent, ComponentShutdown>(OnImplantShutdown);
        SubscribeLocalEvent<MantisBladeImplantComponent, EmpPulseEvent>(OnEmpPulse);
        SubscribeLocalEvent<MantisBladeImplantUserComponent, ToggleMantisBladesEvent>(OnToggle);
        SubscribeLocalEvent<MantisBladeImplantUserComponent, ComponentShutdown>(OnUserShutdown);
    }

    private void OnAddedToBody(Entity<MantisBladeImplantComponent> ent, ref OrganAddedToBodyEvent args)
    {
        var user = EnsureComp<MantisBladeImplantUserComponent>(args.Body);
        if (user.ActionUid == null || TerminatingOrDeleted(user.ActionUid.Value))
            user.ActionUid = _actions.AddAction(args.Body, ent.Comp.ActionProto);
    }

    private void OnRemovedFromBody(Entity<MantisBladeImplantComponent> ent, ref OrganRemovedFromBodyEvent args)
    {
        RetractBlade(ent);

        if (TerminatingOrDeleted(args.OldBody))
            return;

        // Keep the action while another arm still has a blade.
        foreach (var other in _body.GetBodyOrganEntityComps<MantisBladeImplantComponent>(args.OldBody))
        {
            if (other.Owner != ent.Owner && other.Comp2.Body == args.OldBody)
                return;
        }

        RemComp<MantisBladeImplantUserComponent>(args.OldBody);
    }

    private void OnImplantShutdown(Entity<MantisBladeImplantComponent> ent, ref ComponentShutdown args)
    {
        RetractBlade(ent);
    }

    private void OnUserShutdown(Entity<MantisBladeImplantUserComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionUid);
    }

    private void OnEmpPulse(Entity<MantisBladeImplantComponent> ent, ref EmpPulseEvent args)
    {
        args.Affected = true;
        args.Disabled = true;
    }

    private void OnToggle(Entity<MantisBladeImplantUserComponent> ent, ref ToggleMantisBladesEvent args)
    {
        var implants = _body.GetBodyOrganEntityComps<MantisBladeImplantComponent>(ent.Owner);
        if (implants.Count == 0)
            return;

        // If any blade is out, retract them all.
        var anyExtended = false;
        foreach (var implant in implants)
        {
            if (implant.Comp1.BladeUid is { } blade && !TerminatingOrDeleted(blade))
                anyExtended = true;
            else
                implant.Comp1.BladeUid = null;
        }

        if (anyExtended)
        {
            foreach (var implant in implants)
            {
                RetractBlade((implant.Owner, implant.Comp1));
            }

            _audio.PlayPvs(implants[0].Comp1.RetractSound, ent);
            args.Handled = true;
            return;
        }

        var extended = false;
        var disabled = false;
        foreach (var implant in implants)
        {
            if (HasComp<EmpDisabledComponent>(implant.Owner))
            {
                disabled = true;
                continue;
            }

            if (!TryGetHand(implant.Owner, out var hand))
                continue;

            // The blade pushes out whatever is held in that hand.
            DropHeld(ent.Owner, hand.Value);

            if (_pairedExtendable.ToggleExtendable(ent.Owner, implant.Comp1.BladeProto.Id, hand.Value, out var newBlade)
                && newBlade != null)
            {
                implant.Comp1.BladeUid = newBlade;
                extended = true;
            }
        }

        if (extended)
        {
            _audio.PlayPvs(implants[0].Comp1.ExtendSound, ent);
            args.Handled = true;
        }
        else if (disabled)
        {
            _popup.PopupEntity(Loc.GetString("mantis-blade-disabled-emp"), ent, ent);
        }
    }

    private void DropHeld(EntityUid user, HandLocation location)
    {
        foreach (var hand in _hands.EnumerateHands(user))
        {
            if (hand.Location == location && hand.HeldEntity != null)
            {
                _hands.TryDrop(user, hand, checkActionBlocker: false);
                return;
            }
        }
    }

    private void RetractBlade(Entity<MantisBladeImplantComponent> ent)
    {
        if (ent.Comp.BladeUid is { } blade && !TerminatingOrDeleted(blade))
            Del(blade);

        ent.Comp.BladeUid = null;
    }

    /// <summary>
    /// Picks the hand on the same side as the arm the implant sits in.
    /// </summary>
    private bool TryGetHand(EntityUid implant, [NotNullWhen(true)] out HandLocation? hand)
    {
        hand = null;
        if (!_container.TryGetContainingContainer((implant, null, null), out var container)
            || !TryComp<BodyPartComponent>(container.Owner, out var part))
            return false;

        hand = part.Symmetry switch
        {
            BodyPartSymmetry.Left => HandLocation.Left,
            BodyPartSymmetry.Right => HandLocation.Right,
            _ => HandLocation.Middle,
        };
        return true;
    }
}
