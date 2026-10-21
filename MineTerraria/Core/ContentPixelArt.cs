using System;
using System.IO;

namespace DimensionalTerraria.Core;

// Small deterministic pixel shapes. These are logical game textures, not large
// source images whose dimensions can leak into native held-item positioning.
public static class ContentPixelArt
{
    public static byte[] PulseAtlas(byte[] off, byte[] on)
    {
        if (off.Length != 16 * 16 * 4 || on.Length != 16 * 16 * 4)
            throw new ArgumentException("Pulse faces must be 16x16 RGBA.");
        byte[] atlas = new byte[16 * 36 * 4];
        Array.Copy(off, 0, atlas, 0, off.Length);
        Array.Copy(on, 0, atlas, 16 * 18 * 4, on.Length);
        return atlas;
    }

    public static byte[] BlockFace(int rgb, bool gem)
    {
        byte[] pixels = new byte[16 * 16 * 4];
        for (int y = 0; y < 16; y++)
        for (int x = 0; x < 16; x++)
        {
            float shade = x == 0 || y == 0 ? 1.28f : x == 15 || y == 15 ? 0.55f : x == 1 || y == 1 ? 1.12f : x == 14 || y == 14 ? 0.76f : 1;
            if (gem && x > 2 && x < 13 && y > 2 && y < 13) shade *= x + y < 14 ? 1.15f : 0.93f;
            if (!gem && x > 2 && x < 13 && y % 7 == 0) shade *= 0.94f;
            Put(pixels, 16, x, y, rgb, shade);
        }
        return pixels;
    }

    public static byte[] Cube(int rgb, bool gem)
    {
        byte[] pixels = new byte[24 * 24 * 4];
        byte[] face = BlockFace(rgb, gem);
        for (int y = 2; y < 22; y++)
        for (int x = 2; x < 22; x++)
        {
            // A closed hexagon: top (12,2), shoulders (2,7)/(22,7),
            // bottom corners (2,17)/(22,17), bottom (12,22).
            // The visible side faces slope DOWN toward the central seam.
            float dx = (x + 0.5f - 12) / 10, py = y + 0.5f;
            float seam = 12 - Math.Abs(dx) * 5;
            if (py >= 2 + Math.Abs(dx) * 5 && py < seam)
                Put(pixels, 24, x, y, rgb, 1.18f);
            else if (py >= seam && py < 22 - Math.Abs(dx) * 5)
            {
                int u = Math.Clamp((int)(Math.Abs(dx) * 15), 0, 15);
                int v = Math.Clamp((int)((py - seam) * 1.6f), 0, 15);
                int at = (u + v * 16) * 4;
                Put(pixels, 24, x, y, face[at] << 16 | face[at + 1] << 8 | face[at + 2], x < 12 ? 0.9f : 0.68f);
            }
        }
        return pixels;
    }

    public static byte[] SheepEgg() => SpawnEgg(0xEBE7DC, 0xA6A39A, 0x5E5A54);
    public static byte[] SlimeEgg() => SpawnEgg(0x78BDF2, 0x3472CB, 0x203E76);
    public static byte[] VillagerEgg() => SpawnEgg(0x563C33, 0xBD8B72, 0x2E211E);

    private static byte[] SpawnEgg(int shell, int spots, int outline)
    {
        byte[] pixels = new byte[16 * 24 * 4];
        for (int y = 2; y <= 21; y++)
        for (int x = 1; x <= 14; x++)
        {
            float radius = y < 10 ? 3 + (y - 2) * 0.45f : 6 - (y - 12) * (y - 12) * 0.035f;
            float dx = Math.Abs(x - 7.5f);
            if (dx > radius) continue;
            bool edge = dx > radius - 1 || y == 2 || y == 21;
            bool spot = (x / 3 + y / 4 * 3) % 7 is 1 or 4;
            Put(pixels, 16, x, y, edge ? outline : spot ? spots : shell, x < 7 ? 1.06f : 0.92f);
        }
        return pixels;
    }

