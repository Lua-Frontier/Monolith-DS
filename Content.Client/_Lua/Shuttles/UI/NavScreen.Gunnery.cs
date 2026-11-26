using System.Linq;
using Content.Shared._Mono.FireControl;
using Content.Shared._Mono.ShipGuns;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;
using Content.Client.Stylesheets;
using Content.Client._LuaM.UserInterface.Hud; // LuaM

namespace Content.Client.Shuttles.UI;

/// <summary>
/// Weapons panel of the shuttle console, so the pilot can also act as the gunner.
/// </summary>
public sealed partial class NavScreen
{
    public event Action? OnFireControlRefresh;
    public event Action<List<NetEntity>, NetCoordinates>? OnGunneryFire;

    private readonly Dictionary<NetEntity, Button> _weaponButtons = new();
    private readonly Dictionary<NetEntity, ShipGunType> _weaponTypes = new();
    private FireControllableEntry[]? _weapons;
    private bool _gunneryConnected;

    private void InitializeGunnery()
    {
        GunneryRefreshButton.OnPressed += _ => OnFireControlRefresh?.Invoke();
        GunnerySelectAllButton.OnPressed += _ => SelectWeapons(_ => true);
        GunneryUnselectAllButton.OnPressed += _ => SelectWeapons(_ => false);
        GunneryBallisticButton.OnPressed += _ => SelectWeapons(type => type == ShipGunType.Ballistic);
        GunneryEnergyButton.OnPressed += _ => SelectWeapons(type => type == ShipGunType.Energy);
        GunneryMissileButton.OnPressed += _ => SelectWeapons(type => type == ShipGunType.Missile);

        NavRadar.OnRadarClick += OnGunneryRadarClick;
        NavRadar.OnGunneryCursorMove += OnGunneryCursorMove;
    }

    /// <summary>
    /// Shows the weapons of the shuttle's fire control server, or hides the panel when there are none.
    /// </summary>
    public void UpdateWeapons(bool connected, FireControllableEntry[]? weapons)
    {
        _gunneryConnected = connected;
        _weapons = connected ? weapons : null;
        UpdateReloads(_weapons); // LuaM

        GunneryHeader.Visible = connected;
        GunneryControls.Visible = connected;
        UpdateWeaponsPanelVisibility();
        NavRadar.DefaultCursorShape = connected ? CursorShape.Crosshair : CursorShape.Arrow;
        NavRadar.SetGunneryControllables(_weapons);

        GunneryStatus.Lead = Loc.GetString(connected ? "gunnery-window-connected" : "gunnery-window-disconnected").ToUpperInvariant(); // LuaM: callout
        GunneryStatus.Status = connected ? HudStatus.Nominal : HudStatus.Warning; // LuaM

        UpdateWeaponButtons();
        UpdateGunnerySelection();
    }

    /// <summary>
    /// The left panel holds the weapons and the shield, it shows up when there is either.
    /// </summary>
    private void UpdateWeaponsPanelVisibility()
    {
        WeaponsPanel.Visible = _gunneryConnected || ShieldPanel.Visible || _holoCommsAvailable; // LuaM - and the comms button
    }

    private void OnGunneryRadarClick(EntityCoordinates coordinates)
    {
        if (!_gunneryConnected || !NavRadar.GunneryMouseDown)
            return;

        var selected = GetSelectedWeapons();
        if (selected.Count == 0)
            return;

        OnGunneryFire?.Invoke(selected, _entManager.GetNetCoordinates(coordinates));
    }

    /// <summary>
    /// Sends the cursor with no weapons selected: nothing fires, guided missiles just follow it.
    /// </summary>
    private void OnGunneryCursorMove(EntityCoordinates coordinates)
    {
        if (!_gunneryConnected)
            return;

        OnGunneryFire?.Invoke(new List<NetEntity>(), _entManager.GetNetCoordinates(coordinates));
    }

    private List<NetEntity> GetSelectedWeapons()
    {
        return _weaponButtons.Where(kv => kv.Value.Pressed).Select(kv => kv.Key).ToList();
    }

