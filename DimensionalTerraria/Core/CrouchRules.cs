using System;
using System.Numerics;

namespace DimensionalTerraria.Core;

public readonly record struct FootSupport(bool Ground, bool DropThrough);

public static class CrouchRules
{
    public static FootSupport Support(ICollisionWorld world, float left, float feet, float width)
    {
        if (!float.IsFinite(left) || !float.IsFinite(feet) || !float.IsFinite(width) || width <= 0 || width > 8) return default;
        int y = (int)MathF.Floor(feet + .03f);
        if (y < 10 || y >= world.Height - 10 || left < 10 || left + width > world.Width - 10) return default;
        bool hard = false, platform = false;
        for (int x = (int)MathF.Floor(left); x <= (int)MathF.Floor(left + width - .001f); x++)
        {
            BlockShape shape = world.GetCell(x, y).Shape;
            if (shape == BlockShape.Platform) platform = true;
            else if (shape == BlockShape.Solid || shape == BlockShape.HalfBlock && feet + .03f >= y + .5f) hard = true;
        }
        return new(hard || platform, platform && !hard);
    }

    public static float GuardStep(ICollisionWorld world, float left, float feet, float width, float step)
    {
        if (!float.IsFinite(step) || MathF.Abs(step) > 4) return 0;
        int count = Math.Max(1, (int)MathF.Ceiling(MathF.Abs(step) / .025f));
        float safe = 0;
        for (int i = 1; i <= count; i++)
        {
            float next = step * i / count;
            if (!Support(world, left + next, feet, width).Ground) break;
            safe = next;
        }
        return safe;
    }

    // Head stays square; torso progressively leans around the waist. Feet and
    // legs below the waist keep their original positions and collision box.
    public static Vector2 Offset(float aboveFeet, float amount, int direction)
    {
        if (!float.IsFinite(aboveFeet) || !float.IsFinite(amount)) return Vector2.Zero;
        float t = Math.Clamp((aboveFeet - 18) / 13, 0, 1) * Math.Clamp(amount, 0, 1);
        return new Vector2(6 * Math.Sign(direction), 4) * t;
    }
}
