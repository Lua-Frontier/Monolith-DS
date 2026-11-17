namespace Content.Server.Decals;

public sealed partial class DecalSystem
{
    private const int DecalFlushTicks = 3;

    private readonly Dictionary<NetEntity, HashSet<Vector2i>> _pendingChunks = new();

    private int _decalTicks;

    private void GatherDirtyChunks()
    {
        foreach (var (grid, chunks) in _dirtyChunks)
        {
            if (_pendingChunks.TryGetValue(grid, out var pending))
                pending.UnionWith(chunks);
            else
                _pendingChunks[grid] = chunks;
        }

        _dirtyChunks.Clear();

        if (++_decalTicks < DecalFlushTicks)
            return;

        _decalTicks = 0;
        foreach (var (grid, chunks) in _pendingChunks)
        {
            _dirtyChunks[grid] = chunks;
        }

        _pendingChunks.Clear();
    }
}
