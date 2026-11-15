using Content.Shared._LuaM.Vehicles.Components;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._LuaM.Vehicles;

public sealed partial class VehicleShellSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private VehicleTurretSystem _turret = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleShellMagazineComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<VehicleShellMagazineComponent, TakeAmmoEvent>((uid, comp, args) => OnTakeAmmo((uid, comp), args));
        SubscribeLocalEvent<VehicleShellMagazineComponent, GetAmmoCountEvent>(OnAmmoCount);
        SubscribeLocalEvent<VehicleShellMagazineComponent, CheckShootPrototypeEvent>(OnCheckProto);
        SubscribeLocalEvent<VehicleShellMagazineComponent, GunShotEvent>(OnShot);
        SubscribeLocalEvent<VehicleTurretComponent, VehicleSwitchShellActionEvent>(OnSwitchShell);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<VehicleShellMagazineComponent>();
        while (query.MoveNext(out var uid, out var mag))
        {
            if (mag.LoadedAt is not { } at || now < at)
                continue;

            mag.LoadedAt = null;
            if (GetCount(mag, mag.Selected) <= 0 || !TryGetVehicle(uid, out var vehicle))
                continue;

            _audio.PlayPvs(mag.LoadedSound, vehicle);
            if (TryComp<VehicleSeatsComponent>(vehicle, out var seats) && seats.Gunner is { } gunner)
                _popup.PopupEntity(Loc.GetString("luam-vehicle-shell-loaded", ("shell", ShellName(mag, mag.Selected))), vehicle, gunner);
        }
    }

    private void OnMapInit(Entity<VehicleShellMagazineComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Counts.Count == ent.Comp.Shells.Count)
            return;

        ent.Comp.Counts.Clear();
        foreach (var shell in ent.Comp.Shells)
        {
            ent.Comp.Counts.Add(shell.StartingCount);
        }

        Dirty(ent);
    }

    private void OnTakeAmmo(Entity<VehicleShellMagazineComponent> ent, TakeAmmoEvent args)
    {
        var index = ent.Comp.Selected;
        if (index < 0 || index >= ent.Comp.Shells.Count)
            return;

        for (var i = 0; i < args.Shots && GetCount(ent.Comp, index) > 0; i++)
        {
            ent.Comp.Counts[index]--;
            var shell = Spawn(ent.Comp.Shells[index].Proto, args.Coordinates);
            if (TryComp<CartridgeAmmoComponent>(shell, out var cartridge))
                args.Ammo.Add((shell, cartridge));
            else if (TryComp<AmmoComponent>(shell, out var ammo))
                args.Ammo.Add((shell, ammo));
        }

        Dirty(ent);
    }

    private void OnAmmoCount(Entity<VehicleShellMagazineComponent> ent, ref GetAmmoCountEvent args)
    {
        args.Count = GetCount(ent.Comp, ent.Comp.Selected);
        args.Capacity = ent.Comp.Selected < ent.Comp.Shells.Count ? ent.Comp.Shells[ent.Comp.Selected].Capacity : 0;
    }

    private void OnCheckProto(Entity<VehicleShellMagazineComponent> ent, ref CheckShootPrototypeEvent args)
    {
        if (ent.Comp.Selected < ent.Comp.Shells.Count)
            args.ShootPrototype = _proto.Index(ent.Comp.Shells[ent.Comp.Selected].Proto);
    }

    private void OnShot(Entity<VehicleShellMagazineComponent> ent, ref GunShotEvent args)
    {
        if (_net.IsClient || !TryComp<GunComponent>(ent, out var gun))
            return;

        ent.Comp.LoadedAt = gun.NextFire;
    }

    private void OnSwitchShell(Entity<VehicleTurretComponent> ent, ref VehicleSwitchShellActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (!_turret.TryGetSelectedWeapon(ent, ent.Comp, out var weapon, out var gun) ||
            !TryComp<VehicleShellMagazineComponent>(weapon, out var mag) || mag.Shells.Count < 2)
        {
            _popup.PopupClient(Loc.GetString("luam-vehicle-shell-no-choice"), ent, args.Performer);
            return;
        }

        if (_net.IsClient)
            return;

        mag.Selected = (mag.Selected + 1) % mag.Shells.Count;
        Dirty(weapon.Value, mag);

        var now = _timing.CurTime;
        var reload = TimeSpan.FromSeconds(1f / MathF.Max(gun.FireRateModified, 0.01f));
        if (gun.NextFire < now + reload)
        {
            gun.NextFire = now + reload;
            Dirty(weapon.Value, gun);
        }

        mag.LoadedAt = gun.NextFire;
        _audio.PlayPvs(mag.SwitchSound, ent);
        _popup.PopupEntity(Loc.GetString("luam-vehicle-shell-selected",
            ("shell", ShellName(mag, mag.Selected)),
            ("count", GetCount(mag, mag.Selected))), ent, args.Performer);
    }

    public bool TryFindShellTarget(EntityUid vehicle, string ammoType, out Entity<VehicleShellMagazineComponent> target, out int index)
    {
        if (TryComp<VehicleTurretComponent>(vehicle, out var turret) &&
            _turret.TryGetSelectedWeapon(vehicle, turret, out var weapon, out _) &&
            TryComp<VehicleShellMagazineComponent>(weapon, out var mag))
        {
            for (var i = 0; i < mag.Shells.Count; i++)
            {
                if (mag.Shells[i].AmmoType != ammoType || GetCount(mag, i) >= mag.Shells[i].Capacity)
                    continue;

                target = (weapon.Value, mag);
                index = i;
                return true;
            }
        }

        target = default;
        index = -1;
        return false;
    }

    public int AddShells(Entity<VehicleShellMagazineComponent> ent, int index, int amount)
    {
        if (ent.Comp.Counts.Count != ent.Comp.Shells.Count || index < 0 || index >= ent.Comp.Shells.Count)
            return 0;

        var loaded = Math.Min(amount, ent.Comp.Shells[index].Capacity - ent.Comp.Counts[index]);
        if (loaded <= 0)
            return 0;

        ent.Comp.Counts[index] += loaded;
        Dirty(ent);
        return loaded;
    }

    public string GetSummary(VehicleShellMagazineComponent mag)
    {
        var parts = new List<string>();
        for (var i = 0; i < mag.Shells.Count; i++)
        {
            var text = Loc.GetString("luam-vehicle-shell-examine",
                ("shell", ShellName(mag, i)),
                ("count", GetCount(mag, i)),
                ("capacity", mag.Shells[i].Capacity));
            parts.Add(i == mag.Selected ? $"[bold]{text}[/bold]" : text);
        }

        return string.Join(", ", parts);
    }

    private string ShellName(VehicleShellMagazineComponent mag, int index)
    {
        return index < mag.Shells.Count ? Loc.GetString(mag.Shells[index].Name) : string.Empty;
    }

    private static int GetCount(VehicleShellMagazineComponent mag, int index)
    {
        return index >= 0 && index < mag.Counts.Count ? mag.Counts[index] : 0;
    }

    private bool TryGetVehicle(EntityUid weapon, out EntityUid vehicle)
    {
        var current = Transform(weapon).ParentUid;
        for (var i = 0; i < 3 && current.IsValid(); i++)
        {
            if (HasComp<VehicleSeatsComponent>(current))
            {
                vehicle = current;
                return true;
            }

            current = Transform(current).ParentUid;
        }

        vehicle = default;
        return false;
    }
}
