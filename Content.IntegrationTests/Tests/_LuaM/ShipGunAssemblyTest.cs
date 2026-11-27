#nullable enable
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._LuaM.ShipGunAssembly;

namespace Content.IntegrationTests.Tests._LuaM;

/// <summary>
/// Builds ship weapons from their frames: the frame turns into the weapon body on the first barrel,
/// and into the weapon itself after the last step, which has to stay in the world.
/// </summary>
public sealed class ShipGunAssemblyTest : InteractionTest
{
    private const string Plasteel = "Plasteel";
    private const string CableMv = "CableMV";
    private const string CableHv = "CableHV";
    private const string Module = "LuaMShipGunFireControlModule";

    [Test]
    public async Task AssembleL85()
    {
        await SpawnTarget("LuaMShipGunFrameLightBallistic");
        await Interact(Wrench);
        AssertAnchored();

        await InteractUsing("LuaMShipGunBarrelL85");
        AssertPrototype("LuaMShipGunUnfinishedL85");

        await Interact("LuaMShipGunBarrelL85", "LuaMShipGunChargerMk1", Module);
        await InteractUsing(Steel, 5);
        await InteractUsing(Cable, 5);
        await Interact(Screw, Weld);

        await AssertWeaponStays("WeaponTurretL85Autocannon");
    }

    [Test]
    public async Task AssembleHydra()
    {
        await SpawnTarget("LuaMShipGunFrameHeavyArtillery");
        await Interact(Wrench);
        AssertAnchored();

        await InteractUsing("LuaMShipGunBarrelHydra");
        AssertPrototype("LuaMShipGunUnfinishedHydra");

        await Interact("LuaMShipGunBarrelHydra", "LuaMShipGunBarrelHydra", "LuaMShipGunChargerMk4", Module, Module);
        await InteractUsing(Plasteel, 20);
        await InteractUsing(CableHv, 20);
        await Interact(Screw, Weld);

        await AssertWeaponStays("WeaponTurretHydra");
    }

    [Test]
    public async Task AssembleMovableTarnyx()
    {
        await SpawnTarget("LuaMShipGunFrameArtillery");
        await Interact(Wrench);
        AssertAnchored();

        await InteractUsing("LuaMShipGunBarrelTarnyx");
        AssertPrototype("LuaMShipGunUnfinishedTarnyx");

        await Interact("LuaMShipGunChargerMk3", Module);
        await InteractUsing(Plasteel, 10);
        await InteractUsing(CableHv, 10);
        await Interact(Screw, Weld);

        await AssertWeaponStays("WeaponTurretTarnyxMovable");
    }

    private async Task AssertWeaponStays(string weapon)
    {
        AssertPrototype(weapon);

        // the weapon used to vanish right after the last step, give it time to do so
        await RunSeconds(3);
        await CheckTargetChange();

        var uid = SEntMan.GetEntity(Target!.Value);
        Assert.That(SEntMan.EntityExists(uid), Is.True, $"{weapon} disappeared after assembly");
        AssertPrototype(weapon);
        AssertAnchored();
        Assert.That(SEntMan.HasComponent<AssembledShipGunComponent>(uid), Is.True, $"{weapon} lost its assembled price");
    }
}
