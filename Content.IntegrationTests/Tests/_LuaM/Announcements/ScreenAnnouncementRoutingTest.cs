using Content.Shared._Rat.Overwatch;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Robust.Shared.Map;
using System.Reflection;
using Content.Client._LuaM.Announcements;
using Content.Shared._LuaM.CCVar;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Communications;
using Content.Shared.Access.Components;
using Content.Shared.Communications;
using Content.Shared._LuaM.Announcements;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests._LuaM.Announcements;

[TestFixture]
public sealed class ScreenAnnouncementRoutingTest
{
    [Test]
    public async Task EveryFactionConsoleAndPortableMonitorCarriesExplicitRouting()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            foreach (var (prototype, style) in new[]
                     {
                         ("TSFComputerComms", ScreenAnnouncementStyle.Tsf),
                         ("RogueComputerComms", ScreenAnnouncementStyle.Vanguard),
                         ("MieyoComputerComms", ScreenAnnouncementStyle.Mmc),
                         ("SyndicateComputerComms", ScreenAnnouncementStyle.SystemRed),
                         ("WizardComputerComms", ScreenAnnouncementStyle.SystemPurple),
                         ("ComputerComms", ScreenAnnouncementStyle.System),
                     })
            {
                var console = entities.SpawnEntity(prototype, MapCoordinates.Nullspace);
                var communications = entities.GetComponent<CommunicationsConsoleComponent>(console);
                Assert.That(communications.ScreenStyle, Is.EqualTo(style), prototype);
                if (prototype == "TSFComputerComms")
                {
                    Assert.That(communications.Title.ToString(), Is.EqualTo("comms-console-announcement-title-tsf"));
                    Assert.That(communications.Color, Is.EqualTo(Color.FromHex("#5ABEFF")));
                }
                entities.DeleteEntity(console);
            }
            foreach (var (prototype, fixedPrototype, faction) in new[]
                     {
                         ("TSFHandheldOverwatchConsole", "TSFOverwatchComputer", "TSF"),
                         ("PDVHandheldOverwatchConsole", "PDVOverwatchComputer", "PDV"),
                         ("MMCHandheldOverwatchConsole", "MMCOverwatchComputer", "MMC"),
                         ("MDHandheldOverwatchConsole", "MDOverwatchComputer", "MD"),
                         ("CCHandheldOverwatchConsole", "CCTCOverwatchComputer", "Colonial"),
                         ("USSPHandheldOverwatchConsole", "USSPOverwatchComputer", "USSP"),
                         ("VGHandheldOverwatchConsole", "VGOverwatchComputer", "TheViperGroup"),
                     })
            {
                var handheld = entities.SpawnEntity(prototype, MapCoordinates.Nullspace);
                var console = entities.SpawnEntity(fixedPrototype, MapCoordinates.Nullspace);
                Assert.That(entities.GetComponent<OverwatchConsoleComponent>(handheld).Faction.Id,
                    Is.EqualTo(faction), prototype);
                Assert.That(entities.GetComponent<OverwatchConsoleComponent>(handheld).Faction,
                    Is.EqualTo(entities.GetComponent<OverwatchConsoleComponent>(console).Faction));
                entities.DeleteEntity(handheld);
                entities.DeleteEntity(console);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task FilterExclusionAndSingleDelivery()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var (server, client) = pair;
        ScreenAnnouncementUIController controller = default!;
        ScreenAnnouncementQueue queue = default!;
        var cfg = client.ResolveDependency<IConfigurationManager>();
        var enabled = false;
        EntityUid? source = null;
        EntityUid? console = null;
        NetEntity sourceNet = default;
        var serverCfg = server.ResolveDependency<IConfigurationManager>();
        var originalPvs = serverCfg.GetCVar(Robust.Shared.CVars.NetPVS);
        var chat = server.System<ChatSystem>();
        var session = server.ResolveDependency<IPlayerManager>().Sessions.Single();

        // Observe the actual client handler's queue. Event subscriptions are locked after startup.
        await client.WaitPost(() =>
        {
            controller = client.ResolveDependency<IUserInterfaceManager>().GetUIController<ScreenAnnouncementUIController>();
            queue = (ScreenAnnouncementQueue) typeof(ScreenAnnouncementUIController)
                .GetField("_queue", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(controller)!;
            enabled = cfg.GetCVar(LuaMCVars.ScreenAnnouncementsEnabled);
            cfg.SetCVar(LuaMCVars.ScreenAnnouncementsEnabled, true);
            controller.Clear();
        });
        try
        {
            // An announcement excluded by chat's filter must not leak through the screen event.
            await server.WaitPost(() => chat.DispatchFilteredAnnouncement(Filter.Empty(), "excluded",
                playSound: false));
            await pair.RunTicksSync(5);
            await client.WaitAssertion(() =>
            {
                Assert.That(queue.Current, Is.Null);
                Assert.That(queue.PendingCount, Is.Zero);
            });

            await server.WaitPost(() => chat.DispatchFilteredAnnouncement(Filter.Empty().AddPlayer(session),
                "included", sender: "custom RP sender", playSound: false,
                colorOverride: Color.Red, style: ScreenAnnouncementStyle.Vanguard));
            await pair.RunTicksSync(5);
            await client.WaitAssertion(() =>
            {
                Assert.That(queue.Current, Is.Not.Null);
                Assert.That(queue.PendingCount, Is.Zero, "The same announcement must not enqueue twice.");
                var ev = queue.Current!;
                Assert.Multiple(() =>
                {
                    Assert.That(ev.Message, Is.EqualTo("included"));
                    Assert.That(ev.Title, Is.EqualTo("custom RP sender"));
                    Assert.That(ev.Style, Is.EqualTo(ScreenAnnouncementStyle.Vanguard));
                    Assert.That(ev.Accent, Is.EqualTo(Color.Red));
                    Assert.That(ev.Portrait, Is.Null, "An explicit administrative faction has no live speaker.");
                });
                controller.Clear();
            });

            // The Server channel remains vanilla chat-only, including administrative messages.
            await server.WaitPost(() => server.ResolveDependency<IChatManager>().DispatchServerAnnouncement("server"));
            await pair.RunTicksSync(5);
            await client.WaitAssertion(() =>
            {
                Assert.That(queue.Current, Is.Null, "Server notifications must not create HUD banners.");
                Assert.That(queue.PendingCount, Is.Zero);
            });
            await client.WaitPost(controller.Clear);

            // Faction communications consoles select their configured presentation.
            // Exercise its real event handler and network delivery, rather than calling chat directly.
            await server.WaitPost(() =>
            {
                console = server.EntMan.SpawnEntity("TSFComputerComms", MapCoordinates.Nullspace);
                server.EntMan.EventBus.RaiseLocalEvent(console.Value,
                    new CommunicationsConsoleAnnounceMessage("ordinary faction console"));
            });
            await pair.RunTicksSync(5);
            await client.WaitAssertion(() =>
            {
                Assert.That(queue.Current, Is.Not.Null);
                Assert.That(queue.PendingCount, Is.Zero);
                Assert.That(queue.Current!.Message, Does.StartWith("ordinary faction console"));
                Assert.That(queue.Current.Style, Is.EqualTo(ScreenAnnouncementStyle.Tsf));
                Assert.That(queue.Current.Portrait, Is.Null, "An unknown console author must not gain a fabricated portrait.");
                controller.Clear();
            });

            // The source is never replicated: the receiver must reconstruct from the serialized snapshot.
            await server.WaitPost(() =>
            {
                // The test pool disables PVS by default; enable normal visibility filtering here.
                serverCfg.SetCVar(Robust.Shared.CVars.NetPVS, true);
                source = server.EntMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
                var beret = server.EntMan.SpawnEntity("ClothingHeadHatBeret", MapCoordinates.Nullspace);
                Assert.That(server.System<InventorySystem>().TryEquip(source.Value, beret, "head", force: true), Is.True);
                server.EntMan.GetComponent<HumanoidAppearanceComponent>(source.Value).EyeColor = Color.Lime;
                sourceNet = server.EntMan.GetNetEntity(source.Value);
                // Give this test console unrestricted access and clear its first-message cooldown.
                server.EntMan.RemoveComponent<AccessReaderComponent>(console!.Value);
                server.EntMan.GetComponent<CommunicationsConsoleComponent>(console.Value).AnnouncementCooldownRemaining = 0;
                server.EntMan.EventBus.RaiseLocalEvent(console.Value,
                    new CommunicationsConsoleAnnounceMessage("remote portrait") { Actor = source.Value });
            });
            await pair.RunTicksSync(5);
            await client.WaitAssertion(() =>
            {
                Assert.That(client.EntMan.TryGetEntity(sourceNet, out _), Is.False, "Portrait must not depend on source PVS.");
                Assert.That(queue.Current, Is.Not.Null);
                Assert.That(queue.PendingCount, Is.Zero);
                Assert.That(queue.Current!.Message, Is.EqualTo("remote portrait"), "No sender suffix is appended to the message.");
                Assert.That(queue.Current!.Style, Is.EqualTo(ScreenAnnouncementStyle.Tsf),
                    "The console selects the faction presentation independently of the author.");
                var portrait = queue.Current!.Portrait;
                Assert.That(portrait, Is.Not.Null);
                Assert.That(portrait!.Species, Is.EqualTo("Human"));
                Assert.That(portrait.Name, Is.Not.Empty);
                Assert.That(portrait.EyeColor, Is.EqualTo(Color.Lime));
                Assert.That(portrait.Equipment.Single(e => e.Slot == "head").Prototype, Is.EqualTo("ClothingHeadHatBeret"));
            });
        }
        finally
        {
            await server.WaitPost(() => serverCfg.SetCVar(Robust.Shared.CVars.NetPVS, originalPvs));
            if (source is { } uid)
                await server.WaitPost(() => server.EntMan.DeleteEntity(uid));
            if (console is { } consoleUid)
                await server.WaitPost(() => server.EntMan.DeleteEntity(consoleUid));
            await client.WaitPost(() =>
            {
                controller.Clear();
                cfg.SetCVar(LuaMCVars.ScreenAnnouncementsEnabled, enabled);
            });
        }

        await pair.CleanReturnAsync();
    }
}
