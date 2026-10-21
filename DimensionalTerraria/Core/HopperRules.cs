using System;
using System.Numerics;

namespace DimensionalTerraria.Core;

public static class HopperRules
{
    public const int TransferInterval = 8;

    public static bool CanCollect(GridCell hopper, Vector2 itemCenter, Vector2 itemSize)
    {
        if (hopper.X < 0 || hopper.Y < 1 || !float.IsFinite(itemCenter.X) || !float.IsFinite(itemCenter.Y) ||
            !float.IsFinite(itemSize.X) || !float.IsFinite(itemSize.Y) || itemSize.X <= 0 || itemSize.Y <= 0) return false;
        float bottom = itemCenter.Y + itemSize.Y / 2;
        // Test the feet, not the sprite center. An item resting on the block
        // directly above the hopper touches the upper boundary from outside.
        return itemCenter.X >= hopper.X && itemCenter.X < hopper.X + 1 &&
            bottom >= hopper.Y - 1 - 0.001f && bottom <= hopper.Y + 0.001f;
    }
}
