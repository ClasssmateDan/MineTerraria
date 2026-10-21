using System;
using System.IO;
using DimensionalTerraria.Core;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace DimensionalTerraria.Systems;

// PostSetupContent runs on the loader worker. Read packaged pixels on the CPU;
// never use Texture2D.GetData or initialize the render-time sprite atlas here.
internal static class StorageIconPixels
{
    internal static byte[] ReadFace(Mod mod, string path)
    {
        byte[] pixels = Read(mod, path, out int width, out int height);
        if (width != 16 || height != 16) throw new InvalidDataException("Minecraft block faces must be 16x16");
        return pixels;
    }

    internal static byte[] GolemThumbnail(Mod mod)
    {
        byte[] source = Read(mod, "Textures/IronGolem", out int width, out int height);
        source = SpriteEdges.Clean(source, width, height);
        SpriteCrop[] frames = SpriteSheetLayout.Slice(width, height, 4, 2, (x, y) => source[(x + y * width) * 4 + 3]);
        if (!SpriteSheetLayout.TryFrame(frames, 0, out SpriteCrop pose)) throw new InvalidDataException("Iron golem resting pose is unavailable");
        byte[] cropped = new byte[checked(pose.Width * pose.Height * 4)];
        for (int y = 0; y < pose.Height; y++)
            Array.Copy(source, (pose.X + (pose.Y + y) * width) * 4, cropped, y * pose.Width * 4, pose.Width * 4);
        return ContentPixelArt.Thumbnail(pose.Width, pose.Height, cropped, 32, 40);
    }

    internal static byte[] Read(Mod mod, string path, out int width, out int height)
    {
        // tML converts all packaged PNG assets to .rawimg at build time.
        string prefix = mod.Name + "/";
        if (path.StartsWith(prefix, StringComparison.Ordinal)) path = path[prefix.Length..];
        using Stream stream = mod.GetFileStream(path + ".rawimg") ?? throw new FileNotFoundException("Missing packaged icon source", path);
        byte[] pixels = ImageIO.ReadRaw(stream, out width, out height);
        if (width <= 0 || height <= 0 || pixels.Length != checked(width * height * 4))
            throw new InvalidDataException("Invalid packaged icon pixels: " + path);
        return pixels;
    }
}
