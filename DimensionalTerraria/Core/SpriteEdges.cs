using System;

namespace DimensionalTerraria.Core;

public static class SpriteEdges
{
    // Generated PNGs contain nearly transparent white matte pixels. Terraria's
    // raw-image reader uploads their RGB unchanged to a premultiplied renderer.
    // Use an opaque pixel-art silhouette, removing only a tiny boundary band.
    public static byte[] Clean(byte[] rgba, int width, int height, byte[]? silhouette = null)
    {
        if (width <= 0 || height <= 0 || rgba.Length != checked(width * height * 4) ||
            silhouette is not null && silhouette.Length != rgba.Length) throw new ArgumentException("Invalid sprite dimensions");
        byte[] mask = silhouette ?? rgba, result = new byte[rgba.Length];
        int radius = Math.Clamp(height / 600, 1, 2);
        for (int y = radius; y < height - radius; y++) for (int x = radius; x < width - radius; x++)
        {
            int p = (x + y * width) * 4;
            if (rgba[p + 3] < 240) continue;
            bool inside = true;
            for (int dy = -radius; dy <= radius && inside; dy++) for (int dx = -radius; dx <= radius; dx++)
                if (mask[(x + dx + (y + dy) * width) * 4 + 3] < 240) { inside = false; break; }
            if (!inside) continue;
            result[p] = rgba[p]; result[p + 1] = rgba[p + 1]; result[p + 2] = rgba[p + 2]; result[p + 3] = 255;
        }
        return result;
    }
}