    public static byte[] TexturedCube(byte[] top, byte[] front, byte[] side)
    {
        if (top.Length != 16 * 16 * 4 || front.Length != top.Length || side.Length != top.Length)
            throw new ArgumentException("Cube faces must be 16x16 RGBA textures");
        byte[] pixels = new byte[24 * 24 * 4];
        for (int y = 2; y < 22; y++)
        for (int x = 2; x < 22; x++)
        {
            float dx = (x + 0.5f - 12) / 10, py = y + 0.5f, seam = 12 - Math.Abs(dx) * 5;
            byte[] face; float u, v, shade;
            if (py >= 2 + Math.Abs(dx) * 5 && py < seam)
            {
                face = top; shade = 1;
                float sum = (py - 2) / 5;
                u = (sum + dx) / 2; v = (sum - dx) / 2;
            }
            else if (py >= seam && py < 22 - Math.Abs(dx) * 5)
            {
                face = x < 12 ? front : side; shade = x < 12 ? 0.9f : 0.72f;
                u = x < 12 ? dx + 1 : dx; v = (py - seam) / 10;
            }
            else continue;
            int at = (Math.Clamp((int)(u * 16), 0, 15) + Math.Clamp((int)(v * 16), 0, 15) * 16) * 4;
            int rgb = face[at] << 16 | face[at + 1] << 8 | face[at + 2];
            Put(pixels, 24, x, y, rgb, shade);
            pixels[(x + y * 24) * 4 + 3] = face[at + 3];
        }
        return pixels;
    }

    public static byte[] Thumbnail(int width, int height, byte[] source, int targetWidth, int targetHeight)
    {
        if (width <= 0 || height <= 0 || targetWidth < 3 || targetHeight < 3 || source.Length != checked(width * height * 4))
            throw new ArgumentException("Invalid thumbnail dimensions");
        float scale = Math.Min((targetWidth - 2f) / width, (targetHeight - 2f) / height);
        int w = Math.Max(1, (int)Math.Round(width * scale)), h = Math.Max(1, (int)Math.Round(height * scale));
        int x0 = (targetWidth - w) / 2, y0 = (targetHeight - h) / 2;
        byte[] pixels = new byte[checked(targetWidth * targetHeight * 4)];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int from = (Math.Min(width - 1, x * width / w) + Math.Min(height - 1, y * height / h) * width) * 4;
            int to = (x0 + x + (y0 + y) * targetWidth) * 4;
            Array.Copy(source, from, pixels, to, 4);
        }
        return pixels;
    }

    public static byte[] BoatTileAtlas(byte[] icon)
    {
        if (icon.Length != 16 * 16 * 4) throw new ArgumentException("Boat icon must be 16x16 RGBA");
        byte[] atlas = new byte[54 * 36 * 4];
        for (int y = 0; y < 32; y++) for (int x = 0; x < 48; x++)
        {
            int from = (x / 3 + y / 2 * 16) * 4;
            int to = (x / 16 * 18 + x % 16 + (y / 16 * 18 + y % 16) * 54) * 4;
            Array.Copy(icon, from, atlas, to, 4);
        }
        return atlas;
    }

    public static byte[] PistonAtlas(byte[] side, byte[] inner, byte[] top, byte[] stickyTop, bool stickyBase, bool head)
    {
        foreach (byte[] face in new[] { side, inner, top, stickyTop })
            if (face.Length != 16 * 16 * 4) throw new ArgumentException("Piston faces must be 16x16 RGBA");
        byte[] atlas = new byte[72 * 36 * 4];
        for (int row = 0; row < 2; row++) for (int direction = 0; direction < 4; direction++)
        for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
        {
            byte[] face = side; int sx = x, sy = y;
            if (head)
            {
                if (y < 4) { face = row == 1 ? stickyTop : top; sy = y * 4; }
                else if (x >= 6 && x < 10) { face = side; sx = (y - 4) * 16 / 12; sy = x - 6; }
                else continue;
            }
            else if (row == 1 && y < 4) face = inner;
            else if (stickyBase && row == 0 && y < 3) { face = stickyTop; sy = y * 5; }
            (int rx, int ry) = direction switch { 0 => (x, y), 1 => (15 - y, x), 2 => (15 - x, 15 - y), _ => (y, 15 - x) };
            Array.Copy(face, (sx + sy * 16) * 4, atlas, (direction * 18 + rx + (row * 18 + ry) * 72) * 4, 4);
        }
        return atlas;
    }

    public static byte[] RawImage(int width, int height, byte[] pixels)
    {
        if (width <= 0 || height <= 0 || pixels.Length != checked(width * height * 4)) throw new ArgumentException("Invalid pixel dimensions");
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(1); writer.Write(width); writer.Write(height); writer.Write(pixels);
        return stream.ToArray();
    }

    private static void Put(byte[] pixels, int width, int x, int y, int rgb, float shade)
    {
        int at = (x + y * width) * 4;
        pixels[at] = (byte)Math.Clamp((int)((rgb >> 16 & 255) * shade), 0, 255);
        pixels[at + 1] = (byte)Math.Clamp((int)((rgb >> 8 & 255) * shade), 0, 255);
        pixels[at + 2] = (byte)Math.Clamp((int)((rgb & 255) * shade), 0, 255);
        pixels[at + 3] = 255;
    }
}
