using Content.Shared._LuaM.Cyberware;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._LuaM.Cyberware;

public sealed partial class OpticalCamoSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStealthSystem _stealth = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OpticalCamoComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<OpticalCamoComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<OpticalCamoComponent, ActivateOpticalCamoEvent>(OnActivate);
        SubscribeLocalEvent<OpticalCamoComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
        SubscribeLocalEvent<OpticalCamoComponent, MeleeAttackEvent>(OnMeleeAttack);
        SubscribeLocalEvent<OpticalCamoComponent, ShotAttemptedEvent>(OnShotAttempted);
        SubscribeLocalEvent<OpticalCamoComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<OpticalCamoComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<OpticalCamoComponent, CyberpsychosisUseImplantsEvent>(OnPsychosisUse);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<OpticalCamoComponent>();
        while (query.MoveNext(out var uid, out var camo))
        {
            if (camo.Active && _timing.CurTime >= camo.EndTime)
                Deactivate((uid, camo));
        }
    }

    private void OnStartup(Entity<OpticalCamoComponent> ent, ref ComponentStartup args)
    {
        _actions.AddAction(ent, ref ent.Comp.ActionUid, ent.Comp.ActionProto);
        Dirty(ent);
    }

    private void OnShutdown(Entity<OpticalCamoComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        if (ent.Comp.Active)
            Deactivate(ent, playSound: false);

        _actions.RemoveAction(ent.Owner, ent.Comp.ActionUid);
    }

    private void OnActivate(Entity<OpticalCamoComponent> ent, ref ActivateOpticalCamoEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.Active)
        {
            args.Handled = true;
            Deactivate(ent);
            return;
        }

        // Left unhandled when blocked by other stealth, so the cooldown doesn't burn for nothing.
        if (Activate(ent))
            args.Handled = true;
        else
            _popup.PopupEntity(Loc.GetString(ent.Comp.BlockedPopup), ent, ent);
    }

    // The psycho turns the camo on as soon as the action is ready, the cooldown runs as if the player pressed it.
    private void OnPsychosisUse(Entity<OpticalCamoComponent> ent, ref CyberpsychosisUseImplantsEvent args)
    {
        if (ent.Comp.Active
            || !_actions.TryGetActionData(ent.Comp.ActionUid, out var action)
            || action.Cooldown is { } cooldown && cooldown.End > _timing.CurTime)
            return;

        if (Activate(ent))
            _actions.StartUseDelay(ent.Comp.ActionUid);
    }

    private bool Activate(Entity<OpticalCamoComponent> ent)
    {
        // Another source of stealth already hides the body, don't fight over it.
        if (HasComp<StealthComponent>(ent))
            return false;

        var stealth = EnsureComp<StealthComponent>(ent);
        _stealth.SetVisibility(ent, stealth.MinVisibility, stealth);

        ent.Comp.Active = true;
        ent.Comp.EndTime = _timing.CurTime + ent.Comp.Duration;
        Dirty(ent);

        _movement.RefreshMovementSpeedModifiers(ent);
        _audio.PlayPvs(ent.Comp.ActivateSound, ent);
        return true;
    }

    // The attack that breaks the camo hits harder. GetMeleeDamageEvent is raised on the attacker as well.
    private void OnGetMeleeDamage(Entity<OpticalCamoComponent> ent, ref GetMeleeDamageEvent args)
    {
        if (ent.Comp.Active)
            args.Damage *= ent.Comp.FirstHitMultiplier;
    }

    private void OnMeleeAttack(Entity<OpticalCamoComponent> ent, ref MeleeAttackEvent args)
    {
        Break(ent);
    }

    private void OnShotAttempted(Entity<OpticalCamoComponent> ent, ref ShotAttemptedEvent args)
    {
        if (!args.Cancelled)
            Break(ent);
    }

    // Only direct damage from someone or something, bleeding and suffocation don't count.
    private void OnDamageChanged(Entity<OpticalCamoComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageIncreased && args.Origin != null)
            Break(ent);
    }

    private void OnMobStateChanged(Entity<OpticalCamoComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            Break(ent);
    }

    private void Break(Entity<OpticalCamoComponent> ent)
    {
        if (!ent.Comp.Active)
            return;

        Deactivate(ent);
        _popup.PopupEntity(Loc.GetString(ent.Comp.BrokenPopup), ent, ent, PopupType.MediumCaution);
    }

    private void Deactivate(Entity<OpticalCamoComponent> ent, bool playSound = true)
    {
        if (!ent.Comp.Active)
            return;

        ent.Comp.Active = false;
        Dirty(ent);

        RemComp<StealthComponent>(ent);
        _movement.RefreshMovementSpeedModifiers(ent);

        if (playSound)
            _audio.PlayPvs(ent.Comp.DeactivateSound, ent);
    }
}
