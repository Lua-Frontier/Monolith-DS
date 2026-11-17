using System.Reflection;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._LuaM.StateRecovery;

// -- Движок падает в OnGridGetState на повторном чанке в ChunkDeletionHistory, и в этот тик ни один игрок не получает состояние.
public sealed class GridDeletionHistorySystem : EntitySystem
{
    private static readonly FieldInfo? HistoryField =
        typeof(MapGridComponent).GetField("ChunkDeletionHistory", BindingFlags.Instance | BindingFlags.NonPublic);

    private readonly HashSet<Vector2i> _seen = new();
    private bool _valid;

    public override void Initialize()
    {
        base.Initialize();

        _valid = HistoryField?.FieldType == typeof(List<(GameTick, Vector2i)>);
        if (!_valid)
            Log.Error("MapGridComponent.ChunkDeletionHistory changed shape; duplicate chunk deletions are no longer removed.");
    }

    public void Dedupe()
    {
        if (!_valid)
            return;

        var query = EntityQueryEnumerator<MapGridComponent>();
        while (query.MoveNext(out var grid))
        {
            if (HistoryField!.GetValue(grid) is not List<(GameTick Tick, Vector2i Indices)> { Count: > 1 } history)
                continue;

            _seen.Clear();
            var duplicate = false;
            foreach (var (_, indices) in history)
            {
                if (!_seen.Add(indices))
                {
                    duplicate = true;
                    break;
                }
            }

            if (!duplicate)
                continue;

            _seen.Clear();
            for (var i = history.Count - 1; i >= 0; i--)
            {
                if (!_seen.Add(history[i].Indices))
                    history.RemoveAt(i);
            }
        }
    }
}
