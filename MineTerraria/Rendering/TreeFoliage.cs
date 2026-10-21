using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;

namespace DimensionalTerraria.Rendering;

// Resolve Terraria's separate crown/branch sheets instead of treating them as 16px trunk tiles.
internal static class TreeFoliage
{
    internal delegate void AddSprite(Texture2D texture, Rectangle source, Vector2 topLeft, Vector2 size);

    public static void Append(int x, int y, Tile tile, AddSprite add)
    {
        if (tile.TileType == TileID.PalmTree) { AppendPalm(x, y, tile, add); return; }
        if (tile.TileFrameY < 198 || tile.TileFrameX is not (22 or 44 or 66)) return;

        WorldGen.GetTreeFoliageDataMethod? resolve = tile.TileType switch
        {
            TileID.Trees => WorldGen.GetCommonTreeFoliageData,
            >= TileID.TreeAmethyst and <= TileID.TreeAmber => WorldGen.GetGemTreeFoliageData,
            TileID.VanityTreeSakura or TileID.VanityTreeYellowWillow => WorldGen.GetVanityTreeFoliageData,
            TileID.TreeAsh => WorldGen.GetAshTreeFoliageData,
            _ => null,
        };
        if (resolve is null) return;
        int frame = WorldGen.GetTreeFrame(tile), style = 0;
        int offset = tile.TileFrameX == 44 ? 1 : tile.TileFrameX == 66 ? -1 : 0;
        if (!resolve(x, y, offset, ref frame, ref style, out _, out int width, out int height)) return;
        bool crown = tile.TileFrameX == 22;
        Texture2D? texture = crown
            ? Main.instance.TilePaintSystem.TryGetTreeTopAndRequestIfNotReady(style, 0, tile.TileColor)
            : Main.instance.TilePaintSystem.TryGetTreeBranchAndRequestIfNotReady(style, 0, tile.TileColor);
        if (texture is null)
        {
            int index = Math.Clamp(style, 0, TextureAssets.TreeTop.Length - 1);
            texture = (crown ? TextureAssets.TreeTop[index] : TextureAssets.TreeBranch[index]).Value;
        }
        if (crown)
        {
            add(texture, new Rectangle(frame * (width + 2), 0, width, height),
                new Vector2(x + 0.5f - width / 32f, y + 1 - height / 16f), new Vector2(width, height) / 16);
        }
        else
        {
            bool left = tile.TileFrameX == 44;
            add(texture, new Rectangle(left ? 0 : 42, frame * 42, 40, 40),
                new Vector2(x + (left ? -1.5f : 0), y - 0.75f), new Vector2(2.5f));
        }
    }

    private static void AppendPalm(int x, int y, Tile tile, AddSprite add)
    {
        if (tile.TileFrameX is not (88 or 110 or 132)) return;
        int floor = y;
        while (floor < Main.maxTilesY - 2 && Main.tile[x, floor].HasTile && Main.tile[x, floor].TileType == TileID.PalmTree) floor++;
        int variant = Main.tile[x, floor].TileType switch
        {
            TileID.Crimsand => 1, TileID.Pearlsand => 2, TileID.Ebonsand => 3, _ => 0,
        };
        bool oasis = WorldGen.IsPalmOasisTree(x);
        int style = oasis ? 21 : 15;
        int width = oasis ? 114 : 80, height = oasis ? 98 : 80;
        int frame = (tile.TileFrameX - 88) / 22;
        Texture2D texture = Main.instance.TilePaintSystem.TryGetTreeTopAndRequestIfNotReady(style, variant + (oasis ? 4 : 0), tile.TileColor)
            ?? TextureAssets.TreeTop[style].Value;
        add(texture, new Rectangle(frame * (width + 2), variant * (oasis ? 98 : 82), width, height),
            new Vector2(x + (tile.TileFrameY - (oasis ? 48 : 32)) / 16f, y + 1 + (oasis ? 0.125f : 0) - height / 16f),
            new Vector2(width, height) / 16);
    }
}
