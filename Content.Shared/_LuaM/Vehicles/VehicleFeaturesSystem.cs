/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using Content.Shared._LuaM.Vehicles.Components;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._LuaM.Vehicles;

public sealed partial class VehicleFeaturesSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private HardpointSystem _hardpoints = default!;
    [Dependency] private GridVehicleMoverSystem _mover = default!;
    [Dependency] private SharedPointLightSystem _light = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehicleSoundComponent, VehicleHornActionEvent>(OnHorn);
        SubscribeLocalEvent<VehicleOverchargeComponent, VehicleOverchargeActionEvent>(OnOvercharge);
        SubscribeLocalEvent<VehicleSpotlightComponent, VehicleSpotlightActionEvent>(OnSpotlight);
        SubscribeLocalEvent<VehicleDeployableComponent, VehicleDeployActionEvent>(OnDeploy);
        SubscribeLocalEvent<VehicleDeployableComponent, VehicleCanMoveEvent>(OnDeployCanMove);
        SubscribeLocalEvent<VehicleDeployableComponent, ExaminedEvent>(OnDeployExamined);
        SubscribeLocalEvent<VehicleIntegrityComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<VehicleIntegrityComponent, VehicleCanMoveEvent>(OnIntegrityCanMove);
        SubscribeLocalEvent<VehicleIntegrityComponent, ExaminedEvent>(OnIntegrityExamined);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<VehicleDeployableComponent>();
        while (query.MoveNext(out var uid, out var deploy))
        {
            if (!deploy.Transitioning || _timing.CurTime < deploy.TransitionEnd)
                continue;

            deploy.Transitioning = false;
            deploy.Deployed = !deploy.Deployed;
            Dirty(uid, deploy);

            if (deploy.Deployer is { } user)
            {
                var msg = deploy.Deployed ? "luam-vehicle-deployed" : "luam-vehicle-undeployed";
                _popup.PopupClient(Loc.GetString(msg), uid, user, PopupType.Medium);
            }
        }
    }

    private void OnHorn(Entity<VehicleSoundComponent> ent, ref VehicleHornActionEvent args)
    {
        if (args.Handled || ent.Comp.HornSound == null)
            return;

        args.Handled = true;
        _audio.PlayPredicted(ent.Comp.HornSound, ent, args.Performer);
    }

    private void OnOvercharge(Entity<VehicleOverchargeComponent> ent, ref VehicleOverchargeActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.ActiveUntil = _timing.CurTime + ent.Comp.Duration;
        Dirty(ent);

        if (ent.Comp.Sound != null)
            _audio.PlayPredicted(ent.Comp.Sound, ent, args.Performer);
    }

    private void OnSpotlight(Entity<VehicleSpotlightComponent> ent, ref VehicleSpotlightActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.Enabled = !ent.Comp.Enabled;
        Dirty(ent);

        _light.SetRadius(ent, ent.Comp.Radius * _hardpoints.GetSpotlightMultiplier(ent));
        _light.SetEnergy(ent, ent.Comp.Energy);
        _light.SetEnabled(ent, ent.Comp.Enabled);
    }

    private void OnDeploy(Entity<VehicleDeployableComponent> ent, ref VehicleDeployActionEvent args)
    {
        if (args.Handled || ent.Comp.Transitioning)
            return;

        args.Handled = true;
        ent.Comp.Transitioning = true;
        ent.Comp.Deployer = args.Performer;
        ent.Comp.TransitionEnd = _timing.CurTime + (ent.Comp.Deployed ? ent.Comp.UndeployTime : ent.Comp.DeployTime);
        Dirty(ent);

        _mover.Stop(ent);
        var msg = ent.Comp.Deployed ? "luam-vehicle-undeploying" : "luam-vehicle-deploying";
        _popup.PopupClient(Loc.GetString(msg), ent, args.Performer, PopupType.Medium);
    }

    private void OnDeployCanMove(Entity<VehicleDeployableComponent> ent, ref VehicleCanMoveEvent args)
    {
        if (ent.Comp.Deployed || ent.Comp.Transitioning)
            args.Cancelled = true;
    }

    private void OnDeployExamined(Entity<VehicleDeployableComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Deployed)
            args.PushMarkup(Loc.GetString("luam-vehicle-examine-deployed"));
    }

    private void OnDamageChanged(Entity<VehicleIntegrityComponent> ent, ref DamageChangedEvent args)
    {
        var damage = args.Damageable.TotalDamage.Float();
        var wrecked = damage >= ent.Comp.MaxIntegrity;
        _appearance.SetData(ent, VehicleVisuals.Damaged, damage >= ent.Comp.MaxIntegrity * 0.5f);

        if (wrecked == ent.Comp.Wrecked)
            return;

        ent.Comp.Wrecked = wrecked;
        Dirty(ent);

        if (wrecked)
            _mover.Stop(ent);
    }

    private void OnIntegrityCanMove(Entity<VehicleIntegrityComponent> ent, ref VehicleCanMoveEvent args)
    {
        if (ent.Comp.Wrecked)
            args.Cancelled = true;
    }

    private void OnIntegrityExamined(Entity<VehicleIntegrityComponent> ent, ref ExaminedEvent args)
    {
        if (!TryComp<DamageableComponent>(ent, out var damageable) || ent.Comp.MaxIntegrity <= 0f)
            return;

        var percent = (int) MathF.Round(Math.Clamp(1f - damageable.TotalDamage.Float() / ent.Comp.MaxIntegrity, 0f, 1f) * 100f);
        args.PushMarkup(Loc.GetString(ent.Comp.Wrecked ? "luam-vehicle-examine-wrecked" : "luam-vehicle-examine-integrity", ("percent", percent)));
    }
}
