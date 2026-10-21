using System;
using System.Numerics;

namespace DimensionalTerraria.Core;

public readonly record struct HeldItemLayout(Vector2 Size, Vector2 Grip, float Rotation, bool FlipHorizontally, bool AlignUpright)
{
    public static float HandOffsetX(float projectionX) => float.IsFinite(projectionX) && projectionX > 0
        ? MathF.Min(0.70f, 0.72f / projectionX) : 0.70f;

    // Camera-space units, shared with the arm. UI scale and screen pixel sizes
    // must never participate in the item's size or attachment point.
    public static HeldItemLayout Create(int frameWidth, int frameHeight, bool tool, bool shooting)
    {
        if (frameWidth <= 0 || frameHeight <= 0) return default;
        float longest = Math.Max(frameWidth, frameHeight);
        float scale = Math.Clamp(longest / 64f, 0.28f, 0.7f) * 1.15f / longest;
        Vector2 grip = shooting ? new(0.68f, 0.62f) : tool ? new(0.80f, 0.80f) : new(0.5f, 0.68f);
        float rotation = shooting ? -0.15f : 0.22f;
        return new HeldItemLayout(new(frameWidth * scale, frameHeight * scale), grip, rotation, shooting || tool, tool);
    }

    public Vector2 At(float u, float v) => new((u - Grip.X) * Size.X, (Grip.Y - v) * Size.Y);
}
