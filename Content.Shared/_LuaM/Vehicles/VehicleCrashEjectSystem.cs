using Content.Shared._LuaM.Vehicles.Components;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared._LuaM.Vehicles;

public sealed partial class VehicleCrashEjectSystem : EntitySystem
{
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private ThrowingSystem _throwing = default!;

    private readonly List<EntityUid> _riders = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleCrashEjectComponent, VehicleCrashEvent>(OnCrash);
    }

    private void OnCrash(Entity<VehicleCrashEjectComponent> ent, ref VehicleCrashEvent args)
    {
        if (_net.IsClient || args.Speed < ent.Comp.MinSpeed || !TryComp<StrapComponent>(ent, out var strap))
            return;

        _riders.Clear();
        _riders.AddRange(strap.BuckledEntities);
        foreach (var rider in _riders)
        {
            if (!TryComp<BuckleComponent>(rider, out var buckle))
                continue;

            _buckle.Unbuckle((rider, buckle), null);
            if (buckle.Buckled)
                continue;

            var damage = new DamageSpecifier();
            damage.DamageDict["Blunt"] = ent.Comp.BluntDamage;
            _damageable.TryChangeDamage(rider, damage, origin: ent);
            _stun.TryKnockdown(rider, TimeSpan.FromSeconds(ent.Comp.KnockdownTime), true);
            var direction = Angle.FromDegrees(_random.Prob(0.5f) ? 60 : -60).RotateVec(args.Direction);
            _throwing.TryThrow(rider, direction * ent.Comp.ThrowDistance, ent.Comp.ThrowSpeed, ent,
                pushbackRatio: 0f, recoil: false, doSpin: false);
            _popup.PopupEntity(Loc.GetString("luam-vehicle-crash-ejected"), rider, rider, PopupType.MediumCaution);
        }
    }
}
