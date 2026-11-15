using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Timing;
// LuaM-start: configurable announcement position shared with the HUD preview.
using Content.Client._LuaM.Hud;
using Content.Shared._LuaM.Announcements; // LuaM
using Content.Shared._LuaM.CCVar;
using Robust.Shared.Configuration;
using Robust.Client.UserInterface;
using Content.Client.UserInterface.Screens;
// LuaM-end

namespace Content.Client._Crescent.SpaceBiomes;

/// <summary>
/// this system handles the actual drawing of grid names, descriptions, and biome overlays & descriptions
/// </summary>
public sealed partial class SpaceBiomeTextOverlay : Control // LuaM: HUD layer avoids viewport clipping.
{
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConfigurationManager _cfg = default!; // LuaM
    private readonly LocationAnnouncementRenderer _renderer; // LuaM

    // LuaM: rendered as a full-window HUD control rather than a viewport overlay.
    // LuaM: fonts and text measurement belong to LocationAnnouncementRenderer.

    public string? Text;
    public int Index;
    public bool Reverse;
    // LuaM: position is read from the archived client layout settings.
    public TimeSpan CharInterval;
    private TimeSpan _nextUpd = TimeSpan.Zero;

    public string? TextDescription;
    public TimeSpan CharIntervalDescription;
    public int IndexDescription;
    public bool ReverseDescription;
    private TimeSpan _nextUpdDescription = TimeSpan.Zero;

    public SpaceBiomeTextOverlay()
    {
        IoCManager.InjectDependencies(this);
        _renderer = new LocationAnnouncementRenderer(_cache); // LuaM
    }

    public void Reset()
    {
        Text = null;
        Index = 0;
        Reverse = false;
        _nextUpd = TimeSpan.Zero;
    }
    public void ResetDescription()
    {
        TextDescription = null;
        IndexDescription = 0;
        ReverseDescription = false;
        _nextUpdDescription = TimeSpan.Zero;
    }

    protected override void Draw(DrawingHandleScreen handle) // LuaM
    {
        if (UserInterfaceManager.ActiveScreen is not InGameScreen) // LuaM
            return;
        if (Text == null || Text == string.Empty)
            return;

        DrawDescription(); // LuaM: retain the description animation.

        // LuaM-start: use full HUD pixels, independent of the clipped render viewport.
        // Recalculate for current window size/configuration even while an announcement is animating.
        var screen = (Vector2) PixelSize;
        _renderer.Draw(handle, screen,
            new Vector2(_cfg.GetCVar(LuaMCVars.LocationAnnouncementX), _cfg.GetCVar(LuaMCVars.LocationAnnouncementY)),
            Text, TextDescription ?? string.Empty, Math.Min(Index, Text.Length),
            Math.Min(IndexDescription, TextDescription?.Length ?? 0),
            (HudHorizontalAlignment) _cfg.GetCVar(LuaMCVars.LocationAnnouncementAlignment),
            _cfg.GetCVar(LuaMCVars.LocationAnnouncementScale));
        // LuaM-end

        if (_nextUpd > _timing.CurTime)
            return;

        if (!Reverse && Index == Text.Length)
        {
            Reverse = true;

            //delay before text is erased
            _nextUpd += TimeSpan.FromSeconds(2);
            Index++;
        }

        if (Reverse && Index == 0)
        {
            Reset();
            return;
        }

        Index = Reverse ? Index - 1 : Index + 1;

        if (_nextUpd == TimeSpan.Zero)
            _nextUpd = _timing.CurTime;
        _nextUpd += CharInterval;
    }

    private void DrawDescription() // LuaM
    {
        if (TextDescription == null || TextDescription == string.Empty)
            return;


        // LuaM: description is drawn together with the title by the shared renderer.

        if (_nextUpdDescription > _timing.CurTime)
            return;

        if (!ReverseDescription && IndexDescription == TextDescription.Length)
        {
            ReverseDescription = true;

            //delay before description is erased
            _nextUpdDescription += TimeSpan.FromSeconds(2);
            IndexDescription++;
        }

        if (ReverseDescription && IndexDescription == 0)
        {
            ResetDescription();
            return;
        }

        IndexDescription = ReverseDescription ? IndexDescription - 1 : IndexDescription + 1;

        if (_nextUpdDescription == TimeSpan.Zero)
            _nextUpdDescription = _timing.CurTime;
        _nextUpdDescription += CharIntervalDescription;
    }

    // LuaM: legacy fixed-coordinate calculations replaced by HudLayout.
}
