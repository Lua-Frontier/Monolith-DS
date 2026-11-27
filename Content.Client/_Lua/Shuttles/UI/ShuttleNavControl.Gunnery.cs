using System.Linq;
using System.Numerics;
using Content.Shared._Mono.FireControl;
using Content.Shared.Physics;
using Robust.Client.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Client.Shuttles.UI;

/// <summary>
/// Aim lines for the weapons selected in the shuttle console.
/// </summary>
public partial class ShuttleNavControl
{
    private static readonly TimeSpan GunneryCursorInterval = TimeSpan.FromSeconds(0.1);

    private FireControllableEntry[]? _gunneryControllables;
    private HashSet<NetEntity> _gunnerySelected = new();
    private readonly Dictionary<NetEntity, Color> _gunneryBlipColors = new();
    private TimeSpan _nextGunneryCursor;
    private static readonly TimeSpan GunneryLosInterval = TimeSpan.FromSeconds(0.1);
    private readonly Dictionary<NetEntity, bool> _gunneryLos = new();
    private TimeSpan _nextGunneryLos;
    /// <summary>
    /// Whether the fire button is held over the radar.
    /// </summary>
    public bool GunneryMouseDown => _isMouseDown;

    /// <summary>
    /// Raised while the cursor moves over the radar without firing, so guided missiles can follow it.
    /// </summary>
    public event Action<EntityCoordinates>? OnGunneryCursorMove;

    private void UpdateGunneryCursor()
    {
        // While firing every shot already carries the cursor.
        if (_gunneryControllables == null || _coordinates == null || _isMouseDown || !_isMouseInside || OnGunneryCursorMove == null)
            return;

        var now = Timing.RealTime;
        if (now < _nextGunneryCursor)
            return;

        _nextGunneryCursor = now + GunneryCursorInterval;
        OnGunneryCursorMove(GetMouseEntityCoordinates(_lastMousePos));
    }

    public void SetGunneryControllables(FireControllableEntry[]? controllables)
    {
        _gunneryControllables = controllables;
        _gunneryLos.Clear();
    }

    public void SetGunnerySelection(HashSet<NetEntity> selected)
    {
        _gunnerySelected = selected;
    }

    private void DrawGunneryLines(DrawingHandleScreen handle, Matrix3x2 worldToView, MapId mapId)
    {
        if (_gunneryControllables == null || _gunnerySelected.Count == 0 || !_isMouseInside)
            return;

        Matrix3x2.Invert(worldToView, out var viewToWorld);
        var cursorViewPos = ScalePosition(InverseScalePosition(_lastMousePos));
        var cursorWorldPos = Vector2.Transform(cursorViewPos, viewToWorld);

        _gunneryBlipColors.Clear();
        foreach (var blip in _blips.GetCurrentBlips())
        {
            _gunneryBlipColors[blip.NetUid] = blip.Config.Color;
        }

        var now = Timing.RealTime;
        var recheck = now >= _nextGunneryLos;
        if (recheck)
            _nextGunneryLos = now + GunneryLosInterval;

        var physics = EntManager.System<SharedPhysicsSystem>();
        foreach (var controllable in _gunneryControllables)
        {
            if (!_gunnerySelected.Contains(controllable.NetEntity))
                continue;

            var worldPos = _transform.ToMapCoordinates(EntManager.GetCoordinates(controllable.Coordinates)).Position;
            var direction = cursorWorldPos - worldPos;
            if (direction.LengthSquared() <= 0.001f)
                continue;

            if (!controllable.IgnoresLos && (recheck || !_gunneryLos.ContainsKey(controllable.NetEntity)))
            {
                var ray = new CollisionRay(worldPos, direction.Normalized(), (int) CollisionGroup.Impassable);
                _gunneryLos[controllable.NetEntity] = !physics.IntersectRay(mapId, ray, direction.Length(), ignoredEnt: _coordinates?.EntityId).Any();
            }

            var clear = controllable.IgnoresLos || _gunneryLos.GetValueOrDefault(controllable.NetEntity);
            if (clear && _gunneryBlipColors.TryGetValue(controllable.NetEntity, out var color))
                handle.DrawLine(Vector2.Transform(worldPos, worldToView), cursorViewPos, color.WithAlpha(0.3f));
        }
    }
}
