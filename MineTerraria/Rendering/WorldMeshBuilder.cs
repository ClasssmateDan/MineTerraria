using System;
using System.Collections.Generic;
using DimensionalTerraria.Core;
using DimensionalTerraria.World;
using DimensionalTerraria.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;
using Terraria.ID;
using Terraria.ModLoader;
using ReLogic.Content;
using DimensionalTerraria.Systems;

namespace DimensionalTerraria.Rendering;

internal static class WorldMeshBuilder
{
    public const int ChunkSize = 24;
    // Keep the overlay in front of a piston (.5), but behind riders (.57).
    internal const float OverlayBoatDepth = .54f;

    public static SceneChunk Build(Point key, IExplorationMap? exploration)
    {
        Dictionary<Texture2D, List<VertexPositionColorTexture>> geometry = new();
        List<VertexPositionColorTexture> liquid = new();
        int x0 = key.X * ChunkSize, y0 = key.Y * ChunkSize;
        int x1 = Math.Min(x0 + ChunkSize, Main.maxTilesX - 2), y1 = Math.Min(y0 + ChunkSize, Main.maxTilesY - 2);
        for (int x = Math.Max(2, x0); x < x1; x++)
        for (int y = Math.Max(2, y0); y < y1; y++)
        {
            if (exploration is not null && !exploration.IsExplored(x, y)) continue;
            Tile tile = Main.tile[x, y];
            BlockShape shape = TerrariaCollisionWorld.GetShape(tile);
            if (LiquidRules.Bounds(shape, tile.LiquidAmount, out float liquidTop, out float liquidBottom))
            {
                var rgba = LiquidRules.Color(tile.LiquidType);
                Color tint = Color.FromNonPremultiplied(new Vector4(rgba.X, rgba.Y, rgba.Z, rgba.W));
                float a = y + liquidTop, b = y + liquidBottom, z0 = SpatialRules.FrontMinZ, z1 = 0.60f;
                LiquidQuad(P(x, a, z1), P(x + 1, a, z1), P(x + 1, b, z1), P(x, b, z1), tint);
                if (liquidTop > 0 || !LiquidAt(x, y - 1))
                    LiquidQuad(P(x, a, z0), P(x + 1, a, z0), P(x + 1, a, z1), P(x, a, z1), tint * 1.08f);
                if (!LiquidAt(x - 1, y) && !OpaqueAt(x - 1, y))
                    LiquidQuad(P(x, a, z0), P(x, a, z1), P(x, b, z1), P(x, b, z0), tint);
                if (!LiquidAt(x + 1, y) && !OpaqueAt(x + 1, y))
                    LiquidQuad(P(x + 1, a, z1), P(x + 1, a, z0), P(x + 1, b, z0), P(x + 1, b, z1), tint);
            }
            if (tile.WallType != WallID.None)
            {
                Main.instance.LoadWall(tile.WallType);
                Texture2D wall = Main.instance.TilePaintSystem.TryGetWallAndRequestIfNotReady(tile.WallType, tile.WallColor)
                    ?? TextureAssets.Wall[tile.WallType].Value;
                Vector4 uv = Uv(wall, new Rectangle(tile.WallFrameX + 8, tile.WallFrameY + 8, 16, 16));
                Box(Vertices(wall), x, y, x + 1, y + 1, SpatialRules.WallMinZ, SpatialRules.WallMaxZ, uv, 0.67f,
                    shape != BlockShape.Solid, true, !WallAt(x - 1, y), !WallAt(x + 1, y), !WallAt(x, y - 1), !WallAt(x, y + 1));
            }
            if (!tile.HasTile || tile.IsActuated || !TileDrawing.IsVisible(tile)) continue;
            if (tile.TileType == ModContent.TileType<OakBoatTile>()) continue; // LiveBoatRenderer owns both representations.

            Main.instance.LoadTiles(tile.TileType);
            short frameX = tile.TileFrameX, frameY = tile.TileFrameY;
            Main.instance.TilesRenderer.GetTileDrawData(x, y, tile, tile.TileType, ref frameX, ref frameY,
                out int width, out int height, out int top, out int halfHeight, out int extraX, out int extraY,
                out SpriteEffects flip, out _, out _, out _);
            Texture2D texture = Main.instance.TilesRenderer.GetTileDrawTexture(tile, x, y);
            Rectangle source = new(frameX + extraX, frameY + extraY, width, height);
            if (shape == BlockShape.Empty)
            {
                Sprite(texture, source, new Vector2(x - (width - 16) / 32f, y + top / 16f), new Vector2(width, height) / 16, flip, 0.52f);
                TreeFoliage.Append(x, y, tile, (tex, src, pos, size) => Sprite(tex, src, pos, size, SpriteEffects.None, 0.54f));
                continue;
            }

            float topY = y + (shape == BlockShape.HalfBlock ? 0.5f : 0);
            float bottomY = y + (shape == BlockShape.Platform ? 0.125f : 1);
            if (shape == BlockShape.HalfBlock) { source.Y += 8; source.Height = 8; }
            if (shape == BlockShape.Platform) source.Height = 2;
            bool full = shape == BlockShape.Solid;
            if (TileLoader.GetTile(tile.TileType) is HopperTile)
            {
                Texture2D outside = ModContent.Request<Texture2D>(HopperTile.OutsideTexture, AssetRequestMode.ImmediateLoad).Value;
                Texture2D inside = ModContent.Request<Texture2D>(HopperTile.InsideTexture, AssetRequestMode.ImmediateLoad).Value;
                Texture2D rim = ModContent.Request<Texture2D>(HopperTile.TopTexture, AssetRequestMode.ImmediateLoad).Value;
                // Seven cuboids follow the original downward hopper's rim,
                // basin floor, lower body and outlet, within a single tile.
                Part(0, 5, 16, 6, 0, 16, inside);
                Part(0, 0, 2, 5, 0, 16, rim); Part(14, 0, 16, 5, 0, 16, rim);
                Part(2, 0, 14, 5, 0, 2, rim); Part(2, 0, 14, 5, 14, 16, rim);
                Part(4, 6, 12, 12, 4, 12, null); Part(6, 12, 10, 16, 6, 10, null);
                continue;

                void Part(float a, float b, float c, float d, float near, float far, Texture2D? topTexture)
                {
                    float xa = x + a / 16, xb = x + c / 16, ya = y + b / 16, yb = y + d / 16;
                    float za = near / 16 - .5f, zb = far / 16 - .5f;
                    Vector4 faceUv = new(a / 16, b / 16, c / 16, d / 16), sideUv = new(near / 16, b / 16, far / 16, d / 16);
                    Vector4 topUv = new(a / 16, near / 16, c / 16, far / 16);
                    Box(Vertices(outside), xa, ya, xb, yb, za, zb, faceUv, 1, true, true, false, false, false, false);
                    Box(Vertices(outside), xa, ya, xb, yb, za, zb, sideUv, 1, false, false, true, true, false, false);
                    Quad(Vertices(inside), P(xa, yb, zb), P(xb, yb, zb), P(xb, yb, za), P(xa, yb, za), topUv, .58f);
                    if (topTexture is not null)
                        Quad(Vertices(topTexture), P(xa, ya, za), P(xb, ya, za), P(xb, ya, zb), P(xa, ya, zb), topUv, .94f);
                }
            }
            if (TileLoader.GetTile(tile.TileType) is PistonHeadTile)
            {
                int direction = tile.TileFrameX / 18 & 3;
                Texture2D cap = ModContent.Request<Texture2D>("DimensionalTerraria/Textures/Minecraft/" +
                    (tile.TileFrameY == 18 ? "piston_top_sticky" : "piston_top"), AssetRequestMode.ImmediateLoad).Value;
                Texture2D rod = ModContent.Request<Texture2D>("DimensionalTerraria/Textures/Minecraft/piston_side", AssetRequestMode.ImmediateLoad).Value;
                (float a, float b, float c, float d) capBounds = direction switch {
                    0 => (x, y, x + 1, y + .25f), 1 => (x + .75f, y, x + 1, y + 1),
                    2 => (x, y + .75f, x + 1, y + 1), _ => (x, y, x + .25f, y + 1) };
                (float a, float b, float c, float d) rodBounds = direction switch {
                    0 => (x + .375f, y + .25f, x + .625f, y + 1), 1 => (x, y + .375f, x + .75f, y + .625f),
                    2 => (x + .375f, y, x + .625f, y + .75f), _ => (x + .25f, y + .375f, x + 1, y + .625f) };
                Vector4 uv = new(0, 0, 1, 1);
                bool horizontal = direction is 1 or 3;
                Vector4 woodUv = new(0, 0, 1, .25f);
                Box(Vertices(rod), capBounds.a, capBounds.b, capBounds.c, capBounds.d, -.5f, .5f, woodUv, 1, true, true, !horizontal, !horizontal, horizontal, horizontal);
                Texture2D rear = ModContent.Request<Texture2D>("DimensionalTerraria/Textures/Minecraft/piston_top", AssetRequestMode.ImmediateLoad).Value;
                var b = capBounds;
                if (horizontal)
                {
                    Quad(Vertices(direction == 3 ? cap : rear), P(b.a, b.b, -.5f), P(b.a, b.b, .5f), P(b.a, b.d, .5f), P(b.a, b.d, -.5f), uv, .82f);
                    Quad(Vertices(direction == 1 ? cap : rear), P(b.c, b.b, .5f), P(b.c, b.b, -.5f), P(b.c, b.d, -.5f), P(b.c, b.d, .5f), uv, .82f);
                }
                else
                {
                    Quad(Vertices(direction == 0 ? cap : rear), P(b.a, b.b, -.5f), P(b.c, b.b, -.5f), P(b.c, b.b, .5f), P(b.a, b.b, .5f), uv, .94f);
                    Quad(Vertices(direction == 2 ? cap : rear), P(b.a, b.d, .5f), P(b.c, b.d, .5f), P(b.c, b.d, -.5f), P(b.a, b.d, -.5f), uv, .58f);
                }
                Box(Vertices(rod), rodBounds.a, rodBounds.b, rodBounds.c, rodBounds.d, -.125f, .125f, woodUv, .9f, true, true, true, true, true, true);
                continue;
            }
            if (TileLoader.GetTile(tile.TileType) is PistonBaseTile)
            {
                int facing = tile.TileFrameX / 18 & 3;
                Texture2D Face(int direction)
                {
                    string name = direction == facing ? tile.TileFrameY == 18 ? "piston_inner" :
                        tile.TileType == ModContent.TileType<StickyPistonTile>() ? "piston_top_sticky" : "piston_top" :
                        direction == ((facing + 2) & 3) ? "piston_bottom" : "piston_side";
                    return ModContent.Request<Texture2D>("DimensionalTerraria/Textures/Minecraft/" + name, AssetRequestMode.ImmediateLoad).Value;
                }
                Vector4 uv = new(0, 0, 1, 1), frontUv = Uv(texture, source);
                Quad(Vertices(texture), P(x, y, .5f), P(x + 1, y, .5f), P(x + 1, y + 1, .5f), P(x, y + 1, .5f), frontUv, 1);
                if (tile.WallType == WallID.None) Quad(Vertices(texture), P(x + 1, y, -.5f), P(x, y, -.5f), P(x, y + 1, -.5f), P(x + 1, y + 1, -.5f), frontUv, .72f);
                if (!OpaqueAt(x - 1, y)) Quad(Vertices(Face(3)), P(x, y, -.5f), P(x, y, .5f), P(x, y + 1, .5f), P(x, y + 1, -.5f), uv, .82f);
                if (!OpaqueAt(x + 1, y)) Quad(Vertices(Face(1)), P(x + 1, y, .5f), P(x + 1, y, -.5f), P(x + 1, y + 1, -.5f), P(x + 1, y + 1, .5f), uv, .82f);
                if (!OpaqueAt(x, y - 1)) Quad(Vertices(Face(0)), P(x, y, -.5f), P(x + 1, y, -.5f), P(x + 1, y, .5f), P(x, y, .5f), uv, .94f);
                if (!OpaqueAt(x, y + 1)) Quad(Vertices(Face(2)), P(x, y + 1, .5f), P(x + 1, y + 1, .5f), P(x + 1, y + 1, -.5f), P(x, y + 1, -.5f), uv, .58f);
                continue;
            }
            if (TileLoader.GetTile(tile.TileType) is RedstonePulserTile)
            {
                Texture2D lamp = ModContent.Request<Texture2D>(RedstoneSystem.PulserOn(tile) ? RedstonePulserTile.OnTexture : RedstonePulserTile.OffTexture,
                    AssetRequestMode.ImmediateLoad).Value;
                Texture2D side = ModContent.Request<Texture2D>(RedstonePulserTile.SideTexture, AssetRequestMode.ImmediateLoad).Value;
                Vector4 uv = new(0, 0, 1, 1);
                Box(Vertices(lamp), x, topY, x + 1, bottomY, SpatialRules.FrontMinZ, SpatialRules.FrontMaxZ, uv, 1,
                    true, tile.WallType == WallID.None, false, false, false, false);
                Box(Vertices(side), x, topY, x + 1, bottomY, SpatialRules.FrontMinZ, SpatialRules.FrontMaxZ, uv, 1,
                    false, false, !full || !OpaqueAt(x - 1, y), !full || !OpaqueAt(x + 1, y),
                    !full || !OpaqueAt(x, y - 1), !full || !OpaqueAt(x, y + 1));
                continue;
            }
            if (TileLoader.GetTile(tile.TileType) is MinecraftCraftingTableTile)
            {
                Texture2D side = ModContent.Request<Texture2D>(MinecraftCraftingTableTile.SideTexture, AssetRequestMode.ImmediateLoad).Value;
                Texture2D topTexture = ModContent.Request<Texture2D>(MinecraftCraftingTableTile.TopTexture, AssetRequestMode.ImmediateLoad).Value;
                Texture2D bottom = ModContent.Request<Texture2D>("DimensionalTerraria/Textures/Minecraft/oak_planks", AssetRequestMode.ImmediateLoad).Value;
                Vector4 faceUv = Uv(texture, source), sideUv = Uv(side, new Rectangle(0, 0, 16, 16)), topUv = Uv(topTexture, new Rectangle(0, 0, 16, 16));
                float z0 = SpatialRules.FrontMinZ, z1 = SpatialRules.FrontMaxZ;
                Quad(Vertices(texture), P(x, topY, z1), P(x + 1, topY, z1), P(x + 1, bottomY, z1), P(x, bottomY, z1), faceUv, 1);
                if (tile.WallType == WallID.None) Quad(Vertices(texture), P(x + 1, topY, z0), P(x, topY, z0), P(x, bottomY, z0), P(x + 1, bottomY, z0), faceUv, 0.72f);
                if (!full || !OpaqueAt(x - 1, y)) Quad(Vertices(side), P(x, topY, z0), P(x, topY, z1), P(x, bottomY, z1), P(x, bottomY, z0), sideUv, 0.82f);
                if (!full || !OpaqueAt(x + 1, y)) Quad(Vertices(side), P(x + 1, topY, z1), P(x + 1, topY, z0), P(x + 1, bottomY, z0), P(x + 1, bottomY, z1), sideUv, 0.82f);
                if (!full || !OpaqueAt(x, y - 1)) Quad(Vertices(topTexture), P(x, topY, z0), P(x + 1, topY, z0), P(x + 1, topY, z1), P(x, topY, z1), topUv, 0.94f);
                if (!full || !OpaqueAt(x, y + 1)) Quad(Vertices(bottom), P(x, bottomY, z1), P(x + 1, bottomY, z1), P(x + 1, bottomY, z0), P(x, bottomY, z0), Uv(bottom, new Rectangle(0, 0, 16, 16)), 0.58f);
                continue;
            }
            Box(Vertices(texture), x, topY, x + 1, bottomY, SpatialRules.FrontMinZ, SpatialRules.FrontMaxZ, Uv(texture, source, flip), 1,
                true, tile.WallType == WallID.None, !full || !OpaqueAt(x - 1, y), !full || !OpaqueAt(x + 1, y),
                !full || !OpaqueAt(x, y - 1), !full || !OpaqueAt(x, y + 1));
        }

        if (exploration is not null)
        {
            // Merge unknown cells into row strips. The opaque volume also hides sprites at oblique angles.
            Texture2D fog = TextureAssets.MagicPixel.Value;
            Vector4 fogUv = new(0, 0, 1, 1);
            for (int y = Math.Max(2, y0); y < y1; y++)
            for (int x = Math.Max(2, x0); x < x1;)
            {
                if (exploration.IsExplored(x, y)) { x++; continue; }
                int start = x++;
                while (x < x1 && !exploration.IsExplored(x, y)) x++;
                Box(Vertices(fog), start, y, x, y + 1, SpatialRules.WallMinZ, 0.68f, fogUv, 0.035f,
                    true, true, true, true, true, true);
            }
        }

        List<MeshBatch> batches = new();
        List<MeshBatch> liquidBatches = new();
        try
        {
            foreach (var pair in geometry)
            {
                if (pair.Value.Count == 0) continue;
                VertexBuffer buffer = new(Main.instance.GraphicsDevice, typeof(VertexPositionColorTexture), pair.Value.Count, BufferUsage.WriteOnly);
                try { buffer.SetData(pair.Value.ToArray()); }
                catch { buffer.Dispose(); throw; }
                batches.Add(new MeshBatch(buffer, pair.Key, pair.Value.Count));
            }
            if (liquid.Count > 0)
            {
                VertexBuffer buffer = new(Main.instance.GraphicsDevice, typeof(VertexPositionColorTexture), liquid.Count, BufferUsage.WriteOnly);
                try { buffer.SetData(liquid.ToArray()); }
                catch { buffer.Dispose(); throw; }
                liquidBatches.Add(new(buffer, TextureAssets.MagicPixel.Value, liquid.Count));
            }
            // Crowns and branches extend several tiles outside their owner chunk.
            return new SceneChunk(batches, liquidBatches, new BoundingBox(new Vector3(x0 - 8, -y1 - 8, -1.6f), new Vector3(x1 + 8, -y0 + 8, 0.7f)), ContentStamp(key, exploration));
        }
        catch { foreach (MeshBatch batch in batches) batch.Buffer.Dispose(); foreach (MeshBatch batch in liquidBatches) batch.Buffer.Dispose(); throw; }

        void LiquidQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color tint)
        {
            VertexPositionColorTexture va = new(a, tint, Vector2.Zero), vb = new(b, tint, Vector2.UnitX);
            VertexPositionColorTexture vc = new(c, tint, Vector2.One), vd = new(d, tint, Vector2.UnitY);
            liquid.Add(va); liquid.Add(vb); liquid.Add(vc); liquid.Add(va); liquid.Add(vc); liquid.Add(vd);
        }

