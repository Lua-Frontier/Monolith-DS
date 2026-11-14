// LuaWorld - This file is licensed under AGPLv3
// Copyright (c) 2026 LuaWorld Contributors
// See AGPLv3.txt for details.

using System.Numerics;

namespace Content.Shared._Lua.AmbientSpaceEffects;

// -- Должен совпадать с ambient_nebula.swsl: клиент запекает этот шум в текстуру, а сервер по нему же определяет зону погоды
public static class AmbientSpaceNebulaNoise
{
    public const int NavContourSegments = 24;
    public const int NavContourSteps = 12;
    public const float NoiseReferenceRadius = 500f;

    public const int Period = 8;

    public const int BakeSize = 1024;

    private const int Octaves = 5;
    private const int Channels = 2;
    private const float MidLayerBias = 2.3f;

    private static readonly float[][] Lattices = BuildLattices();

    private static float[][] BuildLattices()
    {
        var lattices = new float[Channels * Octaves][];
        for (var channel = 0; channel < Channels; channel++)
        {
            for (var octave = 0; octave < Octaves; octave++)
            {
                var size = Period << octave;
                var lattice = new float[size * size];
                var salt = (uint) (channel * Octaves + octave + 1) * 0xC2B2AE3Du;
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var h = (uint) x * 0x9E3779B1u ^ (uint) y * 0x85EBCA77u ^ salt;
                        h ^= h >> 15;
                        h *= 0x2C1B3C6Du;
                        h ^= h >> 12;
                        h *= 0x297A2D39u;
                        h ^= h >> 15;
                        lattice[y * size + x] = (h >> 8) * (1f / 16777216f);
                    }
                }

                lattices[channel * Octaves + octave] = lattice;
            }
        }

        return lattices;
    }

    public static float Fbm(Vector2 p, int channel = 0)
    {
        var value = 0f;
        var amplitude = 0.5f;
        for (var octave = 0; octave < Octaves; octave++)
        {
            var size = Period << octave;
            var mask = size - 1;
            var lattice = Lattices[channel * Octaves + octave];
            var x = p.X * (1 << octave);
            var y = p.Y * (1 << octave);
            var floorX = MathF.Floor(x);
            var floorY = MathF.Floor(y);
            var fx = x - floorX;
            var fy = y - floorY;
            var x0 = (int) floorX & mask;
            var y0 = (int) floorY & mask;
            var x1 = (x0 + 1) & mask;
            var y1 = (y0 + 1) & mask;
            var ux = fx * fx * (3f - 2f * fx);
            var uy = fy * fy * (3f - 2f * fy);
            var bottom = Lerp(lattice[y0 * size + x0], lattice[y0 * size + x1], ux);
            var top = Lerp(lattice[y1 * size + x0], lattice[y1 * size + x1], ux);
            value += amplitude * Lerp(bottom, top, uy);
            amplitude *= 0.5f;
        }

        return value;
    }

    public static void Bake(int channel, Span<byte> output)
    {
        const int size = BakeSize;
        var sum = new float[size * size];
        var cell0 = new int[size];
        var cell1 = new int[size];
        var blend = new float[size];
        var amplitude = 0.5f;

        for (var octave = 0; octave < Octaves; octave++)
        {
            var latticeSize = Period << octave;
            var mask = latticeSize - 1;
            var lattice = Lattices[channel * Octaves + octave];

            for (var i = 0; i < size; i++)
            {
                var coord = (i + 0.5f) * latticeSize / size;
                var floor = MathF.Floor(coord);
                var f = coord - floor;
                cell0[i] = (int) floor & mask;
                cell1[i] = (cell0[i] + 1) & mask;
                blend[i] = f * f * (3f - 2f * f);
            }

            for (var y = 0; y < size; y++)
            {
                var row0 = cell0[y] * latticeSize;
                var row1 = cell1[y] * latticeSize;
                var uy = blend[y];
                var offset = y * size;
                for (var x = 0; x < size; x++)
                {
                    var x0 = cell0[x];
                    var x1 = cell1[x];
                    var ux = blend[x];
                    var bottom = Lerp(lattice[row0 + x0], lattice[row0 + x1], ux);
                    var top = Lerp(lattice[row1 + x0], lattice[row1 + x1], ux);
                    sum[offset + x] += amplitude * Lerp(bottom, top, uy);
                }
            }

            amplitude *= 0.5f;
        }

        for (var i = 0; i < sum.Length; i++)
        {
            output[i] = (byte) (sum[i] * 255f + 0.5f);
        }
    }

    private static float WarpedFbm(Vector2 p)
    {
        var q = new Vector2(Fbm(p), Fbm(p, 1));
        var warped = p + 1.5f * q + new Vector2(1.7f, 9.2f);
        var r = new Vector2(Fbm(warped), Fbm(warped, 1));
        return Fbm(p + 2f * r);
    }

    public static float SamplePresence(Vector2 p, int seed, float density, float worldRadius)
    {
        var distance = p.Length();
        if (distance >= 1f)
            return 0f;

        var radial = new Vector2(p.X * 1.02f, p.Y * 0.95f).Length();

        var seedF = AmbientSpacePalette.ShaderSeedFromField(seed);
        var sizeScale = MathF.Max(worldRadius, 1f) / NoiseReferenceRadius;
        var basePos = p * sizeScale * new Vector2(2.7675f, 1.9475f);
        basePos += new Vector2(seedF * 0.13f + MidLayerBias, seedF * 0.07f);

        var detail = Fbm(basePos * 2.6f + new Vector2(3.1f, 5.7f));
        var ridges = Fbm(basePos * 0.9f + new Vector2(9f, 2f));
        var edgeNoise = Fbm(basePos * 5.8f + new Vector2(2f, 7f));
        var flameNoise = MathF.Abs(Fbm(basePos * 9.2f + new Vector2(ridges * 3f, detail * 2f)) - 0.5f) * 2f;
        var n = WarpedFbm(basePos);

        var ang = MathF.Max(
            MathF.Abs(basePos.X * 1.1f + ridges * 0.4f),
            MathF.Abs(basePos.Y * 1.1f + detail * 0.35f));
        var angFold = Fract(ang * 0.85f + n * 0.2f);
        var corner = Lerp(0.82f, 1.12f, Step(0.55f, angFold) * Step(angFold, 0.78f));

        var edgeWobble =
            (detail - 0.5f) * 0.42f +
            (angFold - 0.5f) * 0.18f +
            (ridges - 0.5f) * 0.32f +
            (edgeNoise - 0.5f) * 0.55f +
            (flameNoise - 0.5f) * 0.35f;
        var outer = 1f - Smoothstep(0.52f + edgeWobble, 0.98f + edgeWobble * 0.55f, radial);
        outer *= Step(0.28f, edgeNoise * 0.55f + flameNoise * 0.45f + outer * 0.25f);
        outer *= 1f - Smoothstep(0.9f, 1f, distance);

        var dens = Math.Clamp(density, 0.25f, 0.85f);
        var body = Smoothstep(0.34f - dens * 0.1f, 0.6f + dens * 0.05f, n);
        body *= Lerp(0.55f, 1f, Smoothstep(0.28f, 0.72f, ridges));
        body *= Lerp(0.7f, 1.08f, detail);
        body *= corner;
        body *= outer;
        body *= Lerp(0.35f, 1f, Step(0.4f, edgeNoise + flameNoise * 0.5f));
        body = Math.Clamp(body, 0f, 1f);

        const float layerBands = 5f;
        var stepped = Math.Clamp(MathF.Floor(body * layerBands), 0f, layerBands - 1f) / (layerBands - 1f);

        const float edgeBands = 4f;
        var edgeStepped = Math.Clamp(MathF.Floor(Math.Clamp(outer, 0f, 1f) * edgeBands), 0f, edgeBands - 1f) / (edgeBands - 1f);

        var notch = Step(0.68f, detail) * Step(0.35f, angFold) * (1f / layerBands);
        return MathF.Max(stepped - notch, 0f) * edgeStepped;
    }

    public static Vector2[] BuildMidLayerContour(float worldRadius, int seed, float density)
    {
        const float threshold = 0.12f;
        var points = new Vector2[NavContourSegments];
        var step = worldRadius / NavContourSteps;

        for (var i = 0; i < NavContourSegments; i++)
        {
            var angle = i * MathF.Tau / NavContourSegments;
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var edgeR = worldRadius * 0.35f;

            for (var r = worldRadius * 0.2f; r <= worldRadius; r += step)
            {
                if (SamplePresence(dir * (r / worldRadius), seed, density, worldRadius) > threshold)
                    edgeR = r;
            }

            points[i] = dir * edgeR;
        }

        return points;
    }

    private static float Fract(float x) => x - MathF.Floor(x);

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static float Smoothstep(float edge0, float edge1, float x)
    {
        var t = Math.Clamp((x - edge0) / (edge1 - edge0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float Step(float edge, float x) => x < edge ? 0f : 1f;
}
