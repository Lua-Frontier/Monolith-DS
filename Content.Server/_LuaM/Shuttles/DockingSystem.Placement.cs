using System.Numerics;
using Content.Server._LuaM.Shuttles;
using Content.Server.Shuttles.Components;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map.Components;

namespace Content.Server.Shuttles.Systems;

public sealed partial class DockingSystem
{
    [Dependency] private DockIndexSystem _dockIndex = default!;

    private static bool TryGetDockPlacement(
        DockingComponent shuttleDock,
        TransformComponent shuttleDockXform,
        DockingComponent gridDock,
        TransformComponent gridDockXform,
        Box2 shuttleAABB,
        out Matrix3x2 matty,
        out Box2 shuttleDockedAABB,
        out Angle offsetAngle)
    {
        matty = Matrix3x2.Identity;
        shuttleDockedAABB = Box2.UnitCentered;
        offsetAngle = Angle.Zero;

        if (shuttleDock.Docked ||
            gridDock.Docked ||
            !shuttleDockXform.Anchored ||
            !gridDockXform.Anchored ||
            (shuttleDock.DockType & gridDock.DockType) == DockType.None)
        {
            return false;
        }

        var stationDockPos = shuttleDockXform.LocalPosition +
                             shuttleDockXform.LocalRotation.RotateVec(new Vector2(0f, -1f));
        var shuttleDockAngle = shuttleDockXform.LocalRotation;
        var gridDockAngle = gridDockXform.LocalRotation.Opposite();
        offsetAngle = gridDockAngle - shuttleDockAngle;

        var stationDockMatrix = Matrix3Helpers.CreateInverseTransform(stationDockPos, shuttleDockAngle);
        var gridXformMatrix = Matrix3Helpers.CreateTransform(gridDockXform.LocalPosition, gridDockAngle);
        matty = Matrix3x2.Multiply(stationDockMatrix, gridXformMatrix);
        shuttleDockedAABB = matty.TransformBox(shuttleAABB);
        return true;
    }

    private bool AnyTileIntersecting(Entity<MapGridComponent> grid, Box2 aabb)
    {
        var lowerX = (int) Math.Floor(aabb.Left);
        var lowerY = (int) Math.Floor(aabb.Bottom);
        var upperX = (int) Math.Ceiling(aabb.Right);
        var upperY = (int) Math.Ceiling(aabb.Top);
        if (upperX <= lowerX || upperY <= lowerY)
            return false;

        var size = GridChunkSize(grid.Comp);
        var chunkLower = _mapSystem.GridTileToChunkIndices(grid.Comp, new Vector2i(lowerX, lowerY));
        var chunkUpper = _mapSystem.GridTileToChunkIndices(grid.Comp, new Vector2i(upperX - 1, upperY - 1));
        for (var cx = chunkLower.X; cx <= chunkUpper.X; cx++)
        {
            for (var cy = chunkLower.Y; cy <= chunkUpper.Y; cy++)
            {
                if (!grid.Comp.HasChunk(new Vector2i(cx, cy)))
                    continue;

                var x1 = Math.Min(upperX, (cx + 1) * size);
                var y1 = Math.Min(upperY, (cy + 1) * size);
                for (var x = Math.Max(lowerX, cx * size); x < x1; x++)
                {
                    for (var y = Math.Max(lowerY, cy * size); y < y1; y++)
                    {
                        if (_mapSystem.TryGetTileRef(grid, grid.Comp, new Vector2i(x, y), out var tile) && !tile.Tile.IsEmpty)
                            return true;
                    }
                }
            }
        }

        return false;
    }

    private int GridChunkSize(MapGridComponent grid)
    {
        var size = 1;
        while (size < ushort.MaxValue && _mapSystem.GridTileToChunkIndices(grid, new Vector2i(size, 0)).X == 0)
        {
            size++;
        }

        return size;
    }
}
