/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using Content.Server.Atmos.Monitor.Components;
using Content.Server.Atmos.Monitor.Systems;
using Content.Shared.Atmos.Monitor.Components;
using Content.Shared.DeviceNetwork.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class AirAlarmModeSyncTests
{
    [TestPrototypes]
    private const string Alarm = """
        - type: entity
          id: LuaMTestModeSyncAlarm
          components:
          - type: AirAlarm
          - type: AtmosAlarmable
          - type: UserInterface
            interfaces:
              enum.SharedAirAlarmInterfaceKey.Key:
                type: AirAlarmBoundUserInterface
          - type: DeviceNetwork
            receiveFrequency: 27183
            transmitFrequency: 27183
        """;

    [TestCase(false)]
    [TestCase(true)]
    public async Task LastLocalModeReachesPeer(bool changeTwiceInSameTick)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.ResolveDependency<IEntityManager>();
        var alarms = server.System<AirAlarmSystem>();
        EntityUid source = default;
        EntityUid peer = default;

        await server.WaitAssertion(() =>
        {
            source = entities.SpawnEntity("LuaMTestModeSyncAlarm", map.GridCoords);
            peer = entities.SpawnEntity("LuaMTestModeSyncAlarm", map.GridCoords);
            var address = entities.GetComponent<DeviceNetworkComponent>(source).Address;
            alarms.SetMode(source, address, AirAlarmMode.WideFiltering, uiOnly: false);
            if (changeTwiceInSameTick)
                alarms.SetMode(source, address, AirAlarmMode.Filtering, uiOnly: false);
        });

        await server.WaitRunTicks(8);
        await server.WaitAssertion(() =>
        {
            var expected = changeTwiceInSameTick ? AirAlarmMode.Filtering : AirAlarmMode.WideFiltering;
            Assert.That(entities.GetComponent<AirAlarmComponent>(source).CurrentMode, Is.EqualTo(expected));
            Assert.That(entities.GetComponent<AirAlarmComponent>(peer).CurrentMode, Is.EqualTo(expected));
        });
        await pair.CleanReturnAsync();
    }
}
