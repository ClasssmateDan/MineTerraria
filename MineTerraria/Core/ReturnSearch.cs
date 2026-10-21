using System;
using System.Numerics;

namespace DimensionalTerraria.Core;

public static class ReturnSearch
{
    public static bool TryNear(ICollisionWorld world, Vector2 anchor, Vector2 bodySize, IExplorationMap? exploration,
        out Vector2 result, int radius = 32)
    {
        result = anchor;
        // The exact departure point remains a recovery exit even if its map record was not ready on entry.
        if (SpatialRules.ValidateReturn(world, anchor, bodySize) == ReturnFailure.None) return true;
        for (int distance = 1; distance <= radius; distance++)
        {
            // Prefer upward displacement; terrain, liquids and exploration are all checked afresh.
            for (int x = -distance; x <= distance; x++)
                if (Try(x, -distance, out result) || Try(x, distance, out result)) return true;
            for (int y = -distance + 1; y < distance; y++)
                if (Try(-distance, y, out result) || Try(distance, y, out result)) return true;
        }
        return false;

        bool Try(int x, int y, out Vector2 candidate)
        {
            candidate = anchor + new Vector2(x, y);
            return SpatialRules.ValidateReturn(world, candidate, bodySize, exploration: exploration) == ReturnFailure.None;
        }
    }
}
