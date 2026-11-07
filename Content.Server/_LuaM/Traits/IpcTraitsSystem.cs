using Content.Server.Power.EntitySystems;
using Content.Server.PowerCell;
using Content.Shared._EinsteinEngines.Silicon.Components;
using Content.Shared._LuaM.Traits;
using Content.Shared.Chemistry;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._LuaM.Traits;

/// <summary>
/// Negative IPC traits: water shorts, unstable wiring, power hungry chassis and a fragile body.
/// </summary>
public sealed partial class IpcTraitsSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private BatterySystem _battery = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly string ShockType = "Shock";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReagentTouchDamageComponent, ReactionEntityEvent>(OnReaction);
        SubscribeLocalEvent<UnstableWiringComponent, DamageChangedEvent>(OnWiringDamaged);
        SubscribeLocalEvent<SiliconDrainOverrideComponent, ComponentStartup>(OnDrainStartup);
        SubscribeLocalEvent<MobThresholdsDecreaseComponent, ComponentStartup>(OnThresholdsStartup);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Stamina crit has no event, so watch for the moment it starts.
        var query = EntityQueryEnumerator<UnstableWiringComponent, StaminaComponent>();
        while (query.MoveNext(out var uid, out var wiring, out var stamina))
        {
            if (stamina.Critical && !wiring.WasStaminaCrit)
                TryShort((uid, wiring));

            wiring.WasStaminaCrit = stamina.Critical;
        }
    }

    private void OnReaction(Entity<ReagentTouchDamageComponent> ent, ref ReactionEntityEvent args)
    {
        if (args.Method != ReactionMethod.Touch || args.Reagent.ID != ent.Comp.Reagent.Id)
            return;

        var amount = args.Source?.GetReagentQuantity(args.ReagentQuantity.Reagent) ?? args.ReagentQuantity.Quantity;
        if (amount <= 0)
            return;

        _damageable.TryChangeDamage(ent.Owner, ent.Comp.DamagePerUnit * amount.Float(), ignoreResistances: true, interruptsDoAfters: false);
    }

    private void OnWiringDamaged(Entity<UnstableWiringComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased
            || args.DamageDelta == null
            || !args.DamageDelta.DamageDict.TryGetValue(ShockType, out var shock)
            || shock <= 0)
            return;

        TryShort(ent);
    }

    private void TryShort(Entity<UnstableWiringComponent> ent)
    {
        // The discharge hurts the IPC too, so the cooldown is set first to not short again from it.
        if (_timing.CurTime < ent.Comp.NextShort)
            return;

        ent.Comp.NextShort = _timing.CurTime + ent.Comp.Cooldown;

        _stun.TryParalyze(ent, ent.Comp.StunTime, true);

        if (_powerCell.TryGetBatteryFromSlot(ent, out var batteryUid, out var battery))
            _battery.UseCharge(batteryUid.Value, battery.MaxCharge * ent.Comp.BatteryLoss, battery);

        var coords = Transform(ent).Coordinates;
        Spawn(ent.Comp.Effect, coords);
        _audio.PlayPvs(ent.Comp.Sound, ent);
        _popup.PopupEntity(Loc.GetString(ent.Comp.Popup), ent, PopupType.MediumCaution);

        foreach (var target in _lookup.GetEntitiesInRange<MobStateComponent>(coords, ent.Comp.Radius))
        {
            if (target.Owner == ent.Owner)
                continue;

            _damageable.TryChangeDamage(target.Owner, ent.Comp.Damage, origin: ent.Owner);
        }

        // Exactly the set damage to the IPC itself, its shock weakness would make it much more.
        _damageable.TryChangeDamage(ent.Owner, ent.Comp.Damage, ignoreResistances: true, origin: ent.Owner);
    }

    private void OnDrainStartup(Entity<SiliconDrainOverrideComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<SiliconComponent>(ent, out var silicon))
            silicon.DrainPerSecond = ent.Comp.DrainPerSecond;
    }

    private void OnThresholdsStartup(Entity<MobThresholdsDecreaseComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<MobThresholdsComponent>(ent, out var thresholds))
            return;

        foreach (var (damage, state) in new Dictionary<FixedPoint2, MobState>(thresholds.Thresholds))
        {
            if (state == MobState.Alive || damage <= 0)
                continue;

            _thresholds.SetMobStateThreshold(ent, FixedPoint2.Max(FixedPoint2.New(1), damage - ent.Comp.Amount), state, thresholds);
        }
    }
}
