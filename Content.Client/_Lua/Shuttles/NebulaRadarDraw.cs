using System.Numerics;
using Content.Shared._Lua.AmbientSpaceEffects;
using Content.Shared._Lua.CCVar;
using Content.Shared._Lua.SpaceHazards;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._Lua.Shuttles;

/// <summary>
/// Draws nebulae on the shuttle radar and the shuttle map: the outline of the dense part
/// and, for the dangerous ones, the icons of their weather.
/// </summary>
public static class NebulaRadarDraw
{
    private const float HarmlessFillAlpha = 0.05f;
    private const float WeatherFillAlpha = 0.11f;
    private const float OutlineAlpha = 0.85f;

    // Center, the outline, and the first outline point again to close it.
    private static Vector2[] _scratch = new Vector2[AmbientSpaceNebulaNoise.NavContourSegments + 2];
    private static readonly List<Texture> Icons = new();

    /// <param name="worldToScreen">From map coordinates to pixels of the control.</param>
    /// <param name="worldView">Part of the map that is on screen, nebulae outside of it are skipped.</param>
    /// <param name="iconSize">Size of a weather icon in pixels.</param>
    public static void Draw(
        DrawingHandleScreen handle,
        IEntityManager entManager,
        MapId mapId,
        Matrix3x2 worldToScreen,
        Box2 worldView,
        float iconSize)
    {
        if (mapId == MapId.Nullspace)
            return;

        // Harmless clouds are decoration and follow the graphics setting, dangerous ones are always shown.
        var drawHarmless = IoCManager.Resolve<IConfigurationManager>().GetCVar(LuaCVars.AmbientSpaceEffectsQuality) > 0;
        var transform = entManager.System<SharedTransformSystem>();

        var query = entManager.AllEntityQueryEnumerator<AmbientSpaceFieldComponent, TransformComponent>();
        while (query.MoveNext(out _, out var field, out var xform))
        {
            if (xform.MapID != mapId || field.Seed == 0 || !field.HasWeather && !drawHarmless)
                continue;

            var center = transform.GetWorldPosition(xform);
            var radius = MathF.Max(field.Radius, 1f);
            if (!worldView.Intersects(Box2.CenteredAround(center, new Vector2(radius * 2f, radius * 2f))))
                continue;

            var color = AmbientSpacePalette.ResolveFieldColor(field);
            DrawContour(handle, NebulaVeilHelpers.GetMidContour(field.Seed, radius, field.Density), center, worldToScreen, color, field.HasWeather);

            if (field.HasWeather && worldView.Contains(center))
                DrawWeatherIcons(handle, field, Vector2.Transform(center, worldToScreen), iconSize);
        }
    }

    private static void DrawContour(
        DrawingHandleScreen handle,
        Vector2[] contour,
        Vector2 center,
        Matrix3x2 worldToScreen,
        Color color,
        bool hasWeather)
    {
        if (contour.Length < 3)
            return;

        var count = contour.Length + 2;
        if (_scratch.Length < count)
            _scratch = new Vector2[count];

        _scratch[0] = Vector2.Transform(center, worldToScreen);
        for (var i = 0; i < contour.Length; i++)
        {
            _scratch[i + 1] = Vector2.Transform(center + contour[i], worldToScreen);
        }

        _scratch[count - 1] = _scratch[1];

        var fill = color.WithAlpha(hasWeather ? WeatherFillAlpha : HarmlessFillAlpha);
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, _scratch.AsSpan(0, count), fill);
        handle.DrawPrimitives(DrawPrimitiveTopology.LineStrip, _scratch.AsSpan(1, count - 1), color.WithAlpha(OutlineAlpha));
    }

    private static void DrawWeatherIcons(
        DrawingHandleScreen handle,
        AmbientSpaceFieldComponent field,
        Vector2 screenPos,
        float iconSize)
    {
        var prototypes = IoCManager.Resolve<IPrototypeManager>();
        var cache = IoCManager.Resolve<IResourceCache>();

        Icons.Clear();
        foreach (var weatherId in field.Weathers)
        {
            if (prototypes.TryIndex(weatherId, out var weather) &&
                weather.RadarIcon is { } path &&
                cache.TryGetResource<TextureResource>(path, out var resource) &&
                !Icons.Contains(resource.Texture))
            {
                Icons.Add(resource.Texture);
            }
        }

        // A row of icons centered on the nebula.
        var half = new Vector2(iconSize, iconSize) * 0.5f;
        var step = iconSize * 1.12f;
        var x = screenPos.X - step * (Icons.Count - 1) * 0.5f;
        foreach (var icon in Icons)
        {
            var iconCenter = new Vector2(x, screenPos.Y);
            handle.DrawTextureRect(icon, new UIBox2(iconCenter - half, iconCenter + half));
            x += step;
        }
    }
}
