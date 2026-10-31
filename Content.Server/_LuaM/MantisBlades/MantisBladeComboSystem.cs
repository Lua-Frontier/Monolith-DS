using Content.Shared._LuaM.MantisBlades;
using Content.Shared.Actions;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Reflect;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._LuaM.MantisBlades;

/// <summary>
/// Gives the dash action and enables reflection while the user holds enough mantis blades.
/// </summary>
public sealed partial class MantisBladeComboSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MantisBladeComboComponent, GotEquippedHandEvent>(OnEquipped);
        SubscribeLocalEvent<MantisBladeComboComponent, GotUnequippedHandEvent>(OnUnequipped);
        SubscribeLocalEvent<MantisBladeComboUserComponent, ComponentShutdown>(OnUserShutdown);
    }

    private void OnEquipped(Entity<MantisBladeComboComponent> ent, ref GotEquippedHandEvent args)
    {
        Refresh(args.User);
    }

    private void OnUnequipped(Entity<MantisBladeComboComponent> ent, ref GotUnequippedHandEvent args)
    {
        SetReflect(ent, false);
        Refresh(args.User, args.Unequipped);
    }

    private void OnUserShutdown(Entity<MantisBladeComboUserComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.DashActionUid);
    }

    private void Refresh(EntityUid user, EntityUid? removed = null)
    {
        if (TerminatingOrDeleted(user))
            return;

        var blades = new List<Entity<MantisBladeComboComponent>>();
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (held != removed && TryComp<MantisBladeComboComponent>(held, out var combo))
                blades.Add((held, combo));
        }

        var active = blades.Count > 0 && blades.Count >= blades[0].Comp.RequiredBlades;
        foreach (var blade in blades)
        {
            SetReflect(blade, active);
        }

        if (active)
            AddDash(user, blades[0].Comp.DashAction);
        else
            RemoveDash(user);
    }

    private void SetReflect(Entity<MantisBladeComboComponent> blade, bool active)
    {
        if (!TryComp<ReflectComponent>(blade, out var reflect))
            return;

        var prob = active ? blade.Comp.ReflectProb : 0f;
        if (reflect.ReflectProb.Equals(prob))
            return;

        reflect.ReflectProb = prob;
        Dirty(blade, reflect);
    }

    private void AddDash(EntityUid user, EntProtoId actionProto)
    {
        var comp = EnsureComp<MantisBladeComboUserComponent>(user);
        if (comp.DashActionUid != null && !TerminatingOrDeleted(comp.DashActionUid.Value))
            return;

        comp.DashActionUid = _actions.AddAction(user, actionProto);

        if (comp.DashCooldown is { } cooldown && cooldown.End > _timing.CurTime)
            _actions.SetCooldown(comp.DashActionUid, cooldown.Start, cooldown.End);
    }

    private void RemoveDash(EntityUid user)
    {
        if (!TryComp<MantisBladeComboUserComponent>(user, out var comp) || comp.DashActionUid == null)
            return;

        if (_actions.TryGetActionData(comp.DashActionUid, out var action, false))
            comp.DashCooldown = action.Cooldown;

        _actions.RemoveAction(user, comp.DashActionUid);
        comp.DashActionUid = null;
    }
}
