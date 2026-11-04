using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Content.IntegrationTests;
using Content.IntegrationTests.Pair;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Robust.Client.GameObjects;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
// LuaM-start: isolated guidebook rendering; keep ordinary map rendering unchanged.
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Value;
// LuaM-end
using Robust.UnitTesting.Pool;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Content.MapRenderer.Painters
{
    public sealed class MapPainter
    {
        public static async IAsyncEnumerable<RenderedGridImage<Rgba32>> Paint(string map)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var testContext = new ExternalTestContext("Content.MapRenderer", Console.Out);
            await using var pair = await PoolManager.GetServerClient(new PoolSettings
            {
                DummyTicker = false,
                Connected = true,
                Fresh = true,
                // Seriously whoever made MapPainter use GameMapPrototype I wish you step on a lego one time.
                Map = map,
            }, testContext);
            pair.ServerLogHandler.FailureLevel = LogLevel.Fatal;
            pair.ClientLogHandler.FailureLevel = LogLevel.Fatal;

            await foreach (var image in RenderPair(stopwatch, pair))
                yield return image;
        }

        // Mono
        // LuaM-start: isolated guidebook rendering; keep ordinary map rendering unchanged.
        public static IAsyncEnumerable<RenderedGridImage<Rgba32>> PaintGrid(string path) => PaintGridInternal(path, false);

        public static IAsyncEnumerable<RenderedGridImage<Rgba32>> PaintGuidebookGrid(string path) => PaintGridInternal(path, true);

        private static async IAsyncEnumerable<RenderedGridImage<Rgba32>> PaintGridInternal(string path, bool guidebook)
        // LuaM-end
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var testContext = new ExternalTestContext("Content.MapRenderer", Console.Out);
            await using var pair = await PoolManager.GetServerClient(new PoolSettings
            {
                DummyTicker = false,
                Connected = true,
                Fresh = false,
            }, testContext);
            pair.ServerLogHandler.FailureLevel = LogLevel.Fatal;
            pair.ClientLogHandler.FailureLevel = LogLevel.Fatal;

            var server = pair.Server;
            var client = pair.Client;
            var sEntityManager = server.ResolveDependency<IServerEntityManager>();
            var mapLoader = sEntityManager.System<MapLoaderSystem>();
            var sMapManager = server.System<SharedMapSystem>();

            await server.WaitPost(() =>
            {
                var mapId = sEntityManager.System<GameTicker>().DefaultMap;

                foreach (var grid in sMapManager.GetAllGrids(mapId))
                    sEntityManager.QueueDeleteEntity(grid);
            });

            await pair.RunTicksSync(10);
            await Task.WhenAll(client.WaitIdleAsync(), server.WaitIdleAsync());

            await server.WaitPost(() =>
            {
                var mapId = sEntityManager.System<GameTicker>().DefaultMap;

                // LuaM-start: isolated guidebook rendering; keep ordinary map rendering unchanged.
                if (guidebook)
                {
                    if (!mapLoader.TryReadFile(new(path), out var saved)) throw new InvalidOperationException("Cannot read guidebook map " + path);
                    RepairSavedContainers(saved, server.ResolveDependency<IPrototypeManager>());
                    // Unlike TryLoadGrid, this also accepts station files containing several grids.
                    if (!mapLoader.TryLoadGeneric(saved, path, out _, new MapLoadOptions { MergeMap = mapId }))
                        throw new InvalidOperationException("Cannot load guidebook map " + path);
                    return;
                }

                // LuaM-end
                try
                {
                    mapLoader.TryLoadGrid(mapId, new(path), out _);
                }
                catch (Exception e) // we probably tried to load a map
                {
                    Logger.Info($"Failed to load as grid, rendering as map...");
                    sMapManager.DeleteMap(mapId);
                    var opts = new DeserializationOptions();
                    opts.InitializeMaps = true;
                    mapLoader.TryLoadMapWithId(mapId, new(path), out _, out _, opts);
                }
            });

            await foreach (var image in RenderPair(stopwatch, pair))
                yield return image;
        }
        // LuaM-start: isolated guidebook rendering; keep ordinary map rendering unchanged.

        // Some mapped containers contain `ent: invalid`, and some saved actions
        // lack their owner. Repair only the renderer's in-memory copy: neither
        // game loading nor source YAML is changed, and stored items stay hidden.
        private static void RepairSavedContainers(MappingDataNode saved, IPrototypeManager prototypes)
        {
            if (!saved.TryGet("entities", out SequenceDataNode? groups)) return;
            var entities = new Dictionary<string, (MappingDataNode Data, string Proto)>();
            foreach (var group in groups.Cast<MappingDataNode>())
            {
                var proto = group.Get<ValueDataNode>("proto").Value;
                foreach (var entity in group.Get<SequenceDataNode>("entities").Cast<MappingDataNode>())
                    entities[entity.Get<ValueDataNode>("uid").Value] = (entity, proto);
            }
            foreach (var (owner, (entity, _)) in entities)
            {
                if (!entity.TryGet("components", out SequenceDataNode? components)) continue;
                foreach (var component in components.Cast<MappingDataNode>().ToArray())
                {
                    if (component.Get<ValueDataNode>("type").Value != "ContainerContainer"
                        || !component.TryGet("containers", out MappingDataNode? containers)) continue;
                    foreach (var (_, node) in containers)
                        if (node is MappingDataNode slot && slot.TryGet("ent", out ValueDataNode? contents) && contents.Value == "invalid")
                            slot["ent"] = ValueDataNode.Null();
                    if (!containers.TryGet("actions", out MappingDataNode? actions)
                        || !actions.TryGet("ents", out SequenceDataNode? ids)) continue;
                    foreach (var id in ids.Cast<ValueDataNode>())
                    {
                        if (!entities.TryGetValue(id.Value, out var action)
                            || !prototypes.TryIndex<EntityPrototype>(action.Proto, out var prototype)) continue;
                        var type = prototype.Components.Keys.FirstOrDefault(k => k is "InstantAction" or "EntityTargetAction" or "WorldTargetAction" or "EntityWorldTargetAction");
                        if (type == null) continue;
                        if (!action.Data.TryGet("components", out SequenceDataNode? actionComponents))
                            action.Data["components"] = actionComponents = new SequenceDataNode();
                        var data = actionComponents.OfType<MappingDataNode>().FirstOrDefault(c => c.Get<ValueDataNode>("type").Value == type);
                        if (data == null)
                        {
                            data = new MappingDataNode { { "type", new ValueDataNode(type) } };
                            actionComponents.Add(data);
                        }
                        data["container"] = new ValueDataNode(owner);
                    }
                }
            }
        }
        // LuaM-end

        // Mono - separate into its own method
        private static async IAsyncEnumerable<RenderedGridImage<Rgba32>> RenderPair(Stopwatch stopwatch, TestPair pair)
        {
            var server = pair.Server;
            var client = pair.Client;

            Console.WriteLine($"Loaded client and server in {(int)stopwatch.Elapsed.TotalMilliseconds} ms");

            stopwatch.Restart();

            var cEntityManager = client.ResolveDependency<IClientEntityManager>();
            var cPlayerManager = client.ResolveDependency<Robust.Client.Player.IPlayerManager>();

            await client.WaitPost(() =>
            {
                if (cEntityManager.TryGetComponent(cPlayerManager.LocalEntity, out SpriteComponent? sprite))
                {
                    sprite.Visible = false;
                }
            });

            var sEntityManager = server.ResolveDependency<IServerEntityManager>();
            var sPlayerManager = server.ResolveDependency<IPlayerManager>();

            await pair.RunTicksSync(10);
            await Task.WhenAll(client.WaitIdleAsync(), server.WaitIdleAsync());

            var sMapManager = server.System<SharedMapSystem>();

            var tilePainter = new TilePainter(client, server);
            var entityPainter = new GridPainter(client, server);
            Entity<MapGridComponent>[] grids = null!;
            var xformQuery = sEntityManager.GetEntityQuery<TransformComponent>();
            var xformSystem = sEntityManager.System<SharedTransformSystem>();

            await server.WaitPost(() =>
            {
                var playerEntity = sPlayerManager.Sessions.Single().AttachedEntity;

                if (playerEntity.HasValue)
                {
                    sEntityManager.DeleteEntity(playerEntity.Value);
                }

                var mapId = sEntityManager.System<GameTicker>().DefaultMap;
                grids = sMapManager.GetAllGrids(mapId).ToArray();

                foreach (var (uid, _) in grids)
                {
                    var gridXform = xformQuery.GetComponent(uid);
                    xformSystem.SetWorldRotation(gridXform, Angle.Zero);
                }
            });

            await pair.RunTicksSync(10);
            await Task.WhenAll(client.WaitIdleAsync(), server.WaitIdleAsync());

            foreach (var (uid, grid) in grids)
            {
                // Skip empty grids
                if (grid.LocalAABB.IsEmpty())
                {
                    Console.WriteLine($"Warning: Grid {uid} was empty. Skipping image rendering.");
                    continue;
                }

                var tileXSize = grid.TileSize * TilePainter.TileImageSize;
                var tileYSize = grid.TileSize * TilePainter.TileImageSize;

                var bounds = grid.LocalAABB;

                var left = bounds.Left;
                var right = bounds.Right;
                var top = bounds.Top;
                var bottom = bounds.Bottom;

                var w = (int)Math.Ceiling(right - left) * tileXSize;
                var h = (int)Math.Ceiling(top - bottom) * tileYSize;

                var gridCanvas = new Image<Rgba32>(w, h);

                await server.WaitPost(() =>
                {
                    tilePainter.Run(gridCanvas, uid, grid);
                    entityPainter.Run(gridCanvas, uid, grid);

                    gridCanvas.Mutate(e => e.Flip(FlipMode.Vertical));
                });

                var renderedImage = new RenderedGridImage<Rgba32>(gridCanvas)
                {
                    GridUid = uid,
                    Offset = xformSystem.GetWorldPosition(uid),
                };

                yield return renderedImage;
            }

            // We don't care if it fails as we have already saved the images.
            try
            {
                await pair.CleanReturnAsync();
            }
            catch
            {
                // ignored
            }
        }
    }
}
