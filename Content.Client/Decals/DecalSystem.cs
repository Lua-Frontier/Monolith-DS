using Content.Client.Decals.Overlays;
using Content.Shared.Decals;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.GameStates;
using Robust.Shared.Timing; // LuaM
using Robust.Shared.Utility;
using static Content.Shared.Decals.DecalGridComponent;

namespace Content.Client.Decals
{
    public sealed partial class DecalSystem : SharedDecalSystem
    {
        [Dependency] private IOverlayManager _overlayManager = default!;
        [Dependency] private SpriteSystem _sprites = default!;
        [Dependency] private IGameTiming _timing = default!; // LuaM

        private DecalOverlay? _overlay;

        private HashSet<uint> _removedUids = new();
        private readonly List<Vector2i> _removedChunks = new();

        // LuaM-start: decal chunks travel apart from PVS and can arrive before the grid itself, keep them until it does
        private static readonly TimeSpan PendingChunkLifetime = TimeSpan.FromSeconds(30);
        private readonly Dictionary<NetEntity, (Dictionary<Vector2i, DecalChunk> Chunks, TimeSpan Expires)> _pendingChunks = new();
        // LuaM-end

        public override void Initialize()
        {
            base.Initialize();

            _overlay = new DecalOverlay(_sprites, EntityManager, PrototypeManager);
            _overlayManager.AddOverlay(_overlay);

            SubscribeLocalEvent<DecalGridComponent, ComponentHandleState>(OnHandleState);
            SubscribeNetworkEvent<DecalChunkUpdateEvent>(OnChunkUpdate);
        }

        public void ToggleOverlay()
        {
            if (_overlay == null)
                return;

            if (_overlayManager.HasOverlay<DecalOverlay>())
            {
                _overlayManager.RemoveOverlay(_overlay);
            }
            else
            {
                _overlayManager.AddOverlay(_overlay);
            }
        }

        public override void Shutdown()
        {
            base.Shutdown();

            if (_overlay == null)
                return;

            _overlayManager.RemoveOverlay(_overlay);
        }

        protected override void OnDecalRemoved(EntityUid gridId, uint decalId, DecalGridComponent component, Vector2i indices, DecalChunk chunk)
        {
            base.OnDecalRemoved(gridId, decalId, component, indices, chunk);
            DebugTools.Assert(chunk.Decals.ContainsKey(decalId));
            chunk.Decals.Remove(decalId);
        }

        private void OnHandleState(EntityUid gridUid, DecalGridComponent gridComp, ref ComponentHandleState args)
        {
            // is this a delta or full state?
            _removedChunks.Clear();
            Dictionary<Vector2i, DecalChunk> modifiedChunks;

            switch (args.Current)
            {
                case DecalGridDeltaState delta:
                {
                    modifiedChunks = delta.ModifiedChunks;
                    foreach (var key in gridComp.ChunkCollection.ChunkCollection.Keys)
                    {
                        if (!delta.AllChunks.Contains(key))
                            _removedChunks.Add(key);
                    }

                    break;
                }
                case DecalGridState state:
                {
                    modifiedChunks = state.Chunks;
                    foreach (var key in gridComp.ChunkCollection.ChunkCollection.Keys)
                    {
                        if (!state.Chunks.ContainsKey(key))
                            _removedChunks.Add(key);
                    }

                    break;
                }
                default:
                    return;
            }

            if (_removedChunks.Count > 0)
                RemoveChunks(gridUid, gridComp, _removedChunks);

            if (modifiedChunks.Count > 0)
                UpdateChunks(gridUid, gridComp, modifiedChunks);
        }

