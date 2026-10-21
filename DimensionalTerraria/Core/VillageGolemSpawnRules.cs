using System;
using System.Numerics;

namespace DimensionalTerraria.Core;

public readonly record struct GolemSpawnCell(BlockShape Shape, byte LiquidAmount = 0, bool Sloped = false);
public interface IGolemSpawnWorld : ICollisionWorld
{
    GolemSpawnCell GolemCellAt(int x, int y);
}

// Autonomous village guards have stricter footing but tolerate shallow liquid.
// Item/debug/iron-T spawns and player return checks keep their existing rules.
public static class VillageGolemSpawnRules
{
    public const int HorizontalRadius = 8, VerticalRadius = 6;
    private const float Epsilon = .0001f;
    private static readonly Vector2 Body = new(IronGolemBrain.Width / 16f, IronGolemBrain.Height / 16f);

    public static bool TryFind(IGolemSpawnWorld world, int originX, int originY, out Vector2 center)
    {
        center = default;
        if (originX < 0 || originY < 0 || originX >= world.Width || originY >= world.Height) return false;
        // Visit every column once: 0, -1, +1, -2, +2, ... -8, +8.
        for (int step = 0; step <= HorizontalRadius * 2; step++)
        {
            int offset = step % 2 == 0 ? step / 2 : -(step + 1) / 2;
            for (int floor = originY + VerticalRadius; floor >= originY - VerticalRadius; floor--)
                if (Position(world, originX + offset, floor, out center)) return true;
        }
        return false;
    }

    public static bool Position(IGolemSpawnWorld world, int x, int y, out Vector2 center)
    {
        center = default;
        if (x < 12 || y < 12 || x >= world.Width - 12 || y >= world.Height - 12) return false;
        GolemSpawnCell middle = world.GolemCellAt(x, y);
        if (!Support(middle)) return false;
        float feet = y + (middle.Shape == BlockShape.HalfBlock ? .5f : 0);
        for (int dx = -1; dx <= 1; dx++)
        {
            GolemSpawnCell support = world.GolemCellAt(x + dx, y);
            if (!Support(support) || (support.Shape == BlockShape.HalfBlock) != (middle.Shape == BlockShape.HalfBlock)) return false;
        }
        Vector2 position = new(x + .5f, feet - Body.Y / 2);
        // Reuse the complete-body geometry and boundary checks; inspect actual
        // liquid height below instead of treating every wet cell as impassable.
        if (SpatialRules.ValidateReturn(new TerrainOnly(world), position, Body) != ReturnFailure.None) return false;
        int top = (int)MathF.Floor(feet - Body.Y + Epsilon), bottom = (int)MathF.Floor(feet - Epsilon);
        for (int column = x - 1; column <= x + 1; column++)
        for (int row = top; row <= bottom; row++)
        {
            byte amount = world.GolemCellAt(column, row).LiquidAmount;
            if (amount == 0) continue;
            float surface = row + 1 - amount / 255f;
            // Half a liquid byte accounts for 128/255 representing half a tile.
            // A full bottom cell plus even one byte above it still exceeds 1 tile.
            if (feet - surface > 1 + .5f / 255 + Epsilon) return false;
        }
        center = position;
        return true;
    }

    private static bool Support(GolemSpawnCell cell) => !cell.Sloped && cell.Shape is BlockShape.Solid or BlockShape.HalfBlock or BlockShape.Platform;
    private sealed class TerrainOnly(IGolemSpawnWorld world) : ICollisionWorld
    {
        public int Width => world.Width;
        public int Height => world.Height;
        public WorldCell GetCell(int x, int y) => world.GetCell(x, y) with { HasLiquid = false };
    }
}