        List<VertexPositionColorTexture> Vertices(Texture2D texture)
        {
            if (!geometry.TryGetValue(texture, out var list)) geometry.Add(texture, list = new());
            return list;
        }

        void Sprite(Texture2D texture, Rectangle source, Vector2 position, Vector2 size, SpriteEffects flip, float z)
        {
            if (source.Width <= 0 || source.Height <= 0) return;
            // Invalid special frames are skipped rather than stretched from a clamped corner of the atlas.
            if (source.X < 0 || source.Y < 0 || source.Right > texture.Width || source.Bottom > texture.Height) return;
            Vector4 uv = Uv(texture, source, flip);
            if (exploration is null) { AddSpritePart(position, position + size, uv); return; }
            // Crowns and furniture can extend outside their owner's explored cell.
            // Clip their geometry too, including while neighboring chunks are still loading.
            for (int sx = (int)MathF.Floor(position.X); sx < MathF.Ceiling(position.X + size.X); sx++)
            for (int sy = (int)MathF.Floor(position.Y); sy < MathF.Ceiling(position.Y + size.Y); sy++)
            {
                if (!exploration.IsExplored(sx, sy)) continue;
                Vector2 a = Vector2.Max(position, new Vector2(sx, sy));
                Vector2 b = Vector2.Min(position + size, new Vector2(sx + 1, sy + 1));
                Vector2 from = (a - position) / size, to = (b - position) / size;
                Vector4 clipped = new(MathHelper.Lerp(uv.X, uv.Z, from.X), MathHelper.Lerp(uv.Y, uv.W, from.Y),
                    MathHelper.Lerp(uv.X, uv.Z, to.X), MathHelper.Lerp(uv.Y, uv.W, to.Y));
                AddSpritePart(a, b, clipped);
            }

            void AddSpritePart(Vector2 a, Vector2 b, Vector4 partUv) => Quad(Vertices(texture),
                P(a.X, a.Y, z), P(b.X, a.Y, z), P(b.X, b.Y, z), P(a.X, b.Y, z), partUv, 1);
        }

