using System.Numerics;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared._LuaM.Vehicles;
using Content.Shared._LuaM.Vehicles.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Client._LuaM.Vehicles.Dashboard;

public sealed partial class VehicleDashboardUIController : UIController, IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const float KmhPerTile = 3.6f;
    private const float NeedleResponse = 8f;
    private const float PanelMargin = -150f;
    private const float StackGap = 6f;

    private VehicleDashboard? _panel;
    private VehicleGunnerPanel? _gunnerPanel;
    private Vector2? _driverPosition;
    private Vector2? _gunnerPosition;
    private bool _active;
    private bool _stacked;
    private EntityUid? _vehicle;

    public override void Initialize()
    {
        base.Initialize();

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
        gameplayStateLoad.OnScreenUnload += OnScreenUnload;
    }

    public void OnStateEntered(GameplayState state)
    {
        _active = true;
    }

    public void OnStateExited(GameplayState state)
    {
        _active = false;
        Hide();
    }

    private void OnScreenLoad()
    {
        if (UIManager.ActiveScreen is not { } screen)
            return;

        _panel = new VehicleDashboard { Visible = false };
        _panel.Moved += pos => _driverPosition = pos;
        _panel.SpeedLimitChanged += OnSpeedLimitChanged;
        Place(screen, _panel, _driverPosition);

        _gunnerPanel = new VehicleGunnerPanel { Visible = false };
        _gunnerPanel.Moved += pos => _gunnerPosition = pos;
        Place(screen, _gunnerPanel, _gunnerPosition);
        _stacked = false;
    }

    private static void Place(LayoutContainer screen, VehicleHudPanel panel, Vector2? position)
    {
        screen.AddChild(panel);
        if (position is { } pos)
        {
            LayoutContainer.SetAnchorPreset(panel, LayoutContainer.LayoutPreset.TopLeft);
            LayoutContainer.SetPosition(panel, pos);
            return;
        }

        LayoutContainer.SetAnchorPreset(panel, LayoutContainer.LayoutPreset.CenterBottom);
        LayoutContainer.SetGrowHorizontal(panel, LayoutContainer.GrowDirection.Both);
        LayoutContainer.SetGrowVertical(panel, LayoutContainer.GrowDirection.Begin);
        LayoutContainer.SetMarginBottom(panel, PanelMargin);
    }

    private void SetStacked(bool stacked)
    {
        if (_stacked == stacked || _gunnerPosition != null || _gunnerPanel == null || _panel == null)
            return;

        _stacked = stacked;
        var margin = stacked ? PanelMargin - _panel.MinSize.Y - StackGap : PanelMargin;
        LayoutContainer.SetMarginBottom(_gunnerPanel, margin);
    }

    private void OnSpeedLimitChanged(float limit)
    {
        if (_vehicle is { } vehicle)
            _entMan.RaisePredictiveEvent(new VehicleSpeedLimitEvent(_entMan.GetNetEntity(vehicle), limit));
    }

    private void OnScreenUnload()
    {
        _panel?.Orphan();
        _panel = null;
        _gunnerPanel?.Orphan();
        _gunnerPanel = null;
    }

    private void Hide()
    {
        if (_panel != null)
            _panel.Visible = false;

        if (_gunnerPanel != null)
            _gunnerPanel.Visible = false;
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_active || _panel == null || _gunnerPanel == null)
            return;

        if (!_entMan.TryGetComponent<VehicleOccupantComponent>(_player.LocalEntity, out var occupant))
        {
            Hide();
            _vehicle = null;
            return;
        }

        var vehicle = occupant.Vehicle;
        var snap = _vehicle != vehicle;
        _vehicle = vehicle;

        var driver = occupant.Role == VehicleSeatRole.Driver &&
                     _entMan.HasComponent<GridVehicleMoverComponent>(vehicle);
        var gunner = _entMan.TryGetComponent<VehicleSeatsComponent>(vehicle, out var seats) &&
                     seats.Gunner == _player.LocalEntity &&
                     _entMan.HasComponent<VehicleTurretComponent>(vehicle);

        _panel.Visible = driver;
        _gunnerPanel.Visible = gunner;
        SetStacked(driver && gunner);

        if (driver)
            UpdateDriver(vehicle, snap, args.DeltaSeconds);

        if (gunner)
            UpdateGunner(vehicle, snap);
    }

    private void UpdateDriver(EntityUid vehicle, bool snap, float dt)
    {
        var panel = _panel!;
        var mover = _entMan.GetComponent<GridVehicleMoverComponent>(vehicle);
        var now = _timing.CurTime;
        var speed = MathF.Abs(mover.CurrentSpeed);
        if (mover.Floating && _entMan.TryGetComponent<PhysicsComponent>(vehicle, out var body))
            speed = body.LinearVelocity.Length();

        var engineOn = false;
        var starting = false;
        if (_entMan.TryGetComponent<VehicleEngineComponent>(vehicle, out var engine))
        {
            engineOn = engine.EngineOn;
            starting = engine.EngineOn && now < engine.ReadyAt;
        }

        var load = mover.MaxSpeed > 0f ? Math.Clamp(speed / mover.MaxSpeed, 0f, 1f) : 0f;
        var tach = !engineOn ? 0f : starting ? 2.5f + MathF.Sin((float) now.TotalSeconds * 18f) * 0.6f : 0.9f + load * 5.2f;

        // -- Тяга турбин: на газу почти полная, накатом держит только парение
        var hover = _entMan.HasComponent<HoverVehicleComponent>(vehicle);
        var thrust = 0f;
        if (hover && engineOn && _entMan.TryGetComponent<VehicleSeatsComponent>(vehicle, out var seats))
        {
            var throttle = _entMan.System<GridVehicleMoverSystem>().GetInput(vehicle, seats).Throttle;
            thrust = starting ? 0.3f + MathF.Sin((float) now.TotalSeconds * 12f) * 0.05f
                : throttle != 0f ? MathF.Max(0.75f, load) : 0.15f + load * 0.45f;
        }

        var fuel = 1f;
        var hasFuel = true;
        if (_entMan.TryGetComponent<VehicleFuelComponent>(vehicle, out var fuelComp) &&
            _entMan.System<SharedSolutionContainerSystem>().TryGetSolution(vehicle, fuelComp.Solution, out _, out var solution) &&
            solution.MaxVolume > 0)
        {
            var amount = _entMan.System<VehicleFuelSystem>().GetFuel(vehicle, fuelComp);
            fuel = amount.Float() / solution.MaxVolume.Float();
            hasFuel = amount > 0;
        }

        var k = snap ? 1f : 1f - MathF.Exp(-NeedleResponse * dt);
        panel.Speed += (speed * KmhPerTile - panel.Speed) * k;
        panel.Tach += (tach - panel.Tach) * k;
        panel.Thrust += (thrust - panel.Thrust) * k;
        panel.Hover = hover;
        panel.MinSpeedLimit = mover.MinSpeedLimit;
        if (!panel.DraggingLimit)
            panel.SpeedLimit = mover.SpeedLimit;
        panel.Fuel += (fuel - panel.Fuel) * k;
        panel.Hull = GetHull(vehicle);
        panel.EngineOn = engineOn;
        panel.EngineStarting = starting;
        panel.HasFuel = hasFuel;
        panel.Handbrake = mover.Handbrake;
        panel.Lights = _entMan.TryGetComponent<VehicleSpotlightComponent>(vehicle, out var light) && light.Enabled;
        panel.Boost = _entMan.TryGetComponent<VehicleOverchargeComponent>(vehicle, out var boost) && now < boost.ActiveUntil;
        panel.Gear = mover.CurrentSpeed < -0.05f ? "R" : speed > 0.05f ? "D" : "N";
        panel.MaxSpeed = mover.MaxSpeed * KmhPerTile;
        if (snap)
            panel.Title = _entMan.GetComponent<MetaDataComponent>(vehicle).EntityName;
    }

    private void UpdateGunner(EntityUid vehicle, bool snap)
    {
        var panel = _gunnerPanel!;
        var turret = _entMan.GetComponent<VehicleTurretComponent>(vehicle);
        if (snap)
            panel.Title = _entMan.GetComponent<MetaDataComponent>(vehicle).EntityName;

        panel.Azimuth = (float) VehicleTurretSystem.GetLocalRotation(turret, _entMan.GetComponent<TransformComponent>(vehicle)).Degrees;
        panel.Shells.Clear();
        panel.Selected = -1;

        if (!_entMan.System<VehicleTurretSystem>().TryGetSelectedWeapon(vehicle, turret, out var weapon, out var gun))
        {
            panel.Weapon = string.Empty;
            panel.Ammo = 0;
            panel.Capacity = 0;
            panel.Reload = 1f;
            return;
        }

        panel.Weapon = _entMan.GetComponent<MetaDataComponent>(weapon.Value).EntityName;

        var count = new GetAmmoCountEvent();
        _entMan.EventBus.RaiseLocalEvent(weapon.Value, ref count);
        panel.Ammo = count.Count;
        panel.Capacity = count.Capacity;

        var reload = 1f / MathF.Max(gun.FireRateModified, 0.01f);
        var left = (float) (gun.NextFire - _timing.CurTime).TotalSeconds;
        panel.Reload = reload > 0f ? Math.Clamp(1f - left / reload, 0f, 1f) : 1f;

        if (!_entMan.TryGetComponent<VehicleShellMagazineComponent>(weapon.Value, out var mag))
            return;

        for (var i = 0; i < mag.Shells.Count; i++)
        {
            var shell = mag.Shells[i];
            var shells = i < mag.Counts.Count ? mag.Counts[i] : 0;
            panel.Shells.Add((shell.Name.Id, shells, shell.Capacity));
        }

        panel.Selected = mag.Selected;
    }

    private float GetHull(EntityUid vehicle)
    {
        if (!_entMan.TryGetComponent<VehicleIntegrityComponent>(vehicle, out var integrity) ||
            !_entMan.TryGetComponent<DamageableComponent>(vehicle, out var damageable) ||
            integrity.MaxIntegrity <= 0f)
        {
            return 1f;
        }

        return Math.Clamp(1f - damageable.TotalDamage.Float() / integrity.MaxIntegrity, 0f, 1f);
    }
}
