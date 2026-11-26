// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Goobstation.Shared.Grab;
using Content.Goobstation.Shared.GrabIntent;
using Content.Goobstation.Shared.MartialArts.Components;
using Content.Goobstation.Shared.MartialArts.Events;
using Content.Goobstation.Shared.Projectiles;
using Content.Goobstation.Shared.Sprinting;
using Content.Shared._Shitmed.Targeting;
using Content.Shared._White.BackStab;
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Alert;
using Content.Shared.CombatMode;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Goobstation.Shared.MartialArts;

/// <summary>
/// Handles martial arts: learning them, combos and their effects.
/// Combos and their effects run on the server only, the client only predicts grabs and modifiers.
/// </summary>
public abstract partial class SharedMartialArtsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly BackStabSystem _backstab = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedCombatModeSystem _combatMode = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSprintingSystem _sprinting = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly GrabIntentSystem _grab = default!;
    [Dependency] private readonly GrabThrownSystem _grabThrown = default!;

    private const string KnockedDownKey = "KnockedDown";

    private readonly Dictionary<MartialArtsForms, MartialArtPrototype> _arts = new();
    private readonly List<EntityUid> _expiredModifiers = new();

    public override void Initialize()
    {
        base.Initialize();

        InitializeCombos();
        InitializeCqc();
        InitializeCorporateJudo();
        InitializeSleepingCarp();
        InitializeKravMaga();
        InitializeCapoeira();
        InitializeDragon();
        InitializeNinjutsu();

        SubscribeLocalEvent<GrantMartialArtKnowledgeComponent, UseInHandEvent>(OnGrantUse);
        SubscribeLocalEvent<GrantMartialArtKnowledgeComponent, MapInitEvent>(OnGrantMapInit);

        SubscribeLocalEvent<MartialArtsKnowledgeComponent, ComponentShutdown>(OnKnowledgeShutdown);

        SubscribeLocalEvent<MartialArtModifiersComponent, GetMeleeAttackRateEvent>(OnGetMeleeAttackRate);
        SubscribeLocalEvent<MartialArtModifiersComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);
        SubscribeLocalEvent<MartialArtsVulnerableComponent, AttackedEvent>(OnVulnerableAttacked);

        SubscribeLocalEvent<MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        CacheArts();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var curTime = _timing.CurTime;

        var comboQuery = EntityQueryEnumerator<CanPerformComboComponent>();
        while (comboQuery.MoveNext(out var uid, out var combo))
        {
            if (combo.LastAttacks.Count == 0 && combo.ConsecutiveGnashes == 0
                || curTime < combo.ResetTime)
                continue;

            combo.LastAttacks.Clear();
            combo.ConsecutiveGnashes = 0;
            Dirty(uid, combo);
        }

        var modifiersQuery = EntityQueryEnumerator<MartialArtModifiersComponent>();
        while (modifiersQuery.MoveNext(out var uid, out var modifiers))
        {
            if (curTime >= modifiers.NextUpdate && UpdateModifiers((uid, modifiers), curTime))
                _expiredModifiers.Add(uid);
        }

        // Removed right away after the loop, a deferred removal would eat a modifier added later this tick
        foreach (var uid in _expiredModifiers)
        {
            RemComp<MartialArtModifiersComponent>(uid);
        }

        _expiredModifiers.Clear();

        UpdateDragon(curTime);
    }

    #region Prototypes

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<MartialArtPrototype>())
            CacheArts();
    }

    private void CacheArts()
    {
        _arts.Clear();
        foreach (var art in _proto.EnumeratePrototypes<MartialArtPrototype>())
        {
            _arts[art.MartialArtsForm] = art;
        }
    }

    protected bool TryGetArt(MartialArtsForms form, [NotNullWhen(true)] out MartialArtPrototype? art)
    {
        return _arts.TryGetValue(form, out art);
    }

    #endregion

    #region Learning

    private void OnGrantUse(Entity<GrantMartialArtKnowledgeComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (_net.IsClient
            || !TryGrantMartialArt(args.User, ent.Comp.MartialArtsForm, ent.Comp.LearnMessage))
            return;

        var coords = Transform(args.User).Coordinates;
        _audio.PlayPvs(ent.Comp.SoundOnUse, coords);

        if (ent.Comp.MultiUse)
            return;

        QueueDel(ent);

        if (ent.Comp.SpawnedProto is { } spawned)
            Spawn(spawned, coords);
    }

    private void OnGrantMapInit(Entity<GrantMartialArtKnowledgeComponent> ent, ref MapInitEvent args)
    {
        // Mobs with the component know the art from the start
        if (HasComp<MobStateComponent>(ent))
            TryGrantMartialArt(ent, ent.Comp.MartialArtsForm, null, false);
    }

    /// <summary>
    /// Teaches the martial art to the user if they don't know another one.
    /// </summary>
    protected bool TryGrantMartialArt(EntityUid user, MartialArtsForms form, LocId? learnMessage, bool popup = true)
    {
        if (_net.IsClient || TerminatingOrDeleted(user))
            return false;

        if (TryComp<MartialArtsKnowledgeComponent>(user, out var known))
        {
            if (popup)
            {
                var message = known.MartialArtsForm == form ? "cqc-fail-already" : "cqc-fail-knowanother";
                _popup.PopupEntity(Loc.GetString(message), user, user);
            }

            return false;
        }

        if (HasComp<KravMagaComponent>(user))
        {
            if (popup)
                _popup.PopupEntity(Loc.GetString("cqc-fail-knowanother"), user, user);

            return false;
        }

        if (!TryGetArt(form, out var art)
            || !TryComp<MeleeWeaponComponent>(user, out var melee))
            return false;

        var knowledge = EnsureComp<MartialArtsKnowledgeComponent>(user);
        knowledge.MartialArtsForm = form;
        knowledge.OriginalFistDamage = new DamageSpecifier(melee.Damage);
        Dirty(user, knowledge);

        var combo = EnsureComp<CanPerformComboComponent>(user);
        combo.AllowedCombos.Clear();
        if (_proto.TryIndex(art.RoundstartCombos, out var comboList))
            combo.AllowedCombos.AddRange(comboList.Combos);
        combo.LastAttacks.Clear();
        combo.CurrentTarget = null;
        Dirty(user, combo);

        if (art.BaseDamageModifier > FixedPoint2.Zero)
        {
            var bonus = new DamageSpecifier();
            bonus.DamageDict.Add(art.DamageModifierType, art.BaseDamageModifier);
            melee.Damage += bonus;
            Dirty(user, melee);
        }

        switch (form)
        {
            case MartialArtsForms.KungFuDragon:
                EnsureComp<DragonKungFuTimerComponent>(user);
                break;
            case MartialArtsForms.Ninjutsu:
                EnsureComp<NinjutsuSneakAttackComponent>(user);
                break;
        }

        if (popup && learnMessage != null)
            _popup.PopupEntity(Loc.GetString(learnMessage.Value), user, user);

        return true;
    }

    /// <summary>
    /// Makes the entity forget its martial art, see <see cref="OnKnowledgeShutdown"/>.
    /// </summary>
    protected void RevokeMartialArt(EntityUid user, MartialArtsForms form)
    {
        if (TryComp<MartialArtsKnowledgeComponent>(user, out var knowledge)
            && knowledge.MartialArtsForm == form)
            RemComp(user, knowledge);
    }

    private void OnKnowledgeShutdown(Entity<MartialArtsKnowledgeComponent> ent, ref ComponentShutdown args)
    {
        if (_net.IsClient || TerminatingOrDeleted(ent))
            return;

        if (ent.Comp.OriginalFistDamage is { } original
            && TryComp<MeleeWeaponComponent>(ent, out var melee))
        {
            melee.Damage = new DamageSpecifier(original);
            Dirty(ent, melee);
        }

        RemCompDeferred<CanPerformComboComponent>(ent);
        RemCompDeferred<DragonKungFuTimerComponent>(ent);
        RemCompDeferred<NinjutsuSneakAttackComponent>(ent);

        if (ent.Comp.MartialArtsForm == MartialArtsForms.SleepingCarp)
            RemCompDeferred<AutoDodgeComponent>(ent);
    }

    #endregion

    #region Modifiers

    private void OnGetMeleeAttackRate(Entity<MartialArtModifiersComponent> ent, ref GetMeleeAttackRateEvent args)
    {
        // Raised on the weapon and on the user, the modifiers belong to the user
        if (args.User != ent.Owner)
            return;

        var (mult, mod) = GetMultiplierModifier(ent.Comp, MartialArtModifierType.AttackRate, args.Weapon != args.User);
        args.Multipliers *= mult;
        args.Rate += mod;
    }

    private void OnRefreshMovespeed(Entity<MartialArtModifiersComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        var (mult, _) = GetMultiplierModifier(ent.Comp, MartialArtModifierType.MoveSpeed, null);
        if (MathF.Abs(mult - 1f) > 0.001f)
            args.ModifySpeed(mult, mult);
    }

    private void OnVulnerableAttacked(Entity<MartialArtsVulnerableComponent> ent, ref AttackedEvent args)
    {
        args.ModifiersList.AddRange(ent.Comp.ModifierSets);
    }

    /// <returns>True if no modifiers are left.</returns>
    private bool UpdateModifiers(Entity<MartialArtModifiersComponent> ent, TimeSpan curTime)
    {
        var refreshSpeed = false;
        var nextUpdate = TimeSpan.MaxValue;
        var data = ent.Comp.Data;

        for (var i = data.Count - 1; i >= 0; i--)
        {
            var entry = data[i];
            if (curTime < entry.EndTime)
            {
                if (entry.EndTime < nextUpdate)
                    nextUpdate = entry.EndTime;
                continue;
            }

            if ((entry.Type & MartialArtModifierType.MoveSpeed) != 0)
                refreshSpeed = true;

            data.RemoveAt(i);
        }

        if (refreshSpeed)
            _movementSpeed.RefreshMovementSpeedModifiers(ent);

        if (data.Count == 0)
            return true;

        ent.Comp.NextUpdate = nextUpdate;
        Dirty(ent);
        return false;
    }

    /// <summary>
    /// Adds a temporary modifier, modifiers of the same type multiply each other.
    /// </summary>
    protected void ApplyMultiplier(EntityUid uid,
        float multiplier,
        float modifier,
        TimeSpan time,
        MartialArtModifierType type = MartialArtModifierType.AttackRate)
    {
        if (MathF.Abs(multiplier - 1f) < 0.001f && MathF.Abs(modifier) < 0.001f
            || time <= TimeSpan.Zero)
            return;

        var comp = EnsureComp<MartialArtModifiersComponent>(uid);
        var endTime = _timing.CurTime + time;
        comp.Data.Add(new MartialArtModifierData
        {
            Type = type,
            Multiplier = multiplier,
            Modifier = modifier,
            EndTime = endTime,
        });
        AfterModifierAdded((uid, comp), endTime, type);
    }

    /// <summary>
    /// Slows the target down. Unlike <see cref="ApplyMultiplier"/>, slowdowns don't stack:
    /// the strongest one is kept until the latest end time.
    /// </summary>
    protected void ApplySlowdown(EntityUid target, float multiplier, TimeSpan time)
    {
        if (multiplier >= 1f || time <= TimeSpan.Zero)
            return;

        const MartialArtModifierType type = MartialArtModifierType.MoveSpeed | MartialArtModifierType.Slowdown;

        var comp = EnsureComp<MartialArtModifiersComponent>(target);
        var endTime = _timing.CurTime + time;

        MartialArtModifierData? existing = null;
        foreach (var data in comp.Data)
        {
            if (data.Type != type)
                continue;

            existing = data;
            break;
        }

        if (existing != null)
        {
            existing.Multiplier = MathF.Min(existing.Multiplier, multiplier);
            if (endTime > existing.EndTime)
                existing.EndTime = endTime;
        }
        else
        {
            comp.Data.Add(new MartialArtModifierData
            {
                Type = type,
                Multiplier = multiplier,
                EndTime = endTime,
            });
        }

        AfterModifierAdded((target, comp), endTime, type);
    }

    private void AfterModifierAdded(Entity<MartialArtModifiersComponent> ent, TimeSpan endTime, MartialArtModifierType type)
    {
        if (ent.Comp.Data.Count == 1 || endTime < ent.Comp.NextUpdate)
            ent.Comp.NextUpdate = endTime;

        Dirty(ent);

        if ((type & MartialArtModifierType.MoveSpeed) != 0)
            _movementSpeed.RefreshMovementSpeedModifiers(ent);
    }

    private static (float Mult, float Mod) GetMultiplierModifier(MartialArtModifiersComponent comp,
        MartialArtModifierType type,
        bool? armed)
    {
        var mult = 1f;
        var mod = 0f;
        var found = false;

        foreach (var data in comp.Data)
        {
            if ((data.Type & type) == 0)
                continue;

            var armedOnly = (data.Type & MartialArtModifierType.Armed) != 0;
            var unarmedOnly = (data.Type & MartialArtModifierType.Unarmed) != 0;
            if (armed == true && unarmedOnly && !armedOnly
                || armed == false && armedOnly && !unarmedOnly)
                continue;

            mult *= data.Multiplier;
            mod += data.Modifier;
            found = true;
        }

        if (!found)
            return (1f, 0f);

        foreach (var (limitType, limit) in comp.MinMaxModifiersMultipliers)
        {
            if ((limitType & type) == 0)
                continue;

            mult = Math.Clamp(mult, limit.X, limit.Y);
            mod = Math.Clamp(mod, limit.Z, limit.W);
        }

        return (mult, mod);
    }

    private static DamageModifierSet GetDamageModifierSet(DamageSpecifier specifier, float multiplier, float modifier)
    {
        var set = new DamageModifierSet();
        foreach (var type in specifier.DamageDict.Keys)
        {
            set.Coefficients[type] = multiplier;
            // Flat reduction subtracts, so a bonus is a negative reduction
            set.FlatReduction[type] = -modifier;
        }

        return set;
    }

    #endregion

    #region Melee

    private void OnMeleeHit(MeleeHitEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;
        var unarmed = args.Weapon == user;

        if (TryComp<MartialArtModifiersComponent>(user, out var modifiers) && modifiers.Data.Count > 0)
        {
            var (mult, mod) = GetMultiplierModifier(modifiers, MartialArtModifierType.Damage, !unarmed);
            if (MathF.Abs(mult - 1f) > 0.001f || MathF.Abs(mod) > 0.001f)
                args.ModifiersList.Add(GetDamageModifierSet(args.BaseDamage, mult, mod));
        }

        if (_net.IsClient || !TryComp<MartialArtsKnowledgeComponent>(user, out var knowledge))
            return;

        switch (knowledge.MartialArtsForm)
        {
            case MartialArtsForms.Ninjutsu:
                OnNinjutsuMeleeHit(user, args);
                break;
            case MartialArtsForms.Capoeira:
                OnCapoeiraMeleeHit(user, args);
                break;
        }

        if (!unarmed
            || args.HitEntities.Count == 0
            || !TryGetArt(knowledge.MartialArtsForm, out var art)
            || !art.RandomDamageModifier)
            return;

        var randomDamage = _random.Next(art.MinRandomDamageModifier, art.MaxRandomDamageModifier + 1);
        if (randomDamage <= 0)
            return;

        var bonus = new DamageSpecifier();
        bonus.DamageDict.Add(art.DamageModifierType, randomDamage);
        args.BonusDamage += bonus;
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Gets the combo being performed and checks that it can be done.
    /// </summary>
    private bool TryStartCombo(Entity<CanPerformComboComponent> ent,
        [NotNullWhen(true)] out ComboPrototype? proto,
        out EntityUid target,
        out bool downed)
    {
        target = EntityUid.Invalid;
        downed = false;
        proto = null;

        return ent.Comp.BeingPerformed is { } id
               && _proto.TryIndex(id, out proto)
               && TryUseMartialArt(ent, proto, out target, out downed);
    }

    private bool TryUseMartialArt(Entity<CanPerformComboComponent> ent,
        ComboPrototype proto,
        out EntityUid target,
        out bool downed)
    {
        target = EntityUid.Invalid;
        downed = false;

        if (ent.Comp.CurrentTarget is not { } currentTarget
            || TerminatingOrDeleted(currentTarget)
            || !TryComp<MartialArtsKnowledgeComponent>(ent, out var knowledge)
            || knowledge.MartialArtsForm != proto.MartialArtsForm)
            return false;

        if (!proto.CanDoWhileProne && _standing.IsDown(ent))
        {
            _popup.PopupEntity(Loc.GetString("martial-arts-fail-prone"), ent, ent);
            return false;
        }

        target = currentTarget;
        downed = _standing.IsDown(target);
        return true;
    }

    /// <summary>
    /// Plays the combo sound, shows the popups and resets the attack chain.
    /// </summary>
    private void FinishCombo(Entity<CanPerformComboComponent> ent,
        EntityUid target,
        ComboPrototype proto,
        BaseComboPerformedEvent args,
        bool popup = true)
    {
        _audio.PlayPvs(args.Sound, target);

        if (popup)
            ComboPopup(ent, target, proto.Name);

        ent.Comp.LastAttacks.Clear();
        Dirty(ent);
    }

    protected void ComboPopup(EntityUid user, EntityUid target, LocId comboName)
    {
        if (_net.IsClient)
            return;

        var move = Loc.GetString(comboName);
        _popup.PopupEntity(Loc.GetString("martial-arts-action-sender",
                ("name", Identity.Entity(target, EntityManager)),
                ("move", move)),
            user,
            user);
        _popup.PopupEntity(Loc.GetString("martial-arts-action-receiver",
                ("name", Identity.Entity(user, EntityManager)),
                ("move", move)),
            target,
            target);
    }

    /// <summary>
    /// Deals damage boosted by the user's damage modifiers to the target.
    /// </summary>
    private DamageSpecifier DoDamage(EntityUid ent,
        EntityUid target,
        ProtoId<DamageTypePrototype> damageType,
        float damageAmount,
        TargetBodyPart? targetBodyPart = null)
    {
        var damage = new DamageSpecifier();
        if (damageAmount <= 0f)
            return damage;

        damage.DamageDict.Add(damageType, FixedPoint2.New(damageAmount));

        if (TryComp(ent, out MartialArtModifiersComponent? modifiers) && modifiers.Data.Count > 0)
        {
            var (mult, mod) = GetMultiplierModifier(modifiers, MartialArtModifierType.Damage, false);
            if (MathF.Abs(mult - 1f) > 0.001f || MathF.Abs(mod) > 0.001f)
                damage = DamageSpecifier.ApplyModifierSet(damage, GetDamageModifierSet(damage, mult, mod));
        }

        targetBodyPart ??= CompOrNull<TargetingComponent>(ent)?.Target;
        _damageable.TryChangeDamage(target, damage, origin: ent, targetPart: targetBodyPart);
        return damage;
    }

    private void StopPull(EntityUid target, EntityUid user)
    {
        if (TryComp<PullableComponent>(target, out var pullable))
            _pulling.TryStopPull(target, pullable, user);
    }

    /// <summary>
    /// Throws the target so it lands on the floor and knocks down the ones it flies into.
    /// The length of <paramref name="direction"/> is the distance.
    /// </summary>
    private void ThrowTarget(EntityUid target, EntityUid thrower, Vector2 direction, float speed)
    {
        _grabThrown.Throw(target, thrower, direction, speed);
    }

    private Vector2 GetDirection(EntityUid from, EntityUid to)
    {
        return _transform.GetMapCoordinates(to).Position - _transform.GetMapCoordinates(from).Position;
    }

    private void StandUp(EntityUid uid)
    {
        _status.TryRemoveStatusEffect(uid, KnockedDownKey);
        _standing.Stand(uid);
    }

    private float GetVelocity(EntityUid uid)
    {
        return TryComp(uid, out PhysicsComponent? physics) ? physics.LinearVelocity.Length() : 0f;
    }

    #endregion
}
