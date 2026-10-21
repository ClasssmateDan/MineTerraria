using System.Numerics;

namespace DimensionalTerraria.Core;

public static class LiquidRules
{
    public static bool Bounds(BlockShape shape, int amount, out float top, out float bottom)
    {
        top = bottom = 0;
        if (amount <= 0 || amount > 255 || shape == BlockShape.Solid) return false;
        bottom = shape == BlockShape.HalfBlock ? 0.5f : 1;
        top = bottom * (1 - amount / 255f);
        return true;
    }
    public static Vector4 Color(int kind) => kind switch {
        1 => new(1f, 0.29f, 0.04f, 0.88f),
        2 => new(0.94f, 0.60f, 0.10f, 0.72f),
        3 => new(0.66f, 0.43f, 0.94f, 0.65f),
        _ => new(0.20f, 0.51f, 0.88f, 0.58f)
    };
}
