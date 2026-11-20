using System.Numerics;
using Content.Server.Shuttles.Components;
using Content.Shared.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._LuaM;

/// <summary>
/// A shuttle docked to another one goes through FTL together with it.
/// </summary>
[TestFixture]
public sealed class DockedFtlTest
{
    private static readonly ResPath ShuttlePath = new("/Maps/_NF/Shuttles/Scrap/nugget.yml");

    /// <summary>
    /// A drive that jumps straight to the target, skipping hyperspace.
    /// </summary>
    private const string InstantDrive = "MachineFTLDrive600Cicads";

    [Test]
    [TestCase(false)]
    [TestCase(true)]
    public async Task DockedShuttleJumpsTogether(bool instantDrive)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var from = await pair.CreateTestMap();
        var to = await pair.CreateTestMap();

        var entManager = server.ResolveDependency<IEntityManager>();
        var mapLoader = entManager.System<MapLoaderSystem>();
        var docking = entManager.System<DockingSystem>();
        var shuttles = entManager.System<ShuttleSystem>();
        var xforms = entManager.System<SharedTransformSystem>();

        var main = EntityUid.Invalid;
        var docked = EntityUid.Invalid;

        await server.WaitAssertion(() =>
        {
            entManager.DeleteEntity(from.Grid);
            entManager.DeleteEntity(to.Grid);

            Assert.That(mapLoader.TryLoadGrid(from.MapId, ShuttlePath, out var mainGrid, offset: new Vector2(0f, 0f)));
            Assert.That(mapLoader.TryLoadGrid(from.MapId, ShuttlePath, out var dockedGrid, offset: new Vector2(100f, 0f)));
            main = mainGrid!.Value.Owner;
            docked = dockedGrid!.Value.Owner;

            if (instantDrive)
                entManager.SpawnEntity(InstantDrive, new EntityCoordinates(main, Vector2.Zero));

            var config = docking.GetDockingConfig(docked, main);
            Assert.That(config, Is.Not.Null, "the two shuttles have no docks that fit");
            shuttles.FTLDock((docked, entManager.GetComponent<TransformComponent>(docked)), config!);
        });

        await server.WaitRunTicks(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(docking.AreGridsDocked(main, docked), "the shuttles did not dock");

            shuttles.FTLToCoordinates(main,
                entManager.GetComponent<ShuttleComponent>(main),
                new EntityCoordinates(to.MapUid, new Vector2(500f, 500f)),
                Angle.Zero,
                startupTime: 1f,
                hyperspaceTime: 2f);

            Assert.That(entManager.HasComponent<FTLComponent>(main), "the main shuttle did not start the jump");
        });

        // startup, travel and arrival
        await server.WaitRunTicks(30 * 15);

        await server.WaitAssertion(() =>
        {
            var mainMap = entManager.GetComponent<TransformComponent>(main).MapUid;
            var dockedMap = entManager.GetComponent<TransformComponent>(docked).MapUid;

            Assert.Multiple(() =>
            {
                Assert.That(mainMap, Is.EqualTo(to.MapUid), "the main shuttle did not arrive");
                Assert.That(dockedMap, Is.EqualTo(to.MapUid), "the docked shuttle was left behind");
                Assert.That(docking.AreGridsDocked(main, docked), "the shuttles came apart in the jump");
                Assert.That((xforms.GetWorldPosition(docked) - xforms.GetWorldPosition(main)).Length(), Is.LessThan(40f),
                    "the docked shuttle did not arrive next to the main one");
            });
        });

        await pair.CleanReturnAsync();
    }
}