        bool WallAt(int x, int y) => WorldGen.InWorld(x, y) &&
            (exploration is not null && !exploration.IsExplored(x, y) || Main.tile[x, y].WallType != WallID.None);
        bool OpaqueAt(int x, int y) => WorldGen.InWorld(x, y) &&
            (exploration is not null && !exploration.IsExplored(x, y) ||
             TerrariaCollisionWorld.GetShape(Main.tile[x, y]) == BlockShape.Solid && Main.tileBlockLight[Main.tile[x, y].TileType]);
        bool LiquidAt(int x, int y) => exploration is not null && !exploration.IsExplored(x, y) || Main.tile[x, y].LiquidAmount > 0;
    }

    public static int ContentStamp(Point key, IExplorationMap? exploration)
    {
        HashCode stamp = new();
        // Include one neighbor cell for face culling across chunk borders.
        int x0 = Math.Max(2, key.X * ChunkSize - 1), y0 = Math.Max(2, key.Y * ChunkSize - 1);
        int x1 = Math.Min(Main.maxTilesX - 2, (key.X + 1) * ChunkSize + 1);
        int y1 = Math.Min(Main.maxTilesY - 2, (key.Y + 1) * ChunkSize + 1);
        for (int x = x0; x < x1; x++)
        for (int y = y0; y < y1; y++)
        {
            if (exploration is not null && !exploration.IsExplored(x, y)) continue;
            Tile tile = Main.tile[x, y];
            stamp.Add(tile.HasTile); stamp.Add(tile.TileType);
            stamp.Add(tile.TileFrameX); stamp.Add(tile.TileFrameY);
            stamp.Add(tile.WallType); stamp.Add(tile.WallFrameX); stamp.Add(tile.WallFrameY);
            stamp.Add(tile.IsActuated); stamp.Add(tile.IsHalfBlock); stamp.Add(tile.Slope);
            stamp.Add(tile.TileColor); stamp.Add(tile.WallColor); stamp.Add(TileDrawing.IsVisible(tile));
            stamp.Add(tile.LiquidAmount); stamp.Add(tile.LiquidType);
            if (tile.HasTile) stamp.Add(Main.tileFrame[tile.TileType]);
            if (tile.WallType != WallID.None) stamp.Add(Main.wallFrame[tile.WallType]);
        }
        return stamp.ToHashCode();
    }

    private static Vector4 Uv(Texture2D texture, Rectangle source, SpriteEffects flip = SpriteEffects.None)
    {
        int x = Math.Clamp(source.X, 0, Math.Max(0, texture.Width - 1)), y = Math.Clamp(source.Y, 0, Math.Max(0, texture.Height - 1));
        int right = Math.Clamp(source.Right, x + 1, texture.Width), bottom = Math.Clamp(source.Bottom, y + 1, texture.Height);
        float u0 = (x + 0.05f) / texture.Width, v0 = (y + 0.05f) / texture.Height;
        float u1 = (right - 0.05f) / texture.Width, v1 = (bottom - 0.05f) / texture.Height;
        if ((flip & SpriteEffects.FlipHorizontally) != 0) (u0, u1) = (u1, u0);
        if ((flip & SpriteEffects.FlipVertically) != 0) (v0, v1) = (v1, v0);
        return new Vector4(u0, v0, u1, v1);
    }

    internal static Vector3 P(float x, float y, float z) => new(x, -y, z);

    private static void Box(List<VertexPositionColorTexture> v, float x0, float y0, float x1, float y1, float z0, float z1,
        Vector4 uv, float tint, bool front, bool back, bool left, bool right, bool top, bool bottom)
    {
        if (front) Quad(v, P(x0, y0, z1), P(x1, y0, z1), P(x1, y1, z1), P(x0, y1, z1), uv, tint);
        if (back) Quad(v, P(x1, y0, z0), P(x0, y0, z0), P(x0, y1, z0), P(x1, y1, z0), uv, tint * 0.72f);
        if (left) Quad(v, P(x0, y0, z0), P(x0, y0, z1), P(x0, y1, z1), P(x0, y1, z0), uv, tint * 0.82f);
        if (right) Quad(v, P(x1, y0, z1), P(x1, y0, z0), P(x1, y1, z0), P(x1, y1, z1), uv, tint * 0.82f);
        if (top) Quad(v, P(x0, y0, z0), P(x1, y0, z0), P(x1, y0, z1), P(x0, y0, z1), uv, tint * 0.94f);
        if (bottom) Quad(v, P(x0, y1, z1), P(x1, y1, z1), P(x1, y1, z0), P(x0, y1, z0), uv, tint * 0.58f);
    }

    private static void Quad(List<VertexPositionColorTexture> list, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector4 uv, float tint)
    {
        Color color = new(tint, tint, tint, 1f);
        VertexPositionColorTexture va = new(a, color, new Vector2(uv.X, uv.Y)), vb = new(b, color, new Vector2(uv.Z, uv.Y));
        VertexPositionColorTexture vc = new(c, color, new Vector2(uv.Z, uv.W)), vd = new(d, color, new Vector2(uv.X, uv.W));
        list.Add(va); list.Add(vb); list.Add(vc); list.Add(va); list.Add(vc); list.Add(vd);
    }
}

internal sealed record MeshBatch(VertexBuffer Buffer, Texture2D Texture, int VertexCount);
internal sealed record SceneChunk(List<MeshBatch> Batches, List<MeshBatch> Liquids, BoundingBox Bounds, int ContentStamp) : IDisposable
{
    public void Dispose() { foreach (MeshBatch batch in Batches) batch.Buffer.Dispose(); foreach (MeshBatch batch in Liquids) batch.Buffer.Dispose(); }
}
