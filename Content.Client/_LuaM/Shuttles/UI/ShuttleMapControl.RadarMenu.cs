using System.Numerics;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client.Shuttles.UI;

/// <summary>
/// Alt + right click on a sector map object opens the same menu as on NAV.
/// </summary>
public sealed partial class ShuttleMapControl
{
    private const float MapMenuHitRadius = 18f;

    private readonly List<(EntityUid Grid, Vector2 Position)> _mapMenuTargets = new();

    public event Action<EntityUid>? OnMapMenuRequested;

    public bool IsMapMenuTarget(EntityUid grid)
    {
        foreach (var target in _mapMenuTargets)
        {
            if (target.Grid == grid)
                return true;
        }

        return false;
    }

    private bool TryRequestMapMenu(GUIBoundKeyEventArgs args)
    {
        if (FtlMode || args.Function != EngineKeyFunctions.UseSecondary || !_inputs.IsKeyDown(Keyboard.Key.Alt))
            return false;

        EntityUid? best = null;
        var bestDistance = MapMenuHitRadius * UIScale;
        foreach (var (grid, position) in _mapMenuTargets)
        {
            var distance = Vector2.Distance(position, args.RelativePixelPosition);
            if (distance > bestDistance)
                continue;

            best = grid;
            bestDistance = distance;
        }

        if (best != null)
            OnMapMenuRequested?.Invoke(best.Value);

        args.Handle();
        return true;
    }
}
