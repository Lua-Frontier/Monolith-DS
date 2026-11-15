/* LuaCorp - Native navigation teaching window smoke tests.
 * This file is licensed under AGPLv3. See AGPLv3.txt for details. */
using System.Collections.Generic;
using System.Reflection;
using Content.Client._LuaM.Guidebook;
using Content.Client._Mono.FireControl.UI;
using Content.Client._Goobstation.Research.UI;
using Content.Shared._Goobstation.Research;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;
using Content.Client.Salvage.UI;
using Content.Client.Shuttles.UI;
using Robust.Shared.GameObjects;
using Robust.Client.UserInterface;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class GuidebookNavigationExamplesTests
{
    [Test]
    public async Task NativeNavigationWindowsOpenWithoutBuiAndReleaseLocalEntities()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var entities = pair.Client.ResolveDependency<IEntityManager>();
        var root = pair.Client.ResolveDependency<IUserInterfaceManager>().RootControl;
        foreach (var mode in new[] { "shuttle", "weapons", "expedition", "research" })
        {
            var before = 0;
            await pair.Client.WaitAssertion(() =>
            {
                before = entities.EntityCount;
                var windowField = typeof(GuideNavigationUI).GetField("_window", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var open = typeof(GuideNavigationUI).GetMethod("OpenExample", BindingFlags.Instance | BindingFlags.NonPublic)!;
                using (var example = new GuideNavigationUI())
                {
                    Assert.That(example.TryParseTag(new Dictionary<string, string> { ["Mode"] = mode }, out _), Is.True);
                    root.AddChild(example);
                    open.Invoke(example, null);
                    var window = windowField.GetValue(example);
                    Assert.That(window, mode switch
                    {
                        "shuttle" => Is.TypeOf<ShuttleConsoleWindow>(),
                        "weapons" => Is.TypeOf<FireControlWindow>(),
                        "research" => Is.TypeOf<FancyResearchConsoleMenu>(),
                        _ => Is.TypeOf<SalvageExpeditionWindow>(),
                    });
                    if (window is ShuttleConsoleWindow shuttle)
                    {
                        shuttle.SwitchMode(ShuttleConsoleWindow.ShuttleConsoleMode.Map);
                        shuttle.SwitchMode(ShuttleConsoleWindow.ShuttleConsoleMode.Dock);
                        shuttle.SwitchMode(ShuttleConsoleWindow.ShuttleConsoleMode.Nav);
                    }
                    if (window is FireControlWindow weapons)
                    {
                        Assert.That(weapons.WeaponsList.Count, Is.EqualTo(3));
                        weapons.OnServerRefresh?.Invoke();
                    }
                    if (window is FancyResearchConsoleMenu research)
                    {
                        var technology = pair.Client.ResolveDependency<IPrototypeManager>().Index(new ProtoId<TechnologyPrototype>("BasicParts"));
                        research.SelectTech(technology, ResearchAvailability.Available);
                        Assert.That(research.CurrentTech?.Id, Is.EqualTo("BasicParts"));
                        Assert.That(research.GuidebookReadOnly, Is.True);
                    }
                    if (window is SalvageExpeditionWindow expedition)
                        Assert.That(expedition.Title, Does.Contain("Учебная"));

                    // Leaving the UI tree closes the tutorial, like switching guidebook pages.
                    example.Orphan();
                }
            });
            // Native EntityPrototypeView queues icon deletion in ExitedTree; the next real tick owns it.
            await pair.RunTicksSync(1);
            await pair.Client.WaitAssertion(() => Assert.That(entities.EntityCount, Is.EqualTo(before),
                "Closing a native tutorial releases its local entities and queued native icon previews."));
        }
        await pair.CleanReturnAsync();
    }
}
