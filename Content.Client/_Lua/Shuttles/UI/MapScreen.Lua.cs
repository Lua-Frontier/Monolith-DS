using System.Numerics;
using Content.Client._Lua.UserInterface;
using Content.Client.Stylesheets;
using Content.Shared._LuaM.Shuttles; // LuaM
using Content.Shared._NF.Shipyard.Components;
using Content.Shared.Shuttles.UI.MapObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Shuttles.UI;

/// <summary>
/// Compact map screen look: zoom slider, small fonts, sorted object list with icons.
/// </summary>
public sealed partial class MapScreen
{
    private Font _mapObjectFont = default!;
    private Font _mapCompactFont = default!;

    private readonly record struct MapObjectSortKey(int Group, string Name);

    private void InitializeLuaMap()
    {
        var cache = IoCManager.Resolve<IResourceCache>();
        _mapObjectFont = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 9);
        _mapCompactFont = new VectorFont(cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"), 10);

        MapZoomSlider.OnValueChanged += range => SetMapZoom(range.Value);
        MapRadar.WorldRangeChanged += _ => UpdateMapZoomDisplay();
        UpdateMapZoomDisplay();
        ApplyCompactMapStyle(RightDisplayMap);
    }

    private void SetMapZoom(float zoom)
    {
        MapRadar.SetRadarZoom(zoom);
        UpdateMapZoomDisplay();
    }

    private void UpdateMapZoomDisplay()
    {
        MapZoomSlider.SetValueWithoutEvent(MapRadar.RadarZoom);
    }

    private void ApplyCompactMapStyle(Control root)
    {
        foreach (var child in root.Children)
        {
            switch (child)
            {
                case Label label:
                    label.AddStyleClass(StyleNano.StyleClassLabelSmall);
                    break;
                case Button button:
                    button.Label.FontOverride = _mapCompactFont;
                    break;
                case Slider slider:
                    ApplyThinSliderStyle(slider);
                    break;
            }

            ApplyCompactMapStyle(child);
        }
    }