    private void SelectWeapons(Func<ShipGunType?, bool> predicate)
    {
        foreach (var (weapon, button) in _weaponButtons)
        {
            ShipGunType? type = _weaponTypes.TryGetValue(weapon, out var found) ? found : null;
            button.Pressed = predicate(type);
        }

        UpdateGunnerySelection();
    }

    private void UpdateGunnerySelection()
    {
        NavRadar.SetGunnerySelection(GetSelectedWeapons().ToHashSet());
        UpdateWeaponButtonTexts();
    }

    private void UpdateWeaponButtons()
    {
        var weapons = _weapons ?? Array.Empty<FireControllableEntry>();
        var toRemove = new HashSet<NetEntity>(_weaponButtons.Keys);
        _weaponTypes.Clear();

        foreach (var weapon in weapons)
        {
            toRemove.Remove(weapon.NetEntity);

            var entity = _entManager.GetEntity(weapon.NetEntity);
            if (weapon.GunType is { } sentType) // LuaM: the server sends the type
                _weaponTypes[weapon.NetEntity] = sentType;
            else if (_entManager.TryGetComponent<ShipGunTypeComponent>(entity, out var typeComp))
                _weaponTypes[weapon.NetEntity] = typeComp.Type;

            if (_weaponButtons.ContainsKey(weapon.NetEntity))
                continue;

            ShipGunType? gunType = _weaponTypes.TryGetValue(weapon.NetEntity, out var knownType) ? knownType : null; // LuaM
            var button = new Button
            {
                ToggleMode = true,
                Text = weapon.Name,
                StyleClasses = { GunStyle(gunType) }, // LuaM: thin toggle in the weapon type color
                HorizontalExpand = true,
                ClipText = true, // LuaM
                Margin = new Thickness(0, 2), // LuaM: (4, 2) > (0, 2), the list keeps the gap
            };
            AddGunShape(button, GunShape(gunType)); // LuaM
            HudMarquee.Attach(button); // LuaM
            button.OnToggled += _ => UpdateGunnerySelection();

            GunneryWeaponsBox.AddChild(WithReloadBar(weapon.NetEntity, button)); // LuaM: button > button with its reload bar
            _weaponButtons.Add(weapon.NetEntity, button);
        }

        foreach (var weapon in toRemove)
        {
            if (_weaponButtons.Remove(weapon, out var button))
                GunneryWeaponsBox.RemoveChild(button.Parent ?? button); // LuaM: the button sits in a row with its reload bar
            _reloadBars.Remove(weapon); // LuaM
        }

        var hasWeapons = _weaponButtons.Count > 0;
        GunnerySelectAllButton.Disabled = !hasWeapons;
        GunneryUnselectAllButton.Disabled = !hasWeapons;
        GunneryBallisticButton.Disabled = !_weaponTypes.ContainsValue(ShipGunType.Ballistic);
        GunneryEnergyButton.Disabled = !_weaponTypes.ContainsValue(ShipGunType.Energy);
        GunneryMissileButton.Disabled = !_weaponTypes.ContainsValue(ShipGunType.Missile);
    }

    private void UpdateWeaponButtonTexts()
    {
        if (_weapons == null)
            return;

        foreach (var weapon in _weapons)
        {
            if (!_weaponButtons.TryGetValue(weapon.NetEntity, out var button))
                continue;

            // LuaM: texts are only written when they change, every write re-measures the list
            var withAmmo = button.Pressed && weapon.HasManualReload && weapon.AmmoCount.HasValue;
            var text = withAmmo
                ? Loc.GetString("gunnery-gun-select-ammo", ("name", weapon.Name), ("ammo", weapon.AmmoCount!.Value))
                : Loc.GetString("gunnery-gun-select", ("name", weapon.Name));
            if (button.Text != text)
                button.Text = text;
            Color? tint = withAmmo && weapon.AmmoCount!.Value == 0 ? Color.Red : null;
            if (button.ModulateSelfOverride != tint)
                button.ModulateSelfOverride = tint;
        }
    }
}
