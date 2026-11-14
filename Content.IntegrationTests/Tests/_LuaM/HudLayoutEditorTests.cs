using System.Numerics;
using System.Reflection;
using System.IO;
using System.Linq;
using Content.Client._LuaM.Hud;
using Content.Shared._LuaM.CCVar;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Client.Input;
using Robust.Client.ResourceManagement;
using Robust.Shared.Map;
using Content.Client.UserInterface.Screens;
using Content.Client.UserInterface.Controls;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class HudLayoutEditorTests
{
    [Test]
    public async Task PreviewFillsModalAndCancelPreservesConfiguration()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        await pair.Client.WaitAssertion(() =>
        {
            var ui = pair.Client.ResolveDependency<IUserInterfaceManager>();
            var cfg = pair.Client.ResolveDependency<IConfigurationManager>();
            var anchor = new Vector2(cfg.GetCVar(LuaMCVars.LocationAnnouncementX), cfg.GetCVar(LuaMCVars.LocationAnnouncementY));
            var names = new[] { LuaMCVars.LocationAnnouncementX.Name, LuaMCVars.LocationAnnouncementY.Name };
            using (var stored = new MemoryStream())
            {
                try
                {
                    cfg.SetCVar(names[0], 0.2f);
                    cfg.SetCVar(names[1], 0.7f);
                    cfg.SaveToTomlStream(stored, names);
                    cfg.SetCVar(names[0], 0.5f);
                    cfg.SetCVar(names[1], 0.15f);
                    stored.Position = 0;
                    cfg.LoadFromTomlStream(stored);
                    Assert.That(cfg.GetCVar<float>(names[0]), Is.EqualTo(0.2f));
                    Assert.That(cfg.GetCVar<float>(names[1]), Is.EqualTo(0.7f));
                    foreach (var name in names)
                        Assert.That(cfg.GetCVarFlags(name).HasFlag(CVar.ARCHIVE | CVar.CLIENTONLY), Is.True);
                }
                finally
                {
                    cfg.SetCVar(names[0], anchor.X);
                    cfg.SetCVar(names[1], anchor.Y);
                }
            }
            foreach (var size in new[] { new Vector2(640, 480), new Vector2(1920, 1080) })
            {
                var editor = new HudLayoutEditor();
                try
                {
                    var previousScreen = ui.ActiveScreen;
                    editor.OpenFullScreen();
                    Assert.That(ui.ActiveScreen, Is.SameAs(previousScreen), "Menu previews must not take over active gameplay screens.");
                    editor.Measure(size);
                    editor.Arrange(UIBox2.FromDimensions(Vector2.Zero, size));
                    var preview = editor.Children.Single().Children.Single(c => c.Name == "AnnouncementPreview");
                    Assert.That(preview.Size, Is.EqualTo(size));
                    Assert.That(preview.HasKeyboardFocus(), Is.True);
                    var use = new GUIBoundKeyEventArgs(EngineKeyFunctions.Use, BoundKeyState.Down,
                        default, true, Vector2.Zero, Vector2.Zero);
                    preview.GetType().GetMethod("KeyBindDown", BindingFlags.NonPublic | BindingFlags.Instance)!
                        .Invoke(preview, new object[] { use });
                    Assert.That(use.Handled, Is.False, "Mouse gameplay bindings must not prevent the later UIClick binding.");
                    var fields = BindingFlags.Instance | BindingFlags.NonPublic;
                    var anchorField = preview.GetType().GetField("Anchor")!;
                    var before = (Vector2) anchorField.GetValue(preview)!;
                    var title = (string) preview.GetType().GetField("_title", fields)!.GetValue(preview)!;
                    var description = (string) preview.GetType().GetField("_description", fields)!.GetValue(preview)!;
                    var renderer = new LocationAnnouncementRenderer(pair.Client.ResolveDependency<IResourceCache>());
                    var bounds = renderer.GetBounds(preview.PixelSize, before, title, description);
                    var start = preview.GlobalPixelPosition + bounds.TopLeft + new Vector2(10);
                    var delta = new Vector2(40, 50);
                    var finish = start + delta;
                    var uiType = ui.GetType();
                    ui.ControlFocused = preview;
                    uiType.GetMethod("KeyBindDown")!.Invoke(ui, new object[]
                    {
                        new BoundKeyEventArgs(EngineKeyFunctions.UIClick, BoundKeyState.Down,
                            new ScreenCoordinates(start, WindowId.Main), true)
                    });
                    uiType.GetMethod("MouseMove")!.Invoke(ui, new object[]
                    {
                        new MouseMoveEventArgs(delta, new ScreenCoordinates(finish, WindowId.Main))
                    });
                    var after = (Vector2) anchorField.GetValue(preview)!;
                    var moved = renderer.GetBounds(preview.PixelSize, after, title, description);
                    Assert.That(moved.Left, Is.EqualTo(bounds.Left + delta.X).Within(0.01));
                    Assert.That(moved.Top, Is.EqualTo(bounds.Top + delta.Y).Within(0.01));
                    uiType.GetMethod("KeyBindUp")!.Invoke(ui, new object[]
                    {
                        new BoundKeyEventArgs(EngineKeyFunctions.UIClick, BoundKeyState.Up,
                            new ScreenCoordinates(finish, WindowId.Main), true)
                    });
                    uiType.GetMethod("MouseMove")!.Invoke(ui, new object[]
                    {
                        new MouseMoveEventArgs(delta, new ScreenCoordinates(finish + delta, WindowId.Main))
                    });
                    Assert.That((Vector2) anchorField.GetValue(preview)!, Is.EqualTo(after), "Release must stop dragging.");
                    ui.ControlFocused = null;
                    typeof(HudLayoutEditor).GetMethod("FrameUpdate", BindingFlags.NonPublic | BindingFlags.Instance)!
                        .Invoke(editor, new object[] { new FrameEventArgs(0.016f) });
                    Assert.That(editor.Parent, Is.SameAs(ui.ModalRoot), "Resizing must not orphan the active modal.");
                    editor.Close();
                    Assert.That(editor.Parent, Is.Null);
                    Assert.That(ui.KeyboardFocused, Is.Not.SameAs(preview));
                    Assert.That(new Vector2(cfg.GetCVar(LuaMCVars.LocationAnnouncementX), cfg.GetCVar(LuaMCVars.LocationAnnouncementY)), Is.EqualTo(anchor));
                }
                finally
                {
                    editor.Close();
                    editor.Orphan();
                }
            }
            // Starting gameplay reclaims the same cached screen. The editor must release it
            // before ScreenRoot.AddChild and must never put the previous lobby back afterwards.
            var transitionEditor = new HudLayoutEditor();
            try
            {
                transitionEditor.OpenFullScreen();
                var native = ui.WindowRoot.Children.Single(c => c.Name == "NativeHudPreview");
                var gameScreen = native.Children.OfType<DefaultGameScreen>().Single();
                Assert.That(gameScreen.GetWidget<MainViewport>()!.Viewport.Visible, Is.False,
                    "Menu previews must not render an uninitialized world viewport.");
                ui.LoadScreen<DefaultGameScreen>();
                Assert.That(transitionEditor.Parent, Is.Null);
                Assert.That(ui.ActiveScreen, Is.SameAs(gameScreen));
                Assert.That(gameScreen.Parent, Is.Not.Null.And.Not.SameAs(native));
                Assert.That(gameScreen.GetWidget<MainViewport>()!.Viewport.Visible, Is.True);
            }
            finally
            {
                transitionEditor.Close();
                ui.UnloadScreen();
            }
        });
        await pair.CleanReturnAsync();
    }
}
