using Content.Shared.Maps;
using Robust.Shared.Noise;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Parallax.Biomes.Layers;

[Serializable, NetSerializable]
public sealed partial class BiomeEntityLayer : IBiomeWorldLayer
{
    /// <inheritdoc/>
    [DataField("allowedTiles")]
    public List<ProtoId<ContentTileDefinition>> AllowedTiles { get; private set; } = new();

    [DataField("noise")] public FastNoiseLite Noise { get; private set; } = new(0);

    /// <inheritdoc/>
    [DataField("threshold")]
    public float Threshold { get; private set; } = 0.5f;

    /// <inheritdoc/>
    [DataField("invert")] public bool Invert { get; private set; } = false;

    [DataField("entities", required: true)]
    public List<EntProtoId> Entities = new();

    // LuaM-start
    /// <summary>
    /// Сколько из 8 соседних тайлов тоже должны пройти порог слоя, иначе сущность не ставится: убирает одиночные стены на краях шума
    /// </summary>
    [DataField]
    public int MinNeighbours;
    // LuaM-end
}