    private static void ApplyThinSliderStyle(Slider slider)
    {
        slider.BackgroundStyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#07090A"), // LuaM: #121512 > #07090A
            ContentMarginTopOverride = 2,
            ContentMarginBottomOverride = 2,
        };
        slider.FillStyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#4CAF6A"),
            ContentMarginTopOverride = 2,
            ContentMarginBottomOverride = 2,
        };
        slider.ForegroundStyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#1A1F22"), // LuaM: #2A302A > #1A1F22
            BorderColor = Color.FromHex("#3D484D"), // LuaM: #3E463E > #3D484D
            BorderThickness = new Thickness(1),
            ContentMarginTopOverride = 2,
            ContentMarginBottomOverride = 2,
        };
        slider.GrabberStyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#D7F5D7"),
            BorderColor = Color.FromHex("#75BEC6"),
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 1,
            ContentMarginRightOverride = 1,
            ContentMarginTopOverride = 5,
            ContentMarginBottomOverride = 5,
        };
    }

    private void StyleMapHeading(CollapsibleHeading heading)
    {
        heading.MinHeight = 28f;
        heading.Label.FontColorOverride = Color.FromHex("#C5CEDB");
        heading.Label.FontOverride = _mapObjectFont;
    }

    /// <summary>
    /// Builds one row of the sector object list: icon, name button and the track button for grids.
    /// </summary>
    private BoxContainer CreateMapObjectRow(IMapObject mapObj)
    {
        var gridButton = new Button()
        {
            Text = mapObj.Name,
            HorizontalExpand = true,
            MinHeight = 22f,
        };
        gridButton.AddStyleClass(HudThemeStyles.ListRow);
        gridButton.Label.ClipText = true;
        gridButton.Label.Align = Label.AlignMode.Left;
        gridButton.OnPressed += _ => OnMapObjectPress(mapObj);

        var iconSlot = new CenterContainer()
        {
            MinWidth = 16f,
            MaxWidth = 16f,
            MinHeight = 18f,
            VerticalAlignment = VAlignment.Center,
            HorizontalAlignment = HAlignment.Center,
            Children =
            {
                CreateMapObjectIcon(mapObj),
            },
        };

        var row = new BoxContainer()
        {
            SeparationOverride = 4,
            Children =
            {
                iconSlot,
                gridButton,
            },
        };

        if (mapObj is GridMapObject)
        {
            var trackButton = new Button()
            {
                ToolTip = Loc.GetString("shuttle-console-map-track"),
                MinWidth = 32,
                MaxWidth = 32,
                MinHeight = 18f,
            };
            trackButton.AddStyleClass(ConsoleStyles.Button); // LuaM: ButtonSquare > console button
            trackButton.AddChild(new MapTargetIconControl());
            trackButton.OnPressed += _ => OnMapObjectTrackPress(mapObj);
            row.AddChild(trackButton);
        }

        return row;
    }

    private MapObjectSortKey GetMapObjectSortKey(IMapObject mapObj)
    {
        // LuaM: the sector station and points of interest right under our own ship, then the rest
        var group = mapObj switch
        {
            GridMapObject grid when grid.Entity == _shuttleEntity => 0,
            GridMapObject grid when IsMaskedGrid(grid) => 4, // LuaM: unknown signatures go last
            GridMapObject grid when _entManager.HasComponent<SectorLandmarkComponent>(grid.Entity) => 1, // LuaM
            GridMapObject grid when IsStationMapObject(grid) => 2,
            GridMapObject => 3,
            ShuttleBeaconObject => 5,
            ShuttleExclusionObject => 6,
            _ => 7,
        };

        return new MapObjectSortKey(group, mapObj.Name);
    }

    private static int CompareMapObjectSortKeys(MapObjectSortKey x, MapObjectSortKey y)
    {
        var groupCompare = x.Group.CompareTo(y.Group);
        if (groupCompare != 0)
            return groupCompare;

        return string.Compare(x.Name, y.Name, StringComparison.CurrentCultureIgnoreCase);
    }

    private Control CreateMapObjectIcon(IMapObject mapObj)
    {
        if (mapObj is ShuttleBeaconObject)
            return new MapObjectIconControl(MapObjectIconShape.Diamond, Color.FromHex("#7DDBA3"), false);
        if (mapObj is ShuttleExclusionObject)
            return new MapObjectIconControl(MapObjectIconShape.Diamond, Color.FromHex("#E07070"), false);
        if (mapObj is GridMapObject grid)
        {
            if (IsMaskedGrid(grid)) // LuaM: a signature must not tell a ship from a station
                return new MapObjectIconControl(MapObjectIconShape.Hexagon, Color.FromHex("#8A96A8"), false);

            if (IsStationMapObject(grid))
                return new MapObjectIconControl(MapObjectIconShape.Hexagon, Color.FromHex("#5EC8E8"), true);

            return new MapObjectIconControl(MapObjectIconShape.Triangle, Color.FromHex("#F0C674"), true);
        }

        return new MapObjectIconControl(MapObjectIconShape.Hexagon, Color.FromHex("#8A96A8"), false);
    }

    /// <summary>
    /// Player shuttles carry a deed, anything else listed is a station or a point of interest.
    /// </summary>
    private bool IsStationMapObject(GridMapObject grid)
    {
        return grid.Entity != _shuttleEntity && !_entManager.HasComponent<ShuttleDeedComponent>(grid.Entity);
    }

    private enum MapObjectIconShape
    {
        Triangle,
        Hexagon,
        Diamond,
    }

    private sealed class MapObjectIconControl : Control
    {
        private readonly MapObjectIconShape _shape;
        private readonly Color _color;
        private readonly bool _filled;

        public MapObjectIconControl(MapObjectIconShape shape, Color color, bool filled)
        {
            _shape = shape;
            _color = color;
            _filled = filled;
            MinSize = new Vector2(12f, 12f);
            MaxSize = new Vector2(12f, 12f);
            HorizontalAlignment = HAlignment.Center;
            VerticalAlignment = VAlignment.Center;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);

            var center = new Vector2(PixelWidth * 0.5f, PixelHeight * 0.5f);
            var radius = MathF.Min(PixelWidth, PixelHeight) * 0.46f;
            var sides = _shape switch
            {
                MapObjectIconShape.Triangle => 3,
                MapObjectIconShape.Hexagon => 6,
                _ => 4,
            };
            const float angleOffset = -MathF.PI / 2f;
            var vertices = new Vector2[sides];

            for (var i = 0; i < sides; i++)
            {
                var angle = angleOffset + MathF.Tau * i / sides;
                vertices[i] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            }

            handle.DrawPrimitives(
                _filled ? DrawPrimitiveTopology.TriangleFan : DrawPrimitiveTopology.LineLoop,
                vertices,
                _color);
        }
    }

    private sealed class MapTargetIconControl : Control
    {
        public MapTargetIconControl()
        {
            MinSize = new Vector2(14f, 14f);
            MaxSize = new Vector2(14f, 14f);
            HorizontalAlignment = HAlignment.Center;
            VerticalAlignment = VAlignment.Center;
            MouseFilter = MouseFilterMode.Ignore;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);

            var center = new Vector2(PixelWidth * 0.5f, PixelHeight * 0.5f);
            var color = Color.FromHex("#C5CEDB");
            const float radius = 3.5f;
            const float outer = 6f;

            LunaDraw.Circle(handle, center, radius, color, false);
            LunaDraw.Circle(handle, center, 1f, color, true);
            LunaDraw.Line(handle, center + new Vector2(0f, -outer), center + new Vector2(0f, -radius - 1f), color);
            LunaDraw.Line(handle, center + new Vector2(0f, radius + 1f), center + new Vector2(0f, outer), color);
            LunaDraw.Line(handle, center + new Vector2(-outer, 0f), center + new Vector2(-radius - 1f, 0f), color);
            LunaDraw.Line(handle, center + new Vector2(radius + 1f, 0f), center + new Vector2(outer, 0f), color);
        }
    }
}
