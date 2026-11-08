using System.Linq;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class SeveredLimbTests
{
    [Test]
    public async Task SeveredArmSurvivesDamageOnTheFloor()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        var damageable = entities.System<DamageableSystem>();

        EntityUid human = default;
        EntityUid arm = default;

        await server.WaitPost(() =>
        {
            human = entities.SpawnAtPosition("MobHuman", map.GridCoords);
            arm = entities.System<SharedBodySystem>()
                .GetBodyChildrenOfType(human, BodyPartType.Arm, symmetry: BodyPartSymmetry.Right)
                .First().Id;
        });
        await pair.RunTicksSync(5);

        var severed = false;
        for (var i = 0; i < 40 && !severed; i++)
        {
            await server.WaitPost(() =>
            {
                damageable.TryChangeDamage(arm, new DamageSpecifier { DamageDict = { ["Slash"] = 10 } }, true, canSever: true);
                severed = entities.GetComponent<BodyPartComponent>(arm).Body == null;
            });
            await pair.RunTicksSync(2);
        }

        Assert.That(severed, Is.True, "the arm was never severed");

        await server.WaitPost(() =>
        {
            damageable.TryChangeDamage(arm, new DamageSpecifier { DamageDict = { ["Slash"] = 100, ["Blunt"] = 100 } }, true);
        });
        await pair.RunTicksSync(90);

        var clientHasArm = false;
        await pair.Client.WaitPost(() =>
        {
            clientHasArm = pair.Client.EntMan.TryGetEntity(entities.GetNetEntity(arm), out var local)
                && pair.Client.EntMan.EntityExists(local);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(entities.EntityExists(arm), Is.True, "the severed arm was deleted on the floor");
            Assert.That(clientHasArm, Is.True, "the client lost the severed arm");
        });

        await pair.CleanReturnAsync();
    }
}
