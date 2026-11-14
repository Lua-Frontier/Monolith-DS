using System;
using System.Collections.Generic;
using Content.Client.Lobby.UI;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Screens;
using Content.Client.UserInterface.Systems.Chat;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;

namespace Content.Client._LuaM.Hud;

/// <summary>Displays native, cached HUD widgets without duplicating them or taking over gameplay.</summary>
public sealed class NativeHudPreviewSession : IDisposable
{
    private readonly IUserInterfaceManager _ui;
    private readonly IStateManager _states;
    private readonly Action _invalidate;
    private readonly List<Control> _hiddenWindows = new();
    private readonly PanelContainer? _container;
    private readonly UIScreen? _nativeScreen;
    private readonly Control? _viewport;
    private readonly bool _viewportWasVisible;
    private bool _disposed;

    public NativeHudPreviewSession(IUserInterfaceManager ui, IConfigurationManager cfg,
        IStateManager states, Action invalidate)
    {
        _ui = ui;
        _states = states;
        _invalidate = invalidate;
        var previous = ui.ActiveScreen;
        if (previous is not InGameScreen)
        {
            // Fetch the already constructed screen, then immediately restore the active menu.
            // The preview owns no game state and must not occupy the active screen slot.
            ui.UnloadScreen();
            if (cfg.GetCVar(CCVars.UILayout) == "Separated")
                ui.LoadScreen<SeparatedChatGameScreen>();
            else
                ui.LoadScreen<DefaultGameScreen>();
            _nativeScreen = ui.ActiveScreen!;
            ui.GetUIController<ChatUIController>().SetMainChat(false);
            ui.UnloadScreen();
            if (previous is LobbyGui)
                ui.LoadScreen<LobbyGui>();

            // No world/map is loaded for menu previews. Keep the real viewport wrapper for
            // layout, but never render its uninitialized or stale world viewport.
            _viewport = _nativeScreen.GetWidget<MainViewport>()?.Viewport;
            if (_viewport != null)
            {
                _viewportWasVisible = _viewport.Visible;
                _viewport.Visible = false;
            }
            _container = new PanelContainer
            {
                Name = "NativeHudPreview",
                PanelOverride = new StyleBoxFlat(Color.FromHex("#080D16")),
            };
            _container.AddChild(_nativeScreen);
        }

        foreach (var child in ui.WindowRoot.Children)
        {
            if (!child.Visible)
                continue;
            _hiddenWindows.Add(child);
            child.Visible = false;
        }
        if (_container != null)
        {
            ui.WindowRoot.AddChild(_container);
            LayoutContainer.SetAnchorPreset(_container, LayoutContainer.LayoutPreset.Wide);
        }
        ui.OnScreenChanged += OnScreenChanged;
        states.OnStateChanged += OnStateChanged;
    }

    private void OnScreenChanged((UIScreen? Old, UIScreen? New) change)
    {
        // This event precedes ScreenRoot.AddChild. Release a cached screen before gameplay
        // reparents that same instance, and cancel the editor without restoring the old state.
        if (change.New == _nativeScreen)
            _nativeScreen?.Orphan();
        _invalidate();
    }

    private void OnStateChanged(StateChangedEventArgs args) => _invalidate();

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _ui.OnScreenChanged -= OnScreenChanged;
        _states.OnStateChanged -= OnStateChanged;
        if (_viewport != null)
            _viewport.Visible = _viewportWasVisible;
        // The cached screen may already have been reclaimed by gameplay.
        if (_nativeScreen?.Parent == _container)
            _nativeScreen?.Orphan();
        _container?.Orphan();
        foreach (var window in _hiddenWindows)
        {
            if (!window.Disposed && window.Parent == _ui.WindowRoot)
                window.Visible = true;
        }
        _hiddenWindows.Clear();
    }
}
