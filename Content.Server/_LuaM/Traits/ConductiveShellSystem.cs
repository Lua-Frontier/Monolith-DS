using Content.Server._EinsteinEngines.Silicon.Charge;
using Content.Server.EUI;
using Content.Server.Ghost;
using Content.Server.PowerCell;
using Content.Shared._LuaM.Traits;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Traits.Assorted;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._LuaM.Traits;

public sealed partial class ConductiveShellSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EuiManager _eui = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private SharedRottingSystem _rotting = default!;
    [Dependency] private SiliconChargeSystem _siliconCharge = default!;
    [Dependency] private StaminaSystem _stamina = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Raised on the weapon, so with a weapon in hand it goes to the weapon and only fists end up here.
        SubscribeLocalEvent<ConductiveShellComponent, MeleeHitEvent>(OnMeleeHit);
    }

    private void OnMeleeHit(Entity<ConductiveShellComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.User != ent.Owner || args.HitEntities.Count == 0)
            return;

        var mobs = new List<(EntityUid Uid, MobStateComponent Mob)>();
        foreach (var hit in args.HitEntities)
        {
            if (hit != ent.Owner && TryComp<MobStateComponent>(hit, out var mob))
                mobs.Add((hit, mob));
        }

        if (mobs.Count == 0
            || !_siliconCharge.TryGetSiliconBattery(ent, out var battery)
            || !_powerCell.TryUseCharge(ent, battery.MaxCharge * ent.Comp.BatteryCost))
            return;

        _audio.PlayPvs(ent.Comp.ZapSound, ent);

        var shocked = false;
        foreach (var (target, mob) in mobs)
        {
            if (_mobState.IsDead(target, mob))
            {
                TryRevive(ent, target, mob);
                continue;
            }

            _stamina.TakeStaminaDamage(target, ent.Comp.StaminaDamage, source: ent, with: ent);
            shocked = true;
        }

        // Melee damage is shared by every target of the swing, so the shock goes on it only for the living.
        if (shocked)
            args.BonusDamage += ent.Comp.Damage;
    }

    /// <summary>
    /// What a defibrillator zap does to a corpse, without the device itself.
    /// </summary>
    private void TryRevive(Entity<ConductiveShellComponent> ent, EntityUid target, MobStateComponent mob)
    {
        var now = _timing.CurTime;
        if (now < ent.Comp.NextRevive)
            return;

        ent.Comp.NextRevive = now + ent.Comp.ReviveCooldown;

        var revived = false;
        ICommonSession? session = null;

        if (_rotting.IsRotten(target))
        {
            _popup.PopupEntity(Loc.GetString("defibrillator-rotten"), target, ent);
        }
        else if (TryComp<UnrevivableComponent>(target, out var unrevivable))
        {
            _popup.PopupEntity(Loc.GetString(unrevivable.ReasonMessage), target, ent);
        }
        else
        {
            _damageable.TryChangeDamage(target, ent.Comp.ReviveHeal, true, origin: ent);

            if (_thresholds.TryGetThresholdForState(target, MobState.Dead, out var threshold)
                && TryComp<DamageableComponent>(target, out var damageable)
                && damageable.TotalDamage < threshold)
            {
                _mobState.ChangeMobState(target, MobState.Critical, mob, ent);
                revived = true;
            }

            if (_mind.TryGetMind(target, out _, out var mind)
                && _player.TryGetSessionById(mind.UserId, out var playerSession))
            {
                session = playerSession;
                if (mind.CurrentEntity != target)
                    _eui.OpenEui(new ReturnToBodyEui(mind, _mind, _player), session);
            }
            else
            {
                _popup.PopupEntity(Loc.GetString("defibrillator-no-mind"), target, ent);
            }
        }

        _audio.PlayPvs(revived && session != null ? ent.Comp.ReviveSuccessSound : ent.Comp.ReviveFailureSound, target);
    }
}
