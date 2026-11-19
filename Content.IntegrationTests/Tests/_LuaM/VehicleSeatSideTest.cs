using System.Numerics;
using Content.Shared._LuaM.Vehicles.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._LuaM;

// -- У открытой техники водитель сидит у левого борта: справа на экране при виде вниз, слева при виде вверх,
// -- на дальней стороне при виде вправо и на ближней при виде влево
[TestFixture]
public sealed class VehicleSeatSideTest
{
    [Test]
    public async Task DriverSitsOnLeftSide()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var factory = server.ResolveDependency<IComponentFactory>();

        await server.WaitAssertion(() =>
        {
            var checkedAny = false;
            foreach (var proto in protoMan.EnumeratePrototypes<EntityPrototype>())
            {
                if (proto.Abstract ||
                    !proto.TryGetComponent<VehicleRiderVisualsComponent>(out var riders, factory) ||
                    !proto.TryGetComponent<VehicleSeatsComponent>(out var seats, factory) ||
                    seats.Seats.Count != 2)
                {
                    continue;
                }

                var driver = seats.Seats.IndexOf(VehicleSeatRole.Driver);
                if (driver < 0)
                    continue;

                var other = 1 - driver;

                // -- Посадка друг за другом (квадроцикл): борта у мест общие, правило не применимо
                if (riders.SeatOffsets.TryGetValue(Direction.South, out var south) && south.Count == 2)
                {
                    var delta = south[driver] - south[other];
                    if (MathF.Abs(delta.X) <= MathF.Abs(delta.Y))
                        continue;
                }

                checkedAny = true;
                Assert.Multiple(() =>
                {
                    Check(proto.ID, riders, Direction.South, driver, other, (d, o) => d.X > o.X);
                    Check(proto.ID, riders, Direction.North, driver, other, (d, o) => d.X < o.X);
                    Check(proto.ID, riders, Direction.East, driver, other, (d, o) => d.Y > o.Y);
                    Check(proto.ID, riders, Direction.West, driver, other, (d, o) => d.Y < o.Y);
                });
            }

            Assert.That(checkedAny, Is.True);
        });

        await pair.CleanReturnAsync();
    }

    private static void Check(string id, VehicleRiderVisualsComponent riders, Direction dir, int driver, int other,
        Func<Vector2, Vector2, bool> onLeftSide)
    {
        Assert.That(riders.SeatOffsets.TryGetValue(dir, out var offsets) && offsets.Count == 2, Is.True,
            $"{id}: no seat offsets for {dir}");
        Assert.That(onLeftSide(offsets![driver], offsets[other]), Is.True,
            $"{id}: driver is not on the left side when facing {dir}");
    }
}
