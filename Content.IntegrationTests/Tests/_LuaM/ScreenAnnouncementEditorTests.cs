using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.IO;
using Content.Client._LuaM.Announcements;
using Content.Client._LuaM.Hud;
using Content.Shared._LuaM.CCVar;
using Content.Shared._LuaM.Announcements;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Timing;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class ScreenAnnouncementEditorTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public async Task BothHudPreviewsReceiveRealPointerInputAndSaveCancelResetTogether()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        await pair.Client.WaitAssertion(() =>
        {
            var ui = pair.Client.ResolveDependency<IUserInterfaceManager>();
            var cfg = pair.Client.ResolveDependency<IConfigurationManager>();
            var original = ScreenAnchor(cfg);
            var grid = LocationAnchor(cfg);
            var originalScreenAlignment = cfg.GetCVar(LuaMCVars.ScreenAnnouncementAlignment);
            var originalLocationAlignment = cfg.GetCVar(LuaMCVars.LocationAnnouncementAlignment);
            var enabled = cfg.GetCVar(LuaMCVars.ScreenAnnouncementsEnabled);
            try
            {
                var alignmentNames = new[] { LuaMCVars.ScreenAnnouncementAlignment.Name, LuaMCVars.LocationAnnouncementAlignment.Name };
                using (var stored = new MemoryStream())
                {
                    cfg.SetCVar(LuaMCVars.ScreenAnnouncementAlignment, 0);
                    cfg.SetCVar(LuaMCVars.LocationAnnouncementAlignment, 2);
                    cfg.SaveToTomlStream(stored, alignmentNames);
                    cfg.SetCVar(LuaMCVars.ScreenAnnouncementAlignment, 1);
                    cfg.SetCVar(LuaMCVars.LocationAnnouncementAlignment, 1);
                    stored.Position = 0;
                    cfg.LoadFromTomlStream(stored);
                    Assert.That(cfg.GetCVar(LuaMCVars.ScreenAnnouncementAlignment), Is.EqualTo(0));
                    Assert.That(cfg.GetCVar(LuaMCVars.LocationAnnouncementAlignment), Is.EqualTo(2));
                    foreach (var name in alignmentNames)
                        Assert.That(cfg.GetCVarFlags(name).HasFlag(CVar.ARCHIVE | CVar.CLIENTONLY), Is.True);
                }
                cfg.SetCVar(LuaMCVars.ScreenAnnouncementAlignment, 1);
                cfg.SetCVar(LuaMCVars.LocationAnnouncementAlignment, 1);
                // Keep the two examples separate, so the test specifically exercises transparent
                // fullscreen wrappers rather than relying on a forced ControlFocused assignment.
                cfg.SetCVar(LuaMCVars.LocationAnnouncementX, 0.25f);
                cfg.SetCVar(LuaMCVars.LocationAnnouncementY, 0.10f);
                cfg.SetCVar(LuaMCVars.ScreenAnnouncementX, 0.65f);
                cfg.SetCVar(LuaMCVars.ScreenAnnouncementY, 0.40f);
                cfg.SetCVar(LuaMCVars.ScreenAnnouncementsEnabled, false);
                foreach (var save in new[] { false, true })
                {
                    var initialScreen = ScreenAnchor(cfg);
                    var initialLocation = LocationAnchor(cfg);
                    var initialScreenAlignment = cfg.GetCVar(LuaMCVars.ScreenAnnouncementAlignment);
                    var initialLocationAlignment = cfg.GetCVar(LuaMCVars.LocationAnnouncementAlignment);
                    var editor = new HudLayoutEditor();
                    try
                    {
                        editor.OpenFullScreen();
                        var screen = Descendants(editor).OfType<ScreenAnnouncementPreview>().Single();
                        var location = Descendants(editor).Single(c => c.Name == "AnnouncementPreview");
                        var card = screen.Children.OfType<ScreenAnnouncementCard>().Single();
                        Assert.That(screen.VisibleInTree && location.VisibleInTree, Is.True);
                        Assert.That(Descendants(editor).OfType<OptionButton>().Select(c => c.Name),
                            Is.EquivalentTo(new[] { "HudLayoutSelectedElement", "HudLayoutLocationAlignment", "HudLayoutScreenAlignment" }),
                            "Selection and independent alignment controls keep both previews visible.");
                        Arrange(editor, ui, screen);
                        var body = Descendants(card).OfType<RichTextLabel>().Single(c => c.Name == "AnnouncementBody");
                        var message = body.GetFormattedMessage()!.ToString();
                        Assert.That(message, Is.EqualTo(Loc.GetString("screen-announcement-preview-body-system")));
                        Assert.That(message.Length, Is.GreaterThan(300));
                        for (var i = 0; i < 20; i++)
                            Tick(screen);
                        Assert.That(body.GetFormattedMessage()!.ToString(), Is.EqualTo(message), "The sample is permanent, fully revealed and stable.");

                        var oldScreenPosition = card.Position;
                        var oldLocationBounds = LocationBounds(location);
                        SelectAlignment(editor, "HudLayoutScreenAlignment", HudHorizontalAlignment.Left);
                        SelectAlignment(editor, "HudLayoutLocationAlignment", HudHorizontalAlignment.Right);
                        Arrange(editor, ui, screen);
                        Assert.That(card.Position.X, Is.EqualTo(oldScreenPosition.X).Within(0.01));
                        Assert.That(LocationBounds(location).Left, Is.EqualTo(oldLocationBounds.Left).Within(0.01));
                        Assert.That(screen.Alignment, Is.EqualTo(HudHorizontalAlignment.Left));
                        Assert.That(cfg.GetCVar(LuaMCVars.ScreenAnnouncementAlignment), Is.EqualTo(initialScreenAlignment));
                        Assert.That(cfg.GetCVar(LuaMCVars.LocationAnnouncementAlignment), Is.EqualTo(initialLocationAlignment));

                        var cardPosition = (Vector2) card.GlobalPixelPosition;
                        // The headless window is narrow: move inward from the right/bottom clamp.
                        var delta = new Vector2(-25, -20);
                        Drag(ui, screen, cardPosition + new Vector2(10 * screen.UIScale), delta);
                        Arrange(editor, ui, screen);
                        Assert.That(card.GlobalPixelPosition.X, Is.EqualTo(Math.Max(0, cardPosition.X + delta.X)).Within(1),
                            "The narrow test window may clamp the long example at the left edge.");
                        Assert.That(card.GlobalPixelPosition.Y, Is.EqualTo(cardPosition.Y + delta.Y).Within(1));
                        var chosenScreen = screen.Anchor;

                        var before = LocationBounds(location);
                        Drag(ui, location, location.GlobalPixelPosition + before.TopLeft + new Vector2(10), new Vector2(35, 25));
                        var after = LocationBounds(location);
                        Assert.That(after.Left, Is.EqualTo(before.Left + 35).Within(1));
                        Assert.That(after.Top, Is.EqualTo(before.Top + 25).Within(1));
                        var chosenLocation = LocationPreviewAnchor(location);
                        Assert.That(screen.Anchor, Is.EqualTo(chosenScreen), "Dragging the location must leave the other HUD where it was.");
                        Assert.That(ScreenAnchor(cfg), Is.EqualTo(initialScreen));
                        Assert.That(LocationAnchor(cfg), Is.EqualTo(initialLocation), "Dragging remains a preview until Save.");

                        var action = save ? "hud-layout-save" : "hud-layout-cancel";
                        Click(ui, Descendants(editor).OfType<Button>().Single(c => c.Text == Loc.GetString(action)));
                        Assert.That(ScreenAnchor(cfg), Is.EqualTo(save ? chosenScreen : initialScreen));
                        Assert.That(LocationAnchor(cfg), Is.EqualTo(save ? chosenLocation : initialLocation));
                        Assert.That(cfg.GetCVar(LuaMCVars.ScreenAnnouncementAlignment), Is.EqualTo(save ? 0 : initialScreenAlignment));
                        Assert.That(cfg.GetCVar(LuaMCVars.LocationAnnouncementAlignment), Is.EqualTo(save ? 2 : initialLocationAlignment));
                        Assert.That(ui.GetUIController<ScreenAnnouncementUIController>().EditingLayout, Is.False);
                    }
                    finally
                    {
                        editor.Close();
                    }
                }

                var beforeResetScreen = ScreenAnchor(cfg);
                var beforeResetLocation = LocationAnchor(cfg);
                var resetEditor = new HudLayoutEditor();
                try
                {
                    resetEditor.OpenFullScreen();
                    var screen = Descendants(resetEditor).OfType<ScreenAnnouncementPreview>().Single();
                    var location = Descendants(resetEditor).Single(c => c.Name == "AnnouncementPreview");
                    Arrange(resetEditor, ui, screen);
                    Click(ui, Descendants(resetEditor).OfType<Button>().Single(c => c.Text == Loc.GetString("hud-layout-reset")));
                    Assert.That(screen.Alignment, Is.EqualTo(HudHorizontalAlignment.Center));
                    Assert.That((HudHorizontalAlignment) location.GetType().GetField("Alignment")!.GetValue(location)!, Is.EqualTo(HudHorizontalAlignment.Center));
                    Assert.That(screen.Anchor, Is.EqualTo(HudLayout.DefaultScreenPosition));
                    Assert.That(LocationPreviewAnchor(location), Is.EqualTo(HudLayout.DefaultPosition));
                    Assert.That(Descendants(resetEditor).OfType<CheckBox>().Single().Pressed, Is.True);
                    Assert.That(ScreenAnchor(cfg), Is.EqualTo(beforeResetScreen));
                    Assert.That(LocationAnchor(cfg), Is.EqualTo(beforeResetLocation));
                    Assert.That(cfg.GetCVar(LuaMCVars.ScreenAnnouncementAlignment), Is.EqualTo(0));
                    Assert.That(cfg.GetCVar(LuaMCVars.LocationAnnouncementAlignment), Is.EqualTo(2));
                    Click(ui, Descendants(resetEditor).OfType<Button>().Single(c => c.Text == Loc.GetString("hud-layout-save")));
                    Assert.That(cfg.GetCVar(LuaMCVars.ScreenAnnouncementAlignment), Is.EqualTo(1));
                    Assert.That(cfg.GetCVar(LuaMCVars.LocationAnnouncementAlignment), Is.EqualTo(1));
                    Assert.That(ScreenAnchor(cfg), Is.EqualTo(HudLayout.DefaultScreenPosition));
                    Assert.That(LocationAnchor(cfg), Is.EqualTo(HudLayout.DefaultPosition));
                }
                finally
                {
                    resetEditor.Close();
                }
            }
            finally
            {
                cfg.SetCVar(LuaMCVars.ScreenAnnouncementX, original.X);
                cfg.SetCVar(LuaMCVars.ScreenAnnouncementY, original.Y);
                cfg.SetCVar(LuaMCVars.LocationAnnouncementX, grid.X);
                cfg.SetCVar(LuaMCVars.LocationAnnouncementY, grid.Y);
                cfg.SetCVar(LuaMCVars.ScreenAnnouncementAlignment, originalScreenAlignment);
                cfg.SetCVar(LuaMCVars.LocationAnnouncementAlignment, originalLocationAlignment);
                cfg.SetCVar(LuaMCVars.ScreenAnnouncementsEnabled, enabled);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task MouseWheelScalesOnlyHoveredHudAndCancelPreservesStoredScale()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        await pair.Client.WaitAssertion(() =>
        {
            var ui = pair.Client.ResolveDependency<IUserInterfaceManager>();
            var cfg = pair.Client.ResolveDependency<IConfigurationManager>();
            var oldScreen = cfg.GetCVar(LuaMCVars.ScreenAnnouncementScale);
            var oldLocation = cfg.GetCVar(LuaMCVars.LocationAnnouncementScale);
            var oldAnchor = ScreenAnchor(cfg);
            var oldGrid = LocationAnchor(cfg);
            cfg.SetCVar(LuaMCVars.ScreenAnnouncementScale, 1f);
            cfg.SetCVar(LuaMCVars.LocationAnnouncementScale, 1f);
            cfg.SetCVar(LuaMCVars.LocationAnnouncementX, 0.15f);
            cfg.SetCVar(LuaMCVars.LocationAnnouncementY, 0.05f);
            cfg.SetCVar(LuaMCVars.ScreenAnnouncementX, 0.7f);
            cfg.SetCVar(LuaMCVars.ScreenAnnouncementY, 0.3f);
            using var editor = new HudLayoutEditor();
            try
            {
                editor.OpenFullScreen();
                var screen = Descendants(editor).OfType<ScreenAnnouncementPreview>().Single();
                var location = Descendants(editor).Single(c => c.Name == "AnnouncementPreview");
                Arrange(editor, ui, screen);
                var card = Descendants(screen).OfType<ScreenAnnouncementCard>().Single();
                void Wheel(Vector2 point, float delta) => ui.GetType().GetMethod("MouseWheel")!.Invoke(ui,
                    new object[] { new MouseWheelEventArgs(new Vector2(0, delta), new ScreenCoordinates(point, WindowId.Main)) });
                Wheel(card.GlobalPixelPosition + new Vector2(12), -1);
                Assert.That(screen.Scale, Is.EqualTo(0.95f).Within(0.001));
                Assert.That((float) location.GetType().GetField("Scale")!.GetValue(location)!, Is.EqualTo(1));
                for (var i = 0; i < 10; i++) Wheel(card.GlobalPixelPosition + new Vector2(12), -5);
                Assert.That(screen.Scale, Is.EqualTo(HudLayout.MinimumScale));
                Arrange(editor, ui, screen);
                Assert.That(card.PresentationScale, Is.EqualTo(HudLayout.MinimumScale));
                Wheel(LocationBounds(location).Center, 1);
                Assert.That((float) location.GetType().GetField("Scale")!.GetValue(location)!, Is.EqualTo(1.05f).Within(0.001));
                Assert.That(cfg.GetCVar(LuaMCVars.ScreenAnnouncementScale), Is.EqualTo(1));
                Assert.That(cfg.GetCVar(LuaMCVars.LocationAnnouncementScale), Is.EqualTo(1));
                Click(ui, Descendants(editor).OfType<Button>().Single(c => c.Text == Loc.GetString("hud-layout-cancel")));
                Assert.That(cfg.GetCVar(LuaMCVars.ScreenAnnouncementScale), Is.EqualTo(1));
                Assert.That(cfg.GetCVar(LuaMCVars.LocationAnnouncementScale), Is.EqualTo(1));
                using var stream = new MemoryStream();
                cfg.SetCVar(LuaMCVars.ScreenAnnouncementScale, 0.8f);
                cfg.SaveToTomlStream(stream, new[] { LuaMCVars.ScreenAnnouncementScale.Name });
                cfg.SetCVar(LuaMCVars.ScreenAnnouncementScale, 1f);
                stream.Position = 0;
                cfg.LoadFromTomlStream(stream);
                Assert.That(cfg.GetCVar(LuaMCVars.ScreenAnnouncementScale), Is.EqualTo(0.8f));
            }
            finally
            {
                editor.Close();
                cfg.SetCVar(LuaMCVars.ScreenAnnouncementScale, oldScreen);
                cfg.SetCVar(LuaMCVars.LocationAnnouncementScale, oldLocation);
                cfg.SetCVar(LuaMCVars.ScreenAnnouncementX, oldAnchor.X);
                cfg.SetCVar(LuaMCVars.ScreenAnnouncementY, oldAnchor.Y);
                cfg.SetCVar(LuaMCVars.LocationAnnouncementX, oldGrid.X);
                cfg.SetCVar(LuaMCVars.LocationAnnouncementY, oldGrid.Y);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SelectedHudCenterButtonsLeaveTheOtherElementInPlace()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        await pair.Client.WaitAssertion(() =>
        {
            var ui = pair.Client.ResolveDependency<IUserInterfaceManager>();
            using var editor = new HudLayoutEditor();
            try
            {
                editor.OpenFullScreen();
                var screen = Descendants(editor).OfType<ScreenAnnouncementPreview>().Single();
                var location = Descendants(editor).Single(c => c.Name == "AnnouncementPreview");
                Arrange(editor, ui, screen);
                var gridAnchor = LocationPreviewAnchor(location);
                Click(ui, Descendants(editor).OfType<Button>().Single(c => c.Name == "HudLayoutCenterX"));
                Click(ui, Descendants(editor).OfType<Button>().Single(c => c.Name == "HudLayoutCenterY"));
                Arrange(editor, ui, screen);
                Assert.That(screen.ElementBounds.Center.X, Is.EqualTo(editor.PixelWidth / 2f).Within(1));
                Assert.That(screen.ElementBounds.Center.Y, Is.EqualTo(editor.PixelHeight / 2f).Within(1));
                Assert.That(LocationPreviewAnchor(location), Is.EqualTo(gridAnchor));
                var options = Descendants(editor).OfType<OptionButton>().Single(c => c.Name == "HudLayoutSelectedElement");
                var handler = (Action<OptionButton.ItemSelectedEventArgs>) typeof(OptionButton).GetField("OnItemSelected", Private)!.GetValue(options)!;
                handler(new OptionButton.ItemSelectedEventArgs(0, options));
                Assert.That(Descendants(editor).Single(c => c.Name == "HudLayoutLocationAlignment").Parent!.Visible, Is.True);
                Assert.That(Descendants(editor).Single(c => c.Name == "HudLayoutScreenAlignment").Parent!.Visible, Is.False);
                var savedScreen = screen.Anchor;
                Click(ui, Descendants(editor).OfType<Button>().Single(c => c.Name == "HudLayoutCenterX"));
                Assert.That(screen.Anchor, Is.EqualTo(savedScreen));
                Assert.That(LocationBounds(location).Center.X, Is.EqualTo(editor.PixelWidth / 2f).Within(1));
            }
            finally { editor.Close(); }
        });
        await pair.CleanReturnAsync();
    }

    private static void SelectAlignment(Control editor, string name, HudHorizontalAlignment alignment)
    {
        var options = Descendants(editor).OfType<OptionButton>().Single(c => c.Name == name);
        var handler = (Action<OptionButton.ItemSelectedEventArgs>) typeof(OptionButton)
            .GetField("OnItemSelected", Private)!.GetValue(options)!;
        handler(new OptionButton.ItemSelectedEventArgs((int) alignment, options));
        Assert.That(options.SelectedId, Is.EqualTo((int) alignment));
    }

    private static void Drag(IUserInterfaceManager ui, Control expected, Vector2 start, Vector2 delta)
    {
        PointerDown(ui, expected, start);
        ui.GetType().GetMethod("MouseMove")!.Invoke(ui, new object[]
        {
            new MouseMoveEventArgs(delta, new ScreenCoordinates(start + delta, WindowId.Main))
        });
        PointerUp(ui, start + delta);
    }

    private static void Click(IUserInterfaceManager ui, Control button)
    {
        var point = button.GlobalPixelPosition + (Vector2) button.PixelSize / 2;
        PointerDown(ui, button, point);
        PointerUp(ui, point);
    }

    private static void PointerDown(IUserInterfaceManager ui, Control expected, Vector2 point)
    {
        var coordinates = new ScreenCoordinates(point, WindowId.Main);
        var focusArguments = new object[] { coordinates, null! };
        var accepted = ui.GetType().GetMethod("HandleCanFocusDown")!.Invoke(ui, focusArguments);
        Assert.That(accepted, Is.True);
        Assert.That(ui.ControlFocused, Is.SameAs(expected), "Actual engine hit-testing must select the visible HUD under the pointer.");
        ui.GetType().GetMethod("KeyBindDown")!.Invoke(ui, new object[]
        {
            new BoundKeyEventArgs(EngineKeyFunctions.UIClick, BoundKeyState.Down, coordinates, true)
        });
    }

    private static void PointerUp(IUserInterfaceManager ui, Vector2 point)
    {
        ui.GetType().GetMethod("KeyBindUp")!.Invoke(ui, new object[]
        {
            new BoundKeyEventArgs(EngineKeyFunctions.UIClick, BoundKeyState.Up, new ScreenCoordinates(point, WindowId.Main), true)
        });
        ui.GetType().GetMethod("HandleCanFocusUp")!.Invoke(ui, Array.Empty<object>());
    }

    private static Vector2 ScreenAnchor(IConfigurationManager cfg) => new(cfg.GetCVar(LuaMCVars.ScreenAnnouncementX), cfg.GetCVar(LuaMCVars.ScreenAnnouncementY));
    private static Vector2 LocationAnchor(IConfigurationManager cfg) => new(cfg.GetCVar(LuaMCVars.LocationAnnouncementX), cfg.GetCVar(LuaMCVars.LocationAnnouncementY));
    private static Vector2 LocationPreviewAnchor(Control control) => (Vector2) control.GetType().GetField("Anchor")!.GetValue(control)!;

    private static UIBox2 LocationBounds(Control control)
    {
        var renderer = (LocationAnnouncementRenderer) control.GetType().GetField("_renderer", Private)!.GetValue(control)!;
        var title = (string) control.GetType().GetField("_title", Private)!.GetValue(control)!;
        var description = (string) control.GetType().GetField("_description", Private)!.GetValue(control)!;
        return renderer.GetBounds(control.PixelSize, LocationPreviewAnchor(control), title, description,
            (HudHorizontalAlignment) control.GetType().GetField("Alignment")!.GetValue(control)!,
            (float) control.GetType().GetField("Scale")!.GetValue(control)!);
    }

    private static void Tick(Control control) => control.GetType().GetMethod("FrameUpdate", Private)!
        .Invoke(control, new object[] { new FrameEventArgs(0.016f) });

    private static void Arrange(Control editor, IUserInterfaceManager ui, Control preview)
    {
        for (var i = 0; i < 3; i++)
        {
            var size = ui.ModalRoot.Size;
            editor.Measure(size);
            editor.Arrange(UIBox2.FromDimensions(Vector2.Zero, size));
            Tick(preview);
        }
    }

    private static IEnumerable<Control> Descendants(Control control)
    {
        foreach (var child in control.Children)
        {
            yield return child;
            foreach (var nested in Descendants(child))
                yield return nested;
        }
    }
}
