#nullable enable
using Content.Client._LuaM.UserInterface.Hud;
using Content.Client.Stylesheets;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.IntegrationTests.Tests._LuaM;

/// <summary>
/// The lobby style scope no longer walks the whole tree every frame: new controls and changed captions
/// must still get the lobby look on the next frame.
/// </summary>
[TestFixture]
public sealed class HudStyleScopeTest
{
    [Test]
    public async Task StylesNewControlsAndChangedCaptions()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        var ui = client.ResolveDependency<IUserInterfaceManager>();

        BoxContainer root = default!;
        BoxContainer hidden = default!;
        Button first = default!;
        Label title = default!;
        await client.WaitPost(() =>
        {
            root = new BoxContainer();
            first = new Button { Text = "Go" };
            title = new Label { Text = "Title", StyleClasses = { LobbyHudStyles.Title } };
            hidden = new BoxContainer { Visible = false };
            root.AddChild(first);
            root.AddChild(title);
            root.AddChild(hidden);
            root.AddChild(new HudStyleScope());
            ui.RootControl.AddChild(root);
        });

        await client.WaitRunTicks(3);
        await client.WaitAssertion(() =>
        {
            Assert.That(first.HasStyleClass(LobbyHudStyles.Button), Is.True);
            Assert.That(first.Text, Is.EqualTo(HudStyleScope.Track("GO")));
            Assert.That(title.Text, Is.EqualTo(HudStyleScope.Track("TITLE")));
        });

        Button added = default!;
        Button shown = default!;
        await client.WaitPost(() =>
        {
            added = new Button { Text = "Next" };
            root.AddChild(added);
            shown = new Button { Text = "Later" };
            hidden.AddChild(shown);
            hidden.Visible = true;
            title.Text = "Other";
        });

        // well under the one second safety walk: these have to come from the change events and the tracked captions
        await client.WaitRunTicks(2);
        await client.WaitAssertion(() =>
        {
            Assert.That(added.HasStyleClass(LobbyHudStyles.Button), Is.True, "a control added later was not styled");
            Assert.That(added.Text, Is.EqualTo(HudStyleScope.Track("NEXT")));
            Assert.That(shown.HasStyleClass(LobbyHudStyles.Button), Is.True, "a control in a part that showed up was not styled");
            Assert.That(title.Text, Is.EqualTo(HudStyleScope.Track("OTHER")), "a changed caption was not rewritten");
        });

        await client.WaitPost(() => root.Orphan());
        await pair.CleanReturnAsync();
    }
}