        private void OnChunkUpdate(DecalChunkUpdateEvent ev)
        {
            foreach (var (netGrid, updatedGridChunks) in ev.Data)
            {
                if (updatedGridChunks.Count == 0)
                    continue;

                var gridId = GetEntity(netGrid);

                // LuaM-start
                if (!gridId.IsValid())
                {
                    QueuePendingChunks(netGrid, updatedGridChunks);
                    continue;
                }
                // LuaM-end

                if (!TryComp(gridId, out DecalGridComponent? gridComp))
                {
                    Log.Error($"Received decal information for an entity without a decal component: {ToPrettyString(gridId)}");
                    continue;
                }

                UpdateChunks(gridId, gridComp, updatedGridChunks);
            }

            // Now we'll cull old chunks out of range as the server will send them to us anyway.
            foreach (var (netGrid, chunks) in ev.RemovedChunks)
            {
                if (chunks.Count == 0)
                    continue;

                var gridId = GetEntity(netGrid);

                // LuaM-start
                if (!gridId.IsValid())
                {
                    if (_pendingChunks.TryGetValue(netGrid, out var pending))
                    {
                        foreach (var index in chunks)
                        {
                            pending.Chunks.Remove(index);
                        }
                    }

                    continue;
                }
                // LuaM-end

                if (!TryComp(gridId, out DecalGridComponent? gridComp))
                {
                    Log.Error($"Received decal information for an entity without a decal component: {ToPrettyString(gridId)}");
                    continue;
                }

                RemoveChunks(gridId, gridComp, chunks);
            }
        }

        // LuaM-start
        private void QueuePendingChunks(NetEntity netGrid, Dictionary<Vector2i, DecalChunk> chunks)
        {
            var expires = _timing.RealTime + PendingChunkLifetime;
            if (!_pendingChunks.TryGetValue(netGrid, out var pending))
            {
                _pendingChunks[netGrid] = (new Dictionary<Vector2i, DecalChunk>(chunks), expires);
                return;
            }

            foreach (var (index, chunk) in chunks)
            {
                pending.Chunks[index] = chunk;
            }

            _pendingChunks[netGrid] = (pending.Chunks, expires);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            if (_pendingChunks.Count == 0)
                return;

            var now = _timing.RealTime;
            foreach (var (netGrid, pending) in _pendingChunks)
            {
                var gridId = GetEntity(netGrid);
                if (TryComp(gridId, out DecalGridComponent? gridComp))
                {
                    if (pending.Chunks.Count > 0)
                        UpdateChunks(gridId, gridComp, pending.Chunks);

                    _pendingChunks.Remove(netGrid);
                }
                else if (now > pending.Expires)
                {
                    _pendingChunks.Remove(netGrid);
                }
            }
        }
        // LuaM-end

        private void UpdateChunks(EntityUid gridId, DecalGridComponent gridComp, Dictionary<Vector2i, DecalChunk> updatedGridChunks)
        {
            var chunkCollection = gridComp.ChunkCollection.ChunkCollection;

            // Update any existing data / remove decals we didn't receive data for.
            foreach (var (indices, newChunkData) in updatedGridChunks)
            {
                if (chunkCollection.TryGetValue(indices, out var chunk))
                {
                    _removedUids.Clear();
                    _removedUids.UnionWith(chunk.Decals.Keys);
                    _removedUids.ExceptWith(newChunkData.Decals.Keys);
                    foreach (var removedUid in _removedUids)
                    {
                        OnDecalRemoved(gridId, removedUid, gridComp, indices, chunk);
                        gridComp.DecalIndex.Remove(removedUid);
                    }
                }

                chunkCollection[indices] = newChunkData;

                foreach (var (uid, decal) in newChunkData.Decals)
                {
                    gridComp.DecalIndex[uid] = indices;
                }
            }
        }

        private void RemoveChunks(EntityUid gridId, DecalGridComponent gridComp, IEnumerable<Vector2i> chunks)
        {
            var chunkCollection = gridComp.ChunkCollection.ChunkCollection;

            foreach (var index in chunks)
            {
                if (!chunkCollection.TryGetValue(index, out var chunk))
                    continue;

                foreach (var decalId  in chunk.Decals.Keys)
                {
                    OnDecalRemoved(gridId, decalId, gridComp, index, chunk);
                    gridComp.DecalIndex.Remove(decalId);
                }

                chunkCollection.Remove(index);
            }
        }
    }
}
