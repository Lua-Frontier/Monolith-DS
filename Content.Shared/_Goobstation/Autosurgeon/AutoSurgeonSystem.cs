using System.Linq;
using Content.Shared._LuaM.Cyberware; // LuaM
using Content.Shared._Shitmed.BodyEffects;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle.Components; // LuaM
using Content.Shared.Damage.Components; // LuaM
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Forensics;
using Content.Shared.Implants; // LuaM
using Content.Shared.Implants.Components; // LuaM
using Content.Shared.Interaction; // LuaM
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups; // LuaM
using Content.Shared.Stacks; // LuaM
using Content.Shared.Tools.Systems; // LuaM
using Content.Shared.Whitelist; // LuaM
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers; // LuaM
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Shared._Goobstation.Autosurgeon;

// There might be some goidacode inside, I warned you.
// It should also maybe be in _Shitmed instead of here, but who cares.
public sealed partial class AutoSurgeonSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedContainerSystem _container = default!; // LuaM
    [Dependency] private SharedPopupSystem _popup = default!; // LuaM
    [Dependency] private EntityWhitelistSystem _whitelist = default!; // LuaM
    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private INetManager _netManager = default!;
    [Dependency] private ISerializationManager _serializationManager = default!;
    [Dependency] private SharedStackSystem _stack = default!; // LuaM
    [Dependency] private SharedToolSystem _tool = default!; // LuaM

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AutoSurgeonComponent, ItemToggleActivateAttemptEvent>(OnActivated);
        SubscribeLocalEvent<AutoSurgeonComponent, AutoSurgeonDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<AutoSurgeonComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<AutoSurgeonComponent, InteractUsingEvent>(OnInteractUsing); // LuaM
        SubscribeLocalEvent<AutoSurgeonComponent, AutoSurgeonDisassembleDoAfterEvent>(OnDisassembleDoAfter); // LuaM
    }

    // LuaM-start
    private void OnInteractUsing(Entity<AutoSurgeonComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !ent.Comp.Used)
            return;

        args.Handled = _tool.UseTool(args.Used,
            args.User,
            ent,
            ent.Comp.DisassembleTime,
            ent.Comp.DisassembleQuality,
            new AutoSurgeonDisassembleDoAfterEvent());
    }

    private void OnDisassembleDoAfter(Entity<AutoSurgeonComponent> ent, ref AutoSurgeonDisassembleDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || !ent.Comp.Used)
            return;

        args.Handled = true;

        if (_netManager.IsClient)
            return;

        foreach (var (material, amount) in ent.Comp.DisassembleMaterials)
        {
            var stack = SpawnNextToOrDrop(material, ent);
            _stack.SetCount(stack, amount);
        }

        QueueDel(ent);
    }
    // LuaM-end

    private void OnActivated(Entity<AutoSurgeonComponent> ent, ref ItemToggleActivateAttemptEvent args)
    {
        _audio.Stop(ent.Comp.ActiveSound);
        args.Cancelled = true;

        if (ent.Comp.Used || args.User == null)
            return;

        // LuaM-start: whoever is buckled to the autosurgeon gets the surgery, otherwise the one who turned it on
        var target = args.User.Value;
        if (TryComp<StrapComponent>(ent, out var strap)
            && strap.BuckledEntities.FirstOrDefault() is { Valid: true } buckled)
            target = buckled;
        // LuaM-end

        if (!_doAfter.TryStartDoAfter(new DoAfterArgs(
                EntityManager,
                ent.Owner,
                ent.Comp.DoAfterTime,
                new AutoSurgeonDoAfterEvent(),
                ent.Owner,
                target, // LuaM: args.User > target
                ent.Owner)
            {
                BreakOnMove = true,
                DistanceThreshold = 0.1f,
                MovementThreshold = 0.1f,
            }))
            return;

        var ev = new TransferDnaEvent { Donor = args.User.Value, Recipient = ent };
        RaiseLocalEvent(args.User.Value, ref ev);

        if (_netManager.IsClient) // Fuck sound networking
            return;

        var sound = _audio.PlayPvs(ent.Comp.Sound, ent);
        if (sound.HasValue)
            ent.Comp.ActiveSound = sound.Value.Entity;
    }

    private void OnDoAfter(Entity<AutoSurgeonComponent> ent, ref AutoSurgeonDoAfterEvent args)
    {
        if (args.Cancelled
        || ent.Comp.Used
        || args.Target == null)
        {
            _audio.Stop(ent.Comp.ActiveSound);
            return;
        }

        // LuaM-start: autosurgeons that install a subdermal implant
        if (ent.Comp.Implant is { } implantProto)
        {
            _audio.Stop(ent.Comp.ActiveSound);

            if (_netManager.IsClient)
                return;

            if (TryComp<ImplantedComponent>(args.Target, out var implanted)
                && implanted.ImplantContainer.ContainedEntities.Any(implant => Prototype(implant)?.ID == implantProto.Id))
            {
                _popup.PopupEntity(Loc.GetString("autosurgeon-implant-already"), args.Target.Value, args.Target.Value);
                return;
            }

            var newImplant = Spawn(implantProto, Transform(args.Target.Value).Coordinates);
            if (!TryComp<SubdermalImplantComponent>(newImplant, out var implantComp))
            {
                Del(newImplant);
                return;
            }

            // Same checks as an implanter: the implant's whitelist and anything that forbids it, like a mindshield on a rev head.
            var attempt = new AddImplantAttemptEvent(args.Target.Value, args.Target.Value, newImplant, ent.Owner);
            if (_whitelist.IsWhitelistPassOrNull(implantComp.Whitelist, args.Target.Value)
                && _whitelist.IsBlacklistFailOrNull(implantComp.Blacklist, args.Target.Value))
                RaiseLocalEvent(args.Target.Value, attempt);
            else
                attempt.Cancel();

            if (attempt.Cancelled)
            {
                Del(newImplant);
                _popup.PopupEntity(Loc.GetString("autosurgeon-implant-failed"), args.Target.Value, args.Target.Value);
                return;
            }

            var implantedComp = EnsureComp<ImplantedComponent>(args.Target.Value);
            implantComp.ImplantedEntity = args.Target.Value;
            _container.Insert(newImplant, implantedComp.ImplantContainer);

            if (ent.Comp.OneTimeUse)
                ent.Comp.Used = true;
            Dirty(ent);
            RaiseInstalled(args.Target.Value); // LuaM
            return;
        }

        // Autosurgeons that put the same organ into several parts, like a mantis blade into each arm
        if (ent.Comp.OrganForEachPart is { } organProto)
        {
            _audio.Stop(ent.Comp.ActiveSound);

            if (_netManager.IsClient)
                return;

            var target = args.Target.Value;
            var installed = false;
            foreach (var (partId, partComp) in _body.GetBodyChildrenOfType(target, ent.Comp.TargetBodyPart).ToList())
            {
                var organ = Spawn(organProto, Transform(target).Coordinates);
                if (!TryComp<OrganComponent>(organ, out var organComp)
                    || !_body.InsertOrgan(partId, organ, organComp.SlotId, partComp, organComp))
                {
                    Del(organ);
                    continue;
                }

                installed = true;
            }

            if (!installed)
            {
                _popup.PopupEntity(Loc.GetString("autosurgeon-implant-already"), target, target);
                return;
            }

            if (ent.Comp.OneTimeUse)
                ent.Comp.Used = true;
            Dirty(ent);
            RaiseInstalled(args.Target.Value); // LuaM
            return;
        }
        // LuaM-end

        var isBodyPart = ent.Comp.TargetOrgan == null;

        // Handle replacing the part
        if (ent.Comp.NewPartProto != null)
        {
            var parent = _body.GetBodyChildrenOfType(args.Target.Value, ent.Comp.TargetBodyPart, symmetry: ent.Comp.TargetBodyPartSymmetry)
                .FirstOrDefault()
                .Id;

            if (!parent.Valid
            || !TryComp<BodyPartComponent>(parent, out var parentComp))
            {
                _audio.Stop(ent.Comp.ActiveSound);
                return;
            }

            var newPart = Spawn(ent.Comp.NewPartProto, Transform(args.Target.Value).Coordinates);

            if (isBodyPart)
            {
                if (!TryComp<BodyPartComponent>(newPart, out var newPartComp)
                || !parentComp.Children.ContainsKey(_body.GetSlotFromBodyPart(newPartComp))) // why is there no method for this
                {
                    Del(newPart);
                    _audio.Stop(ent.Comp.ActiveSound);
                    return;
                }

                var oldPart = _body.GetBodyChildrenOfType(args.Target.Value, newPartComp.PartType, symmetry: newPartComp.Symmetry)
                    .FirstOrDefault()
                    .Id;

                if (oldPart.Valid)
                    _body.DetachPart(parent, _body.GetSlotFromBodyPart(newPartComp), oldPart);

                _body.AttachPart(parent, _body.GetSlotFromBodyPart(newPartComp), newPart);
            }
            else
            {
                if (!TryComp<OrganComponent>(newPart, out var newOrganComp)
                || !_body.CanInsertOrgan(parent, newOrganComp.SlotId))
                {
                    Del(newPart);
                    _audio.Stop(ent.Comp.ActiveSound);
                    return;
                }

                // LuaM-start: the organ goes into its own slot, not into the first free one
                if (!_body.InsertOrgan(parent, newPart, newOrganComp.SlotId))
                {
                    if (_container.TryGetContainer(parent, SharedBodySystem.GetOrganContainerId(newOrganComp.SlotId), out var slotContainer)
                        && slotContainer.ContainedEntities.FirstOrDefault() is { Valid: true } oldOrgan)
                        _body.RemoveOrgan(oldOrgan);

                    if (!_body.InsertOrgan(parent, newPart, newOrganComp.SlotId))
                    {
                        Del(newPart);
                        _audio.Stop(ent.Comp.ActiveSound);
                        return;
                    }
                }
                // LuaM-end
            }

            _audio.Stop(ent.Comp.ActiveSound);

            if (ent.Comp.OneTimeUse)
                ent.Comp.Used = true;
            Dirty(ent);
            RaiseInstalled(args.Target.Value); // LuaM
            return;
        }

        // If we didn't replace it, then we upgrade it.
        var part = isBodyPart
            ? _body.GetBodyChildrenOfType(args.Target.Value, ent.Comp.TargetBodyPart, symmetry: ent.Comp.TargetBodyPartSymmetry)
                .FirstOrDefault()
                .Id
            : _body.GetBodyOrgans(args.Target)
                .FirstOrDefault(organ => organ.Component.SlotId == ent.Comp.TargetOrgan)
                .Id;

        if (!part.Valid)
        {
            _audio.Stop(ent.Comp.ActiveSound);
            return;
        }

        var addedToPart = AddComponents(part, ent.Comp.ComponentsToPart); // if none were actually added the part is probably already upgraded

        if (addedToPart != null // null indicates there were no components to add in the first place, so it's fine
        && !addedToPart.Any())
        {
            _audio.Stop(ent.Comp.ActiveSound);
            return;
        }

        if (ent.Comp.TargetOrgan == null)
            HandleBodyPart(args.Target.Value, part, ent.Comp.ComponentsToUser);
        else
            HandleOrgan(args.Target.Value, part, ent.Comp.ComponentsToUser);

        // LuaM-start: humanity cost of the upgrade, stored on the part so a repeated upgrade doesn't count twice
        if (ent.Comp.HumanityCost > 0 && Prototype(ent)?.ID is { } upgradeId)
            EnsureComp<CyberwareHumanityComponent>(part).Upgrades[upgradeId] = ent.Comp.HumanityCost;
        // LuaM-end

        _audio.Stop(ent.Comp.ActiveSound);

        if (ent.Comp.OneTimeUse)
            ent.Comp.Used = true;
        Dirty(ent);
        RaiseInstalled(args.Target.Value); // LuaM
    }

    // LuaM-start
    private void RaiseInstalled(EntityUid target)
    {
        if (_netManager.IsClient)
            return;

        var ev = new CyberwareInstalledEvent();
        RaiseLocalEvent(target, ref ev);
    }
    // LuaM-end

    private void HandleBodyPart(EntityUid user, EntityUid part, ComponentRegistry? comps)
    {
        if (!TryComp<BodyPartComponent>(part, out var partComp)
        || comps == null)
            return;

        var addedToOnAdd = new ComponentRegistry();
        foreach (var (name, data) in comps)
        {
            if (partComp.OnAdd != null)
            {
                if (partComp.OnAdd.TryAdd(name, data))
                    addedToOnAdd.Add(name, data);
            }
            else
            {
                partComp.OnAdd = comps;
                addedToOnAdd = comps;
            }
        }

        var addedToUser = AddComponents(user, addedToOnAdd);
        if (addedToUser == null)
            return;

        var partEffectComp = EnsureComp<BodyPartEffectComponent>(part);
        foreach (var (name, data) in addedToUser)
            partEffectComp.Active.TryAdd(name, data);
    }

    private void HandleOrgan(EntityUid user, EntityUid organ, ComponentRegistry? comps)
    {
        if (!TryComp<OrganComponent>(organ, out var organComp)
        || comps == null)
            return;

        var addedToOnAdd = new ComponentRegistry();
        foreach (var (name, data) in comps)
        {
            if (organComp.OnAdd != null)
            {
                if (organComp.OnAdd.TryAdd(name, data))
                    addedToOnAdd.Add(name, data);
            }
            else
            {
                organComp.OnAdd = comps;
                addedToOnAdd = comps;
            }
        }

        var addedToUser = AddComponents(user, addedToOnAdd);
        if (addedToUser == null)
            return;

        var organEffectComp = EnsureComp<OrganEffectComponent>(organ);
        foreach (var (name, data) in addedToUser)
            organEffectComp.Active.TryAdd(name, data);
    }

    private void OnExamined(Entity<AutoSurgeonComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(ent.Comp.Used ? Loc.GetString("gun-cartridge-spent") : Loc.GetString("gun-cartridge-unspent")); // Yes gun locale, and?

        if (ent.Comp.Used) // LuaM
            args.PushMarkup(Loc.GetString("autosurgeon-disassemble-examine"));
    }

    private ComponentRegistry? AddComponents(EntityUid ent, ComponentRegistry? comps) // Returns actually added components
    {
        if (comps == null)
            return null;

        var result = new ComponentRegistry();

        foreach (var (name, data) in comps)
        {
            var newComp = (Component)_componentFactory.GetComponent(name);
            if (HasComp(ent, newComp.GetType()))
            {
                // LuaM: implanted damage protection replaces the same one from a trait instead of being skipped
                if (newComp is DamageProtectionBuffComponent && MergeDamageProtection(ent, newComp, data))
                    result.Add(name, data);
                continue;
            }

            newComp.Owner = ent;
            object? temp = newComp;
            _serializationManager.CopyTo(data.Component, ref temp);
            AddComp(ent, (Component)temp!);

            result.Add(name, data);
        }

        return result;
    }

    // LuaM-start
    /// <summary>
    /// Writes the implant's damage modifiers into the protection the user already has.
    /// A modifier under the same key, like the dermal armor trait, gets replaced.
    /// </summary>
    private bool MergeDamageProtection(EntityUid ent, Component newComp, EntityPrototype.ComponentRegistryEntry data)
    {
        if (!TryComp<DamageProtectionBuffComponent>(ent, out var existing))
            return false;

        object? temp = newComp;
        _serializationManager.CopyTo(data.Component, ref temp);
        if (temp is not DamageProtectionBuffComponent implant)
            return false;

        foreach (var (key, modifier) in implant.Modifiers)
            existing.Modifiers[key] = modifier;

        Dirty(ent, existing);
        return true;
    }
    // LuaM-end
}
