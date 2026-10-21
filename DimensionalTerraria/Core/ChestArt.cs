using System;

namespace DimensionalTerraria.Core;

public static class ChestArt
{
    public static byte[] Face(byte[] source, int face)
    {
        if (source.Length != 64 * 64 * 4 || face < 0 || face > 2) throw new ArgumentException("Chest UV source must be 64x64 RGBA");
        byte[] pixels = new byte[16 * 16 * 4];
        for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++)
        {
            int u = x * 14 / 16, v = y * 15 / 16;
            int sx, sy;
            if (face == 0) { sx = 14 + u; sy = y * 14 / 16; }
            else if (v < 5) { sx = (face == 1 ? 14 : 0) + u; sy = 14 + v; }
            else { sx = (face == 1 ? 14 : 0) + u; sy = 33 + v - 5; }
            if (face == 1 && x >= 7 && x < 9 && y >= 4 && y < 8) { sx = 1 + x - 7; sy = 1 + y - 4; }
            Array.Copy(source, (sx + sy * 64) * 4, pixels, (x + y * 16) * 4, 4);
        }
        return pixels;
    }
    public static byte[] Atlas(byte[] source)
    {
        if (source.Length != 64 * 64 * 4) throw new ArgumentException("Chest UV source must be 64x64 RGBA");
        // BasicChest draws three states at Y offsets 0, 38 and 76, even for
        // modded chests. Each state must contain all four 16/18px tile pieces.
        byte[] atlas = new byte[36 * 114 * 4];
        for (int frame = 0; frame < 3; frame++)
        {
            // Sample the original 14x5 lid and 14x10 body at an exact 2x
            // scale, without the previous 14->16->28 resampling distortion.
            for (int y = 0; y < 20; y++) for (int x = 0; x < 28; x++) Put(x + 2, y + 12, 14 + x / 2, 33 + y / 2);
            int lidHeight = frame == 0 ? 10 : frame == 1 ? 7 : 4;
            for (int y = 0; y < lidHeight; y++) for (int x = 0; x < 28; x++) Put(x + 2, y + 2, 14 + x / 2, 14 + y * 5 / lidHeight);
            if (frame > 0) for (int y = 2 + lidHeight; y < 12; y++) for (int x = 2; x < 30; x++)
                Set(x, y, 37, 24, 13, 255);
            int lockY = frame == 0 ? 8 : frame == 1 ? 5 : 2;
            for (int y = 0; y < 8; y++) for (int x = 0; x < 4; x++) Put(x + 14, y + lockY, 1 + x / 2, 1 + y / 2);

            void Put(int x, int y, int sx, int sy)
            {
                int p = (sx + sy * 64) * 4;
                Set(x, y, source[p], source[p + 1], source[p + 2], source[p + 3]);
            }
            void Set(int x, int y, byte r, byte g, byte b, byte a)
            {
                int at = (x / 16 * 18 + x % 16 + (frame * 38 + y / 16 * 18 + y % 16) * 36) * 4;
                atlas[at] = r; atlas[at + 1] = g; atlas[at + 2] = b; atlas[at + 3] = a;
            }
        }
        return atlas;
    }
}
