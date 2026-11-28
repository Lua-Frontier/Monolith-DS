using System.Collections.Generic;
using Content.Client.Cargo.UI;
using Content.Server.Cargo.Systems;
using Content.Shared._LuaM.Cargo;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Paper;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._LuaM;

/// <summary>
/// The Colossus kitchen and the MMC Hokkaido bounty consoles: own texts, own order slips and who may skip a task.
/// </summary>
[TestFixture]
public sealed class FactionBountyConsoleTests
{
    // console, locale prefix, order slip, a bounty of its station
    private static readonly object[] Consoles =
    {
        new object[] { "LuaMComputerKitchenBounty", "kitchen-", "LuaMPaperKitchenOrderManifest", "CCBountyCottonBun" },
        new object[] { "LuaMComputerTabletopKitchenBounty", "kitchen-", "LuaMPaperKitchenOrderManifest", "CCBountyCottonBun" },
        new object[] { "LuaMComputerMMCBounty", "mmc-", "LuaMPaperMMCBountyManifest", "MMCBountyT2IndustrialParts" },
    };

    [Test]
    [TestCaseSource(nameof(Consoles))]
    public async Task ConsoleHasOwnTextsAndSlip(string console, string prefix, string slip, string bounty)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var loc = server.ResolveDependency<ILocalizationManager>();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var factory = server.ResolveDependency<IComponentFactory>();
            var proto = server.ProtoMan.Index<EntityPrototype>(console);

            Assert.That(proto.TryComp<BountyConsoleLocaleComponent>(out var locale, factory), $"{console} has no locale prefix");
            Assert.That(locale!.Prefix, Is.EqualTo(prefix));
            Assert.That(proto.TryComp<CargoBountyConsoleComponent>(out var bountyConsole, factory));
            Assert.That(bountyConsole!.BountyLabelId.Id, Is.EqualTo(slip));

            Assert.Multiple(() =>
            {
                // own texts where there are some, the common ones for the rest
                Assert.That(BountyConsoleLoc.Get(prefix, "bounty-console-menu-title"),
                    Is.EqualTo(loc.GetString(prefix + "bounty-console-menu-title")).And.Not.EqualTo(loc.GetString("bounty-console-menu-title")));
                Assert.That(BountyConsoleLoc.Get(prefix, "bounty-console-manifest-label", ("item", "x")),
                    Is.Not.EqualTo(loc.GetString("bounty-console-manifest-label", ("item", "x"))));
                Assert.That(BountyConsoleLoc.Get(prefix, "bounty-console-reward-label", ("reward", "5")),
                    Is.EqualTo(loc.GetString("bounty-console-reward-label", ("reward", "5"))));
                Assert.That(BountyConsoleLoc.Get(null, "bounty-console-menu-title"), Is.EqualTo(loc.GetString("bounty-console-menu-title")));
            });

            // the slip the console prints
            var label = entities.SpawnEntity(slip, MapCoordinates.Nullspace);
            var data = new CargoBountyData(server.ProtoMan.Index<CargoBountyPrototype>(bounty), 1);
            entities.System<CargoSystem>().SetupBountyLabel(label, label, data, localePrefix: prefix);
            var content = entities.GetComponent<PaperComponent>(label).Content;
            var header = FormattedMessage.EscapeText(loc.GetString(prefix + "bounty-manifest-header", ("id", data.Id)));
            Assert.That(content, Does.Contain(header).And.Contain(loc.GetString(prefix + "bounty-manifest-list-start")));
            Assert.That(content, Does.Not.Contain(loc.GetString("bounty-manifest-list-start")));
            entities.DeleteEntity(label);
        });

        await pair.Client.WaitAssertion(() =>
        {
            var clientLoc = pair.Client.ResolveDependency<ILocalizationManager>();
            var menu = new CargoBountyMenu();
            menu.SetLocalePrefix(prefix);
            Assert.That(menu.Title, Is.EqualTo(clientLoc.GetString(prefix + "bounty-console-menu-title")));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SkipAccess()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var reader = entities.System<AccessReaderSystem>();
            var access = entities.System<SharedAccessSystem>();

            EntityUid User(params string[] tags)
            {
                var user = entities.SpawnEntity(null, MapCoordinates.Nullspace);
                entities.EnsureComponent<AccessComponent>(user);
                access.TrySetTags(user, new HashSet<ProtoId<AccessLevelPrototype>>(System.Array.ConvertAll(tags, t => new ProtoId<AccessLevelPrototype>(t))));
                return user;
            }

            var kitchen = entities.SpawnEntity("LuaMComputerKitchenBounty", map.GridCoords);
            var kitchenTabletop = entities.SpawnEntity("LuaMComputerTabletopKitchenBounty", map.GridCoords);
            var mmc = entities.SpawnEntity("LuaMComputerMMCBounty", map.GridCoords);
            var common = entities.SpawnEntity("ComputerCargoBounty", map.GridCoords);

            var nobody = User();
            var employee = User("Mieyo");
            var sergeant = User("USSPHigh");

            Assert.Multiple(() =>
            {
                Assert.That(reader.IsAllowed(nobody, kitchen), "anyone skips a kitchen order");
                Assert.That(reader.IsAllowed(nobody, kitchenTabletop), "anyone skips a kitchen order on the tabletop console");
                Assert.That(reader.IsAllowed(employee, mmc), "an MMC employee can't skip an MMC task");
                Assert.That(reader.IsAllowed(nobody, mmc), Is.False, "anyone skips an MMC task");
                Assert.That(reader.IsAllowed(sergeant, mmc), Is.False, "USSPHigh still skips MMC tasks");
                Assert.That(reader.IsAllowed(nobody, common), Is.False, "the common bounty console lost its access");
            });
        });

        await pair.CleanReturnAsync();
    }
}
