using System.Reflection;
using System.Linq;
using Content.Shared._Mono.Company;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Content.Client._LuaM.Announcements;
using Content.Server.Chat.Managers;
using Content.Server._LuaM.Announcements;
using Content.Shared._LuaM.Announcements;
using Content.Shared._LuaM.CCVar;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;

namespace Content.IntegrationTests.Tests._LuaM.Announcements;

[TestFixture]
public sealed class AdminOverwatchRoutingTest
{
    [Test]
    public async Task ServerNoticesStayInChatAndAdministrativeOverwatchUsesItsFactionDefault()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var (server, client) = pair;
        var cfg = client.ResolveDependency<IConfigurationManager>();
        var enabled = cfg.GetCVar(LuaMCVars.ScreenAnnouncementsEnabled);
        ScreenAnnouncementUIController controller = default!;
        ScreenAnnouncementQueue queue = default!;
        string defaultTitle = null!;
        var session = server.PlayerMan.Sessions.Single();
        EntityUid? member = null;
        EntityUid? previous = null;
        await client.WaitPost(() =>
        {
            controller = client.ResolveDependency<IUserInterfaceManager>().GetUIController<ScreenAnnouncementUIController>();
            queue = (ScreenAnnouncementQueue) typeof(ScreenAnnouncementUIController)
                .GetField("_queue", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(controller)!;
            cfg.SetCVar(LuaMCVars.ScreenAnnouncementsEnabled, true);
            controller.Clear();
        });
        try
        {
            await server.WaitPost(() => server.ResolveDependency<IChatManager>().DispatchServerAnnouncement("Round restart"));
            await pair.RunTicksSync(5);
            await client.WaitAssertion(() =>
            {
                Assert.That(queue.Current, Is.Null, "Server-channel messages must not become HUD cards.");
                Assert.That(queue.PendingCount, Is.Zero);
            });
            await server.WaitPost(() =>
            {
                previous = session.AttachedEntity;
                member = server.EntMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
                server.EntMan.EnsureComponent<CompanyComponent>(member.Value).CompanyName = "MMC";
                server.PlayerMan.SetAttachedEntity(session, member);
                var overwatch = server.System<AdminOverwatchAnnouncementSystem>();
                var presentation = overwatch.GetPresentation(ScreenAnnouncementStyle.Mmc);
                Assert.That(presentation.Faction, Is.EqualTo("MMC"));
                defaultTitle = presentation.Title;
            });
            await pair.RunTicksSync(5);
            await client.WaitPost(controller.Clear);
            await server.WaitPost(() => server.System<AdminOverwatchAnnouncementSystem>()
                .Send("Administrative Overwatch", ScreenAnnouncementStyle.Mmc, "  ", playSound: false));
            await pair.RunTicksSync(5);
            await client.WaitAssertion(() =>
            {
                Assert.That(queue.Current, Is.Not.Null);
                Assert.That(queue.Current!.Message, Is.EqualTo("Administrative Overwatch"));
                Assert.That(queue.Current.Style, Is.EqualTo(ScreenAnnouncementStyle.Mmc));
                Assert.That(queue.Current.Title, Does.Contain(defaultTitle));
                Assert.That(queue.Current.Portrait, Is.Null);
                Assert.That(queue.PendingCount, Is.Zero, "Only the Overwatch event should create this card.");
                controller.Clear();
            });
            // A different company's transmission must not leak to this MMC session.
            await server.WaitPost(() => server.System<AdminOverwatchAnnouncementSystem>()
                .Send("Excluded TSF heading", ScreenAnnouncementStyle.Tsf, playSound: false));
            await pair.RunTicksSync(5);
            await client.WaitAssertion(() => Assert.That(queue.Current, Is.Null));
            // Nor can a crafted packet turn a non-company design into a global transmission.
            await server.WaitPost(() => server.System<AdminOverwatchAnnouncementSystem>()
                .Send("Invalid system design", ScreenAnnouncementStyle.System, playSound: false));
            await pair.RunTicksSync(5);
            await client.WaitAssertion(() => Assert.That(queue.Current, Is.Null));
            await server.WaitPost(() =>
            {
                server.EntMan.RemoveComponent<CompanyComponent>(member!.Value);
                server.EntMan.EnsureComponent<CompanyComponent>(member.Value).CompanyName = "TSF";
                server.System<AdminOverwatchAnnouncementSystem>()
                    .Send("Custom heading", ScreenAnnouncementStyle.Tsf, "RP high command", playSound: false);
            });
            await pair.RunTicksSync(5);
            await client.WaitAssertion(() =>
            {
                Assert.That(queue.Current!.Title, Does.Contain("RP high command"));
                Assert.That(queue.Current.Style, Is.EqualTo(ScreenAnnouncementStyle.Tsf));
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                server.PlayerMan.SetAttachedEntity(session, previous);
                if (member != null && server.EntMan.EntityExists(member.Value))
                    server.EntMan.DeleteEntity(member.Value);
            });
            await client.WaitPost(() =>
            {
                controller.Clear();
                cfg.SetCVar(LuaMCVars.ScreenAnnouncementsEnabled, enabled);
            });
        }
        await pair.CleanReturnAsync();
    }
}
