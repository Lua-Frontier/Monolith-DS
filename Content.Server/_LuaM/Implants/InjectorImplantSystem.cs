using Content.Server.Body.Systems;
using Content.Shared._LuaM.Implants;
using Content.Shared.Actions;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._LuaM.Implants;

public sealed class InjectorImplantSystem : EntitySystem
{
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InjectorImplantComponent, UseInjectorImplantEvent>(OnUse);
        SubscribeLocalEvent<InjectorImplantComponent, ImplantRelayEvent<MobStateChangedEvent>>(OnMobStateChanged);
    }

    private void OnUse(EntityUid uid, InjectorImplantComponent component, UseInjectorImplantEvent args)
    {
        if (args.Handled || !TryComp<SubdermalImplantComponent>(uid, out var implant) || implant.ImplantedEntity is not { } user)
            return;

        // The action system takes the charge itself.
        args.Handled = Inject((uid, component), user);
    }

    private void OnMobStateChanged(EntityUid uid, InjectorImplantComponent component, ImplantRelayEvent<MobStateChangedEvent> args)
    {
        if (!component.InjectOnCritical || args.Event.NewMobState != MobState.Critical)
            return;

        if (_timing.CurTime < component.NextAutoInject)
            return;

        if (!TryComp<SubdermalImplantComponent>(uid, out var implant) || implant.Action is not { } action)
            return;

        var ent = new Entity<InjectorImplantComponent>(uid, component);

        if (_actions.GetCharges(action) is not > 0)
            return;

        if (!Inject(ent, args.Event.Target))
            return;

        component.NextAutoInject = _timing.CurTime + component.AutoInjectCooldown;
        _actions.RemoveCharges(action, 1);
        UpdateCharges(ent, action);
    }

    private bool Inject(Entity<InjectorImplantComponent> ent, EntityUid user)
    {
        if (!_bloodstream.TryAddToChemicals(user, ent.Comp.Solution.Clone()))
            return false;

        _audio.PlayPvs(ent.Comp.InjectSound, user);
        _popup.PopupEntity(Loc.GetString("injector-implant-used"), user, user);
        return true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<InjectorImplantComponent, SubdermalImplantComponent>();
        while (query.MoveNext(out var uid, out var injector, out var implant))
        {
            if (implant.Action is { } action)
                UpdateCharges((uid, injector), action);
        }
    }

    private void UpdateCharges(Entity<InjectorImplantComponent> ent, EntityUid action)
    {
        if (!_actions.TryGetActionData(action, out var data) || data.Charges is not { } charges || data.MaxCharges is not { } max)
            return;

        var now = _timing.CurTime;

        if (charges >= max)
        {
            ent.Comp.NextRecharge = null;
            return;
        }

        ent.Comp.NextRecharge ??= now + ent.Comp.RechargeTime;

        if (now >= ent.Comp.NextRecharge)
        {
            _actions.AddCharges(action, 1);
            _actions.SetEnabled(action, true);
            _actions.ClearCooldown(action);
            charges++;
            ent.Comp.NextRecharge = charges < max ? now + ent.Comp.RechargeTime : null;
            return;
        }

        // Show the time until the next charge on the hotbar once everything is spent.
        if (charges <= 0 && data.Cooldown?.End != ent.Comp.NextRecharge)
            _actions.SetCooldown(action, now, ent.Comp.NextRecharge.Value);
    }
}
