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

public static class NebulaRadarDraw
{
    private const float OutlineAlpha = 0.85f;
    private const int MaxCachedFills = 512;
    private const int FillVertexCount = AmbientSpaceNebulaNoise.NavContourSegments * 9;

    // -- Заливка от центра к краю (как у областей DeepLagoon) строится один раз на туманность, каждый кадр только переводится в пиксели
    private static readonly Dictionary<(Vector2[] Contour, bool Weather), DrawVertexUV2DColor[]> Fills = new();
    private static readonly DrawVertexUV2DColor[] FillScratch = new DrawVertexUV2DColor[FillVertexCount];
    private static Vector2[] _outline = new Vector2[AmbientSpaceNebulaNoise.NavContourSegments + 1];
    private static readonly List<Texture> Icons = new();

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

        var drawHarmless = IoCManager.Resolve<IConfigurationManager>().GetCVar(LuaCVars.AmbientSpaceEffectsQuality) > 0;
        var transform = entManager.System<SharedTransformSystem>();
        var modulate = Color.FromSrgb(handle.Modulate);

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
            var contour = NebulaVeilHelpers.GetMidContour(field.Seed, radius, field.Density);
            DrawFill(handle, GetFill(contour, field.Seed, color, field.HasWeather), center, worldToScreen, modulate);
            DrawOutline(handle, contour, center, worldToScreen, color);

            if (field.HasWeather && worldView.Contains(center))
                DrawWeatherIcons(handle, field, Vector2.Transform(center, worldToScreen), iconSize);
        }
    }

    private static DrawVertexUV2DColor[] GetFill(Vector2[] contour, int seed, Color color, bool hasWeather)
    {
        if (Fills.TryGetValue((contour, hasWeather), out var fill))
            return fill;

        if (Fills.Count >= MaxCachedFills)
            Fills.Clear();

        fill = BuildFill(contour, seed, color, hasWeather ? 1f : 0.75f);
        Fills[(contour, hasWeather)] = fill;
        return fill;
    }

    private static DrawVertexUV2DColor[] BuildFill(Vector2[] contour, int seed, Color color, float density)
    {
        var segments = contour.Length;
        var vertices = new DrawVertexUV2DColor[segments * 9];
        var random = new System.Random(seed);
        var middle = new DrawVertexUV2DColor[segments];
        for (var i = 0; i < segments; i++)
        {
            var position = contour[i] * (0.48f + (float) random.NextDouble() * 0.2f);
            var alpha = (0.07f + (float) random.NextDouble() * 0.07f) * density;
            middle[i] = new DrawVertexUV2DColor(position, Color.FromSrgb(color.WithAlpha(alpha)));
        }

        var core = new DrawVertexUV2DColor(Vector2.Zero, Color.FromSrgb(color.WithAlpha(0.16f * density)));
        var edgeColor = Color.FromSrgb(color.WithAlpha(0.018f * density));
        var index = 0;
        for (var i = 0; i < segments; i++)
        {
            var next = (i + 1) % segments;
            var outer = new DrawVertexUV2DColor(contour[i], edgeColor);
            var outerNext = new DrawVertexUV2DColor(contour[next], edgeColor);
            vertices[index++] = core;
            vertices[index++] = middle[i];
            vertices[index++] = middle[next];
            vertices[index++] = middle[i];
            vertices[index++] = outer;
            vertices[index++] = outerNext;
            vertices[index++] = middle[i];
            vertices[index++] = outerNext;
            vertices[index++] = middle[next];
        }

        return vertices;
    }

    private static void DrawFill(
        DrawingHandleScreen handle,
        DrawVertexUV2DColor[] fill,
        Vector2 center,
        Matrix3x2 worldToScreen,
        Color modulate)
    {
        var count = Math.Min(fill.Length, FillScratch.Length);
        for (var i = 0; i < count; i++)
        {
            var vertex = fill[i];
            vertex.Position = Vector2.Transform(center + vertex.Position, worldToScreen);
            vertex.Color *= modulate;
            FillScratch[i] = vertex;
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, Texture.White, FillScratch.AsSpan(0, count));
    }

    private static void DrawOutline(DrawingHandleScreen handle, Vector2[] contour, Vector2 center, Matrix3x2 worldToScreen, Color color)
    {
        if (contour.Length < 3)
            return;

        var count = contour.Length + 1;
        if (_outline.Length < count)
            _outline = new Vector2[count];

        for (var i = 0; i < contour.Length; i++)
        {
            _outline[i] = Vector2.Transform(center + contour[i], worldToScreen);
        }

        _outline[count - 1] = _outline[0];
        handle.DrawPrimitives(DrawPrimitiveTopology.LineStrip, _outline.AsSpan(0, count), color.WithAlpha(OutlineAlpha));
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
