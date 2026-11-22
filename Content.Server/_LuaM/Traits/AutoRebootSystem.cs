using Content.Server._EinsteinEngines.Silicon.Charge;
using Content.Server._Goobstation.Temperature;
using Content.Server.Temperature.Components;
using Content.Shared._EinsteinEngines.Silicon.DeadStartupButton;
using Content.Shared._LuaM.Traits;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._LuaM.Traits;

public sealed partial class AutoRebootSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SiliconChargeSystem _siliconCharge = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AutoRebootComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(Entity<AutoRebootComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            ent.Comp.NextAttempt = _timing.CurTime + ent.Comp.Interval;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AutoRebootComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var reboot, out var mobState))
        {
            if (mobState.CurrentState != MobState.Dead || now < reboot.NextAttempt)
                continue;

            reboot.NextAttempt = now + reboot.Interval;

            if (!TryReboot(uid, mobState))
                RebootFailed(uid);
        }
    }

    private bool TryReboot(EntityUid uid, MobStateComponent mobState)
    {
        if (!_siliconCharge.TryGetSiliconBattery(uid, out var battery) || battery.CurrentCharge <= 0)
            return false;

        // Still overheated, it would shut down again right away.
        if (TryComp<KillOnOverheatComponent>(uid, out var overheat)
            && TryComp<TemperatureComponent>(uid, out var temperature)
            && temperature.CurrentTemperature >= overheat.OverheatThreshold)
            return false;

        if (!TryComp<MobThresholdsComponent>(uid, out var thresholds)
            || !TryComp<DamageableComponent>(uid, out var damageable)
            || !_thresholds.TryGetThresholdForState(uid, MobState.Dead, out var deadThreshold, thresholds)
            || damageable.TotalDamage >= deadThreshold)
            return false;

        // The startup button plays the sound and popup once the mob is alive again.
        _mobState.ChangeMobState(uid, MobState.Alive, mobState);
        return true;
    }

    private void RebootFailed(EntityUid uid)
    {
        if (TryComp<DeadStartupButtonComponent>(uid, out var button))
            _audio.PlayPvs(button.BuzzSound, uid, AudioParams.Default.WithVariation(0.05f));

        _popup.PopupEntity(Loc.GetString("auto-reboot-failed", ("target", MetaData(uid).EntityName)), uid);
    }
}
