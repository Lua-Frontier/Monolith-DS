using System.Collections.Generic;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._LuaM;

/// <summary>
/// The Colossus station staff and the TSF don't open each other's doors, and the Director of Care opens only medical ones.
/// </summary>
[TestFixture]
public sealed class ColossusTsfAccessTest
{
    private static readonly string[] ColossusDoors =
    {
        "AirlockColossusSecurityLocked",
        "AirlockColossusCommandLocked",
        "AirlockColossusFrontierCommandLocked",
        "AirlockFrontierLocked",
        "AirlockMailCarrierLocked",
    };

    /// <summary>
    /// Medical doors: the Colossus medbay and the Medical Center. Only the medics open them.
    /// </summary>
    private static readonly string[] MedicalDoors =
    {
        "AirlockMedicalLocked",
        "AirlockCommandChiefMedicalOfficerLocked",
        "HighSecChiefMedicalOfficerLocked",
    };

    private static readonly string[] TsfDoors =
    {
        "AirlockNfsdLocked",
        "AirlockNfsdArmoryLocked",
        "AirlockHeadOfSecurityLocked",
        "WindoorSecureBrigLocked",
        "AirlockTsfEngineering",
        "AirlockExternalGlassNfsdLocked",
    };

    private static readonly string[] TsfJobs =
    {
        "Sheriff", "Bailiff", "SeniorOfficer", "Deputy", "Cadet", "Brigmedic", "NFDetective", "TsfEngineer", "TsfMarsoc",
    };

    [Test]
    public async Task StationAndTsfAreSeparate()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entities = server.EntMan;
        var protos = server.ProtoMan;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var reader = entities.System<AccessReaderSystem>();
            var access = entities.System<SharedAccessSystem>();

            var doors = new Dictionary<string, EntityUid>();
            foreach (var id in ColossusDoors)
                doors[id] = entities.SpawnEntity(id, map.GridCoords);
            foreach (var id in TsfDoors)
                doors[id] = entities.SpawnEntity(id, map.GridCoords);
            foreach (var id in MedicalDoors)
                doors[id] = entities.SpawnEntity(id, map.GridCoords);

            EntityUid User(string job)
            {
                var proto = protos.Index<JobPrototype>(job);
                var tags = new HashSet<ProtoId<AccessLevelPrototype>>(proto.Access);
                foreach (var group in proto.AccessGroups)
                {
                    tags.UnionWith(protos.Index(group).Tags);
                }

                var user = entities.SpawnEntity(null, MapCoordinates.Nullspace);
                entities.EnsureComponent<AccessComponent>(user);
                access.TrySetTags(user, tags);
                return user;
            }

            Assert.Multiple(() =>
            {
                var guard = User("SecurityGuard");
                Assert.That(reader.IsAllowed(guard, doors["AirlockColossusSecurityLocked"]), "the guard can't open the station security door");
                foreach (var id in TsfDoors)
                    Assert.That(reader.IsAllowed(guard, doors[id]), Is.False, $"the guard opens the TSF door {id}");

                var commandant = User("StationRepresentative");
                foreach (var id in ColossusDoors)
                    Assert.That(reader.IsAllowed(commandant, doors[id]), $"the commandant can't open the station door {id}");
                foreach (var id in TsfDoors)
                    Assert.That(reader.IsAllowed(commandant, doors[id]), Is.False, $"the commandant opens the TSF door {id}");
                foreach (var id in MedicalDoors)
                    Assert.That(reader.IsAllowed(commandant, doors[id]), Is.False, $"the commandant opens the medical door {id}");

                foreach (var job in TsfJobs)
                {
                    var tsf = User(job);
                    foreach (var id in ColossusDoors)
                        Assert.That(reader.IsAllowed(tsf, doors[id]), Is.False, $"{job} opens the station door {id}");
                    foreach (var id in MedicalDoors)
                        Assert.That(reader.IsAllowed(tsf, doors[id]), Is.False, $"{job} opens the medical door {id}");
                }

                var director = User("DirectorOfCare");
                foreach (var id in MedicalDoors)
                    Assert.That(reader.IsAllowed(director, doors[id]), $"the Director of Care can't open the medical door {id}");
                foreach (var id in ColossusDoors)
                    Assert.That(reader.IsAllowed(director, doors[id]), Is.False, $"the Director of Care opens the station door {id}");
                foreach (var id in TsfDoors)
                    Assert.That(reader.IsAllowed(director, doors[id]), Is.False, $"the Director of Care opens the TSF door {id}");

                var medic = User("MdMedic");
                Assert.That(reader.IsAllowed(medic, doors["AirlockMedicalLocked"]), "the MD medic can't open the medbay");
                Assert.That(reader.IsAllowed(medic, doors["AirlockCommandChiefMedicalOfficerLocked"]), Is.False, "the MD medic opens the Director of Care door");

                Assert.That(reader.IsAllowed(User("Sheriff"), doors["AirlockNfsdLocked"]), "the TSF colonel can't open the TSF door");
                Assert.That(reader.IsAllowed(User("TsfMarsoc"), doors["AirlockNfsdArmoryLocked"]), "MARSOC can't open the TSF armory");
            });
        });

        await pair.CleanReturnAsync();
    }
}
