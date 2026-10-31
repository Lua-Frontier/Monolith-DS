using Robust.Shared.GameStates;

namespace Content.Shared._LuaM.Cowboy;

/// <summary>
/// Cowboy trait. Pistols, revolvers and laser pistols lose the dual wield penalties, sawn-off shotguns can be dual
/// wielded, long guns (shotguns, rifles, SMGs, snipers, machine guns), launchers and other energy weapons can not be fired.
/// Guns are told apart by the prototypes they inherit from.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CowboyComponent : Component
{
    /// <summary>
    /// Guns inheriting from these lose the dual wield penalties. The small energy bases are the laser pistols.
    /// Checked before the forbidden list, since small energy guns inherit from the big energy bases.
    /// </summary>
    [DataField]
    public HashSet<string> PistolBases = new()
    {
        "BaseWeaponPistol",
        "BaseWeaponRevolver",
        "BaseWeaponBatterySmall",
        "BaseWeaponPowerCellSmall",
        // pistols that are not made from the pistol bases
        "WeaponPistolEchis",
        "WeaponPistolMercenaryEchis",
        "WeaponPistolAnaconda", // also the registered one, HAWK-4 and the Dragoon revolver
    };

    /// <summary>
    /// Guns inheriting from these can be fired and dual wielded, even though they are shotguns.
    /// </summary>
    [DataField]
    public HashSet<string> SawnOffBases = new() { "WeaponShotgunSawn", "WeaponTBShotgunSawn" };

    /// <summary>
    /// Guns inheriting from these can not be fired.
    /// </summary>
    [DataField]
    public HashSet<string> ForbiddenBases = new()
    {
        "BaseWeaponShotgun",
        "BaseWeaponRifle",
        "BaseWeaponSubMachineGun",
        "BaseWeaponSniper",
        "BaseWeaponLightMachineGun",
        "BaseWeaponHeavyMachineGun",
        "BaseWeaponBattery",
        "BaseWeaponPowerCell",
        "BaseWeaponLauncher",
        // long guns and launchers that are not made from the bases above
        "WeaponShotgunBulldog",
        "WeaponShotgunJack",
        "WeaponSniperHunter101",
        "WeaponLaserLMG",
        "WeaponLauncherMyoko",
        "WeaponLauncherPanzero",
        "WeaponLauncherMk348",
    };

    [DataField]
    public TimeSpan PopupCooldown = TimeSpan.FromSeconds(1);

    public TimeSpan NextPopup;

    /// <summary>
    /// Gun class per prototype id, so the parents of a gun are walked once and not on every shot.
    /// </summary>
    public readonly Dictionary<string, byte> GunClassCache = new();
}

/// <summary>
/// Marks a sawn-off or pistol that got its dual wield component from the cowboy trait, so it is taken away when dropped.
/// </summary>
[RegisterComponent]
public sealed partial class CowboyGrantedMultishotComponent : Component;
