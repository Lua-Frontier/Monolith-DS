/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

using System.Threading.Tasks;
using Content.Server.Parallax;
using Content.Server.Procedural;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Procedural;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._LuaM.Procedural;

public sealed partial class LuaMBiomeDungeonsSystem : EntitySystem
{
    [Dependency] private DungeonSystem _dungeon = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;

    private const int PlacementAttempts = 24;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LuaMBiomeDungeonsComponent, MapInitEvent>(OnMapInit, after: new[] { typeof(BiomeSystem) });
    }

    private void OnMapInit(Entity<LuaMBiomeDungeonsComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Generated || ent.Comp.Configs.Count == 0 || !TryComp<MapGridComponent>(ent, out var grid))
            return;

        ent.Comp.Generated = true;

        var seed = TryComp<BiomeComponent>(ent, out var biome) && biome.Seed != -1 ? biome.Seed : _random.Next();
        _ = Generate(ent, grid, seed);
    }

    private async Task Generate(Entity<LuaMBiomeDungeonsComponent> ent, MapGridComponent grid, int seed)
    {
        var random = new System.Random(seed);
        var comp = ent.Comp;
        var count = random.Next(comp.MinCount, Math.Max(comp.MinCount, comp.MaxCount) + 1);
        var spacingSq = comp.Spacing * comp.Spacing;
        var placed = new List<Vector2i>(count);

        for (var i = 0; i < count; i++)
        {
            if (!TryPickPosition(comp, random, placed, spacingSq, out var position))
                break;

            placed.Add(position);

            var configId = comp.Configs[random.Next(comp.Configs.Count)];
            if (!_proto.TryIndex(configId, out var config))
            {
                Log.Error($"Unknown dungeon config {configId} on {ToPrettyString(ent)}");
                continue;
            }

            await _dungeon.GenerateDungeonAsync(config, config.ID, ent, grid, position, seed + i + 1);

            if (TerminatingOrDeleted(ent))
                return;
        }
    }

    private static bool TryPickPosition(
        LuaMBiomeDungeonsComponent comp,
        System.Random random,
        List<Vector2i> placed,
        int spacingSq,
        out Vector2i position)
    {
        for (var attempt = 0; attempt < PlacementAttempts; attempt++)
        {
            var angle = random.NextDouble() * Math.Tau;
            var distance = random.Next(comp.MinDistance, Math.Max(comp.MinDistance, comp.MaxDistance) + 1);
            position = comp.Origin + new Vector2i((int) (Math.Cos(angle) * distance), (int) (Math.Sin(angle) * distance));

            var free = true;
            foreach (var other in placed)
            {
                if ((other - position).LengthSquared >= spacingSq)
                    continue;

                free = false;
                break;
            }

            if (free)
                return true;
        }

        position = default;
        return false;
    }
}
