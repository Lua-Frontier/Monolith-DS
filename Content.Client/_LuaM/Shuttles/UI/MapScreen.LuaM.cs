using System.Numerics;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.UI.MapObjects;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Map;

namespace Content.Client.Shuttles.UI;

/// <summary>
/// Map marker shared with the radar marker on NAV, object search, and scanning the sector without a button.
/// </summary>
public sealed partial class MapScreen
{
    private static readonly TimeSpan MapAutoRefreshInterval = TimeSpan.FromSeconds(30);

    private TimeSpan _nextMapAutoRefresh;
    private bool _mapTargetModified;
    private string _mapSearch = string.Empty;
    private HashSet<MapId>? _preSearchExpanded;

    public event Action<NetEntity?, Vector2>? OnSetTargetCoordinates;
    public event Action<NetEntity?, bool>? OnSetHideTarget;
    public event Action? OnClearTarget;

    private void InitializeMapTarget()
    {
        MapTargetSet.AddChild(new MapTargetIcon());
        MapTargetX.OnTextChanged += _ => _mapTargetModified = true;
        MapTargetY.OnTextChanged += _ => _mapTargetModified = true;
        MapTargetX.OnTextEntered += _ => SetMapTarget();
        MapTargetY.OnTextEntered += _ => SetMapTarget();
        MapTargetSet.OnPressed += _ => SetMapTarget();
        MapTargetHide.OnPressed += _ => OnSetHideTarget?.Invoke(GetShuttleNetEntity(), MapTargetHide.Pressed);
        MapTargetAutopilot.OnPressed += _ => AutopilotToMapTarget();
        MapTargetClear.OnPressed += _ => ClearMapTarget();
    }

    private void InitializeMapSearch()
    {
        MapObjectSearch.OnTextChanged += args => SetMapSearch(args.Text);
    }

    private void SetMapSearch(string text)
    {
        var search = text.Trim();
        if (search == _mapSearch)
            return;

        // Remember which sectors were open, the search opens every sector with a match.
        if (_mapSearch.Length == 0)
            _preSearchExpanded = GetExpandedHeadings();

        _mapSearch = search;
        MapRadar.SetMapSearch(search);
        ApplyMapSearch();

        if (search.Length == 0 && _preSearchExpanded != null)
        {
            SetExpandedHeadings(_preSearchExpanded);
            _preSearchExpanded = null;
        }
    }

    /// <summary>
    /// Hides list rows that don't match the search and sectors without matches.
    /// </summary>
    private void ApplyMapSearch()
    {
        var searching = _mapSearch.Length > 0;

        foreach (var contents in _mapHeadings.Values)
        {
            var anyVisible = false;
            foreach (var row in contents.Children)
            {
                var visible = !searching
                              || _mapObjectControls.TryGetValue(row, out var key)
                              && key.Name.Contains(_mapSearch, StringComparison.CurrentCultureIgnoreCase);
                row.Visible = visible;
                anyVisible |= visible;
            }

            if (contents.Parent?.Parent is not Collapsible collapsible)
                continue;

            collapsible.Visible = !searching || anyVisible;
            if (searching && anyVisible)
                collapsible.BodyVisible = true;
        }
    }

    /// <summary>
    /// A grid with its IFF label turned off: the list only shows its signature, nothing else about it may leak.
    /// </summary>
    private bool IsMaskedGrid(GridMapObject grid)
    {
        return grid.Entity != _shuttleEntity
               && _entManager.TryGetComponent(grid.Entity, out IFFComponent? iff)
               && (iff.Flags & IFFFlags.HideLabel) != 0x0;
    }

    private HashSet<MapId> GetExpandedHeadings()
    {
        var expanded = new HashSet<MapId>();
        foreach (var (mapId, contents) in _mapHeadings)
        {
            if (contents.Parent?.Parent is Collapsible { BodyVisible: true })
                expanded.Add(mapId);
        }

        return expanded;
    }

    private void SetExpandedHeadings(HashSet<MapId> expanded)
    {
        foreach (var (mapId, contents) in _mapHeadings)
        {
            if (contents.Parent?.Parent is Collapsible collapsible)
                collapsible.BodyVisible = expanded.Contains(mapId);
        }
    }

    /// <summary>
    /// Shows the console marker on the map and in the marker fields, unless the fields are being edited.
    /// </summary>
    public void UpdateMapTarget(NavInterfaceState state)
    {
        MapRadar.SetMapTarget(state.Target, state.TargetEntity, state.HideTarget);
        MapTargetHide.Pressed = state.HideTarget;

        if (_mapTargetModified)
            return;

        var target = state.Target ?? Vector2.Zero;
        MapTargetX.Text = target.X.ToString("F1");
        MapTargetY.Text = target.Y.ToString("F1");
    }

    private Vector2 SetMapTarget()
    {
        if (!float.TryParse(MapTargetX.Text, out var x))
            x = 0f;

        if (!float.TryParse(MapTargetY.Text, out var y))
            y = 0f;

        var target = new Vector2(x, y);
        MapRadar.SetMapTarget(target, NetEntity.Invalid, false);
        MapTargetHide.Pressed = false;
        _mapTargetModified = false;

        OnSetTargetCoordinates?.Invoke(GetShuttleNetEntity(), target);
        return target;
    }

    private void AutopilotToMapTarget()
    {
        // Sets the marker from the fields first, so the pilot sees where the shuttle goes.
        var target = SetMapTarget();

        if (!ShuttleMarkerAutopilot.TryGetDestination(_entManager, _shuttleEntity, target, out var destination, out var angle))
            return;

        RequestAutopilot?.Invoke(destination, angle);
    }

    private void ClearMapTarget()
    {
        MapRadar.SetMapTarget(null, null, false);
        MapTargetHide.Pressed = false;
        MapTargetX.Text = 0.0f.ToString("F1");
        MapTargetY.Text = 0.0f.ToString("F1");
        _mapTargetModified = false;

        OnClearTarget?.Invoke();
    }

    private NetEntity? GetShuttleNetEntity()
    {
        _entManager.TryGetNetEntity(_shuttleEntity, out var shuttle);
        return shuttle;
    }

    /// <summary>
    /// Rescans on the next frame the map is visible.
    /// </summary>
    private void RequestMapRefresh()
    {
        _nextMapAutoRefresh = TimeSpan.Zero;
    }

    private void UpdateMapAutoRefresh(TimeSpan curTime)
    {
        if (IsFTLBlocked() || _shuttleEntity == null || curTime < _nextMapAutoRefresh)
            return;

        _nextMapAutoRefresh = curTime + MapAutoRefreshInterval;
        RefreshMapObjects();
    }

    /// <summary>
    /// Rebuilds the sector object list silently, keeping the sector headings the pilot opened.
    /// </summary>
    private void RefreshMapObjects()
    {
        var hadHeadings = _mapHeadings.Count > 0;
        var expanded = GetExpandedHeadings();

        RebuildMapObjects();

        foreach (var (mapId, mapObj) in _pendingMapObjects)
        {
            AddMapObject(mapId, mapObj);
        }
        _pendingMapObjects.Clear();

        if (hadHeadings)
            SetExpandedHeadings(expanded);

        ApplyMapSearch();
    }
}
