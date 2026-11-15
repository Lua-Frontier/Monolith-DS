using Content.Server._LuaM.Hearing;
using Content.Server.Body.Systems;
using Content.Shared._LuaM.Cyberware;
using Content.Shared._LuaM.Weapons;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._LuaM.Cyberware;

public sealed partial class BerserkHeartSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private DeafnessSystem _deafness = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const string DeafSource = "BerserkHeart";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BerserkHeartComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BerserkHeartComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<BerserkHeartComponent, ToggleBerserkHeartEvent>(OnToggle);
        SubscribeLocalEvent<BerserkHeartComponent, GetMeleeDamageEvent>(OnGetMeleeDamage);
        SubscribeLocalEvent<BerserkHeartComponent, MeleeUserHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<BerserkHeartComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<BerserkHeartComponent, CyberpsychosisUseImplantsEvent>(OnPsychosisUse);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<BerserkHeartComponent>();
        while (query.MoveNext(out var uid, out var heart))
        {
            if (!heart.Active)
            {
                UpdateCooldown(heart, now);
                continue;
            }

            if (!heart.Frenzy && now >= heart.ActivatedAt + heart.FrenzyAfter)
            {
                heart.Frenzy = true;
                Dirty(uid, heart);
                _popup.PopupEntity(Loc.GetString("berserk-heart-frenzy"), uid, uid, PopupType.LargeCaution);
            }

            if (now < heart.NextDamage)
                continue;

            heart.NextDamage = now + heart.DamageInterval;

            // The heart wears the body down from the inside, it does not cut it open: no bleeding from this.
            var damage = heart.SelfDamage;
            _bloodstream.WithoutBleeding(uid,
                () => _damageable.TryChangeDamage(uid, damage, ignoreResistances: true, interruptsDoAfters: false, canSever: false));
        }
    }

    // The actions system clears the action cooldown right after the toggle is handled,
    // so the heart keeps its own and puts it back on the hotbar.
    private void UpdateCooldown(BerserkHeartComponent heart, TimeSpan now)
    {
        if (heart.CooldownTime is not { } cooldown)
            return;

        if (now >= cooldown.End)
        {
            heart.CooldownTime = null;
            return;
        }

        if (_actions.TryGetActionData(heart.ActionUid, out var action) && action.Cooldown?.End != cooldown.End)
            _actions.SetCooldown(heart.ActionUid, cooldown.Start, cooldown.End);
    }

    private void OnStartup(Entity<BerserkHeartComponent> ent, ref ComponentStartup args)
    {
        _actions.AddAction(ent, ref ent.Comp.ActionUid, ent.Comp.ActionProto);
        Dirty(ent);
    }

    private void OnShutdown(Entity<BerserkHeartComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        Deactivate(ent, playSound: false);
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionUid);
    }

    private void OnToggle(Entity<BerserkHeartComponent> ent, ref ToggleBerserkHeartEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (ent.Comp.Active)
            Deactivate(ent);
        else
            Activate(ent);
    }

    // The psycho starts the heart, but never stops it.
    private void OnPsychosisUse(Entity<BerserkHeartComponent> ent, ref CyberpsychosisUseImplantsEvent args)
    {
        if (!ent.Comp.Active)
            Activate(ent);
    }

    private void Activate(Entity<BerserkHeartComponent> ent)
    {
        if (ent.Comp.Active || !_mobState.IsAlive(ent))
            return;

        // Also stops the cyberpsychosis from starting a heart that is still recovering.
        if (ent.Comp.CooldownTime is { } cooldown && _timing.CurTime < cooldown.End)
            return;

        var comp = ent.Comp;
        comp.Active = true;
        comp.Frenzy = false;
        comp.ActivatedAt = _timing.CurTime;
        comp.NextDamage = _timing.CurTime + comp.DamageInterval;

        comp.OldCritThreshold = RaiseThreshold(ent, MobState.Critical, comp.CritThreshold);
        comp.OldDeadThreshold = RaiseThreshold(ent, MobState.Dead, comp.DeadThreshold);

        _deafness.AddSource(ent, DeafSource, comp.MuffledSoundVolume);

        Dirty(ent);
        _movement.RefreshMovementSpeedModifiers(ent);

        _audio.PlayPvs(comp.ActivateSound, ent);
        _popup.PopupEntity(Loc.GetString("berserk-heart-on"), ent, ent, PopupType.MediumCaution);
    }

    private void Deactivate(Entity<BerserkHeartComponent> ent, bool playSound = true)
    {
        if (!ent.Comp.Active)
            return;

        // Off first: putting the thresholds back can drop the user into crit, which comes back here.
        var comp = ent.Comp;
        comp.Active = false;
        comp.Frenzy = false;
        comp.CooldownTime = (_timing.CurTime, _timing.CurTime + comp.Cooldown);
        Dirty(ent);

        if (comp.OldCritThreshold is { } crit)
            _thresholds.SetMobStateThreshold(ent, crit, MobState.Critical);

        if (comp.OldDeadThreshold is { } dead)
            _thresholds.SetMobStateThreshold(ent, dead, MobState.Dead);

        comp.OldCritThreshold = null;
        comp.OldDeadThreshold = null;

        _deafness.RemoveSource(ent, DeafSource);

        _movement.RefreshMovementSpeedModifiers(ent);

        if (!playSound)
            return;

        _audio.PlayPvs(comp.DeactivateSound, ent);
        _popup.PopupEntity(Loc.GetString("berserk-heart-off"), ent, ent, PopupType.Medium);
    }

    /// <summary>
    /// Raises the threshold of the state to the given value and returns the old one to restore later.
    /// Returns null if the body has no such state or already has more.
    /// </summary>
    private FixedPoint2? RaiseThreshold(EntityUid uid, MobState state, FixedPoint2 value)
    {
        if (!_thresholds.TryGetThresholdForState(uid, state, out var old) || old.Value >= value)
            return null;

        _thresholds.SetMobStateThreshold(uid, value, state);
        return old;
    }

    private void OnGetMeleeDamage(Entity<BerserkHeartComponent> ent, ref GetMeleeDamageEvent args)
    {
        if (ent.Comp.Active)
            args.Damage *= ent.Comp.MeleeDamageMultiplier;
    }

    // Only hits on living creatures heal, so walls, crates and corpses can not be farmed.
    private void OnMeleeHit(Entity<BerserkHeartComponent> ent, ref MeleeUserHitEvent args)
    {
        if (!ent.Comp.Active)
            return;

        foreach (var target in args.Targets)
        {
            if (target == ent.Owner || !_mobState.IsAlive(target) && !_mobState.IsCritical(target))
                continue;

            _damageable.TryChangeDamage(ent.Owner, ent.Comp.HealOnHit, ignoreResistances: true, interruptsDoAfters: false);
            return;
        }
    }

    // Like the sandevistan, the heart gives out when the user goes down.
    private void OnMobStateChanged(Entity<BerserkHeartComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Alive)
            Deactivate(ent);
    }
}
