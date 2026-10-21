using System.Numerics;

namespace DimensionalTerraria.Core;

public static class GroundSpawnRules
{
    public static readonly Vector2 SheepSize = new(34f / 16, 28f / 16);
    public static bool SheepPosition(ICollisionWorld world, int x, int y, out Vector2 center, IExplorationMap? exploration = null)
        => Position(world, x, y, SheepSize, out center, exploration);

    public static bool IronGolemPosition(ICollisionWorld world, int x, int y, out Vector2 center, IExplorationMap? exploration = null)
        => Position(world, x, y, new(IronGolemBrain.Width / 16f, IronGolemBrain.Height / 16f), out center, exploration);

    public static bool Position(ICollisionWorld world, int x, int y, Vector2 bodySize, out Vector2 center, IExplorationMap? exploration = null)
    {
        center = default;
        if (x < 12 || y < 12 || x >= world.Width - 12 || y >= world.Height - 12 ||
            exploration is not null && !exploration.IsExplored(x, y)) return false;
        WorldCell floor = world.GetCell(x, y);
        if (floor.Shape == BlockShape.Empty) return false;
        center = new(x + 0.5f, y + (floor.Shape == BlockShape.HalfBlock ? 0.5f : 0) - bodySize.Y / 2);
        return SpatialRules.ValidateReturn(world, center, bodySize, exploration: exploration) == ReturnFailure.None;
    }
}
