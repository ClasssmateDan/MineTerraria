using System;

namespace DimensionalTerraria.Core;

public readonly record struct SpriteCrop(int X, int Y, int Width, int Height, float FootX)
{
    public bool Empty => Width <= 0 || Height <= 0;
}

public static class SpriteSheetLayout
{
    public static bool TryFrame(SpriteCrop[] frames, int index, out SpriteCrop frame)
    {
        frame = default;
        if ((uint)index >= (uint)frames.Length || frames[index].Empty) return false;
        frame = frames[index];
        return true;
    }

    // Generated sheets can have padding and dimensions not divisible by the grid.
    // Each source rectangle is bounded to its own cell; feet anchor animation.
    public static SpriteCrop[] Slice(int width, int height, int columns, int rows, Func<int, int, byte> alpha)
    {
        if (width <= 0 || height <= 0 || columns <= 0 || rows <= 0 || columns > width || rows > height) return Array.Empty<SpriteCrop>();
        SpriteCrop[] frames = new SpriteCrop[columns * rows];
        for (int i = 0; i < frames.Length; i++)
        {
            int x0 = i % columns * width / columns, x1 = (i % columns + 1) * width / columns;
            int y0 = i / columns * height / rows, y1 = (i / columns + 1) * height / rows;
            int left = x1, top = y1, right = x0 - 1, bottom = y0 - 1;
            for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
            {
                if (alpha(x, y) < 128) continue;
                left = Math.Min(left, x); right = Math.Max(right, x);
                top = Math.Min(top, y); bottom = Math.Max(bottom, y);
            }
            if (right < left || bottom < top) continue;
            long sum = 0; int count = 0;
            int feet = Math.Max(top, bottom - Math.Max(1, (bottom - top) / 12));
            for (int y = feet; y <= bottom; y++)
            for (int x = left; x <= right; x++) if (alpha(x, y) >= 128) { sum += x; count++; }
            float footX = count == 0 ? (left + right + 1) / 2f : sum / (float)count;
            frames[i] = new(left, top, right - left + 1, bottom - top + 1, footX - left);
        }
        return frames;
    }
}

public static class ProjectionWeaponStats
{
    public const int NormalDamage = 30;
    public const int DetachedDamage = 999;
    public static int Damage(bool detached) => detached ? DetachedDamage : NormalDamage;
    public static int ShotPickPower(int damage) => Math.Clamp(damage, 35, 200);
}
