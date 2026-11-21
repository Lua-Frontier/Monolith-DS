using System.Numerics;
using Content.Shared.Construction;
using Content.Shared.Construction.Conditions;
using Content.Shared.Maps;
using Content.Shared.Physics;
using JetBrains.Annotations;
using Robust.Shared.Map;

namespace Content.Shared._LuaM.Construction.Conditions;

/// <summary>
/// Like <see cref="Content.Shared.Construction.Conditions.TileNotBlocked"/>, but checks every tile a structure wider than one tile covers.
/// </summary>
[UsedImplicitly]
[DataDefinition]
public sealed partial class TilesNotBlocked : IConstructionCondition
{
    /// <summary>
    /// Covered tiles relative to the placed one, for the structure facing south. They turn with the structure.
    /// </summary>
    [DataField(required: true)]
    public List<Vector2i> Tiles = new();

    [DataField]
    public bool FailIfSpace = true;

    [DataField]
    public bool FailIfNotSturdy = true;

    public bool Condition(EntityUid user, EntityCoordinates location, Direction direction)
    {
        if (!IoCManager.Resolve<IEntityManager>().TrySystem<TurfSystem>(out var turfSystem))
            return false;

        var angle = direction.ToAngle();
        foreach (var tile in Tiles)
        {
            var offset = angle.RotateVec(new Vector2(tile.X, tile.Y));
            var coords = location.Offset(new Vector2(MathF.Round(offset.X), MathF.Round(offset.Y)));

            if (!turfSystem.TryGetTileRef(coords, out var tileRef))
                return false;

            if (FailIfSpace && turfSystem.IsSpace(tileRef.Value))
                return false;

            if (FailIfNotSturdy && !turfSystem.GetContentTileDefinition(tileRef.Value).Sturdy)
                return false;

            if (turfSystem.IsTileBlocked(tileRef.Value, CollisionGroup.Impassable))
                return false;
        }

        return true;
    }

    public ConstructionGuideEntry GenerateGuideEntry()
    {
        return new ConstructionGuideEntry
        {
            Localization = "construction-step-condition-tiles-not-blocked",
        };
    }
}
