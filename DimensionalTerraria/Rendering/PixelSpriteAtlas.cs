using System.Collections.Generic;
using DimensionalTerraria.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.ModLoader;

namespace DimensionalTerraria.Rendering;

internal sealed class PixelSpriteAtlas
{
    private static readonly Dictionary<Texture2D, PixelSpriteAtlas> Cache = new();
    public readonly Texture2D Texture;
    public readonly SpriteCrop[] Frames;
    public readonly float RestHeight;
    public readonly bool Ready;

    private PixelSpriteAtlas(Texture2D texture)
    {
        Texture = texture;
        Color[] pixels = new Color[texture.Width * texture.Height];
        texture.GetData(pixels);
        Frames = SpriteSheetLayout.Slice(texture.Width, texture.Height, 4, 2, (x, y) => pixels[x + y * texture.Width].A);
        RestHeight = Frames.Length > 0 ? System.Math.Max(1, Frames[0].Height) : 1;
        Ready = Frames.Length == 8;
        foreach (SpriteCrop frame in Frames) Ready &= !frame.Empty;
    }

    public static PixelSpriteAtlas Request(string path)
    {
        // Async assets expose a 1x1 transparent dummy until the real PNG is
        // ready. Never slice or cache that dummy as an eight-pose atlas.
        Texture2D texture = Systems.StorageTextures.Sprite(path).Value;
        if (Cache.TryGetValue(texture, out PixelSpriteAtlas? atlas)) return atlas;
        atlas = new(texture);
        if (atlas.Ready) Cache.Add(texture, atlas);
        return atlas;
    }

    public bool TryFrame(int index, out SpriteCrop frame) => SpriteSheetLayout.TryFrame(Frames, index, out frame);
    public static Rectangle Source(SpriteCrop frame) => new(frame.X, frame.Y, frame.Width, frame.Height);
    public static Vector2 Origin(SpriteCrop frame) => new(frame.FootX, frame.Height);
    public static void Clear() => Cache.Clear();
}
