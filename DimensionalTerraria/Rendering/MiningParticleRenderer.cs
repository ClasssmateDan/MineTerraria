using System;
using System.Collections.Generic;
using DimensionalTerraria.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using NVector3 = System.Numerics.Vector3;
using DimensionalTerraria.Systems;
using DimensionalTerraria.Tiles;
using Terraria.ModLoader;

namespace DimensionalTerraria.Rendering;

internal readonly record struct MiningSurface(Texture2D Texture, Rectangle Frame);

internal sealed class MiningParticleRenderer
{
    private sealed class Chip(MiningDebris motion, Texture2D texture, Rectangle source, float size, float rotation)
    {
        public MiningDebris Motion = motion;
        public readonly Texture2D Texture = texture;
        public readonly Rectangle Source = source;
        public readonly float Size = size;
        public float Rotation = rotation;
    }

    private const int Limit = 256;
    private readonly List<Chip> chips = new();
    private readonly Random random = new();
    private readonly VertexPositionColorTexture[] vertices = new VertexPositionColorTexture[6];

    public static MiningSurface Capture(TileRayHit hit)
    {
        if (hit.Layer == RayLayer.Tile && BoatCaptureSystem.OverlayAt(hit.X, hit.Y) is { } boat)
            return new(TextureAssets.Tile[ModContent.TileType<OakBoatTile>()].Value,
                new Rectangle((hit.X - boat.Position.X) * 18, (hit.Y - boat.Position.Y) * 18, 16, 16));
        Tile tile = Main.tile[hit.X, hit.Y];
        Texture2D texture;
        Rectangle frame;
        if (hit.Layer == RayLayer.Wall)
        {
            Main.instance.LoadWall(tile.WallType);
            texture = TextureAssets.Wall[tile.WallType].Value;
            frame = new Rectangle(tile.WallFrameX + 8, tile.WallFrameY + 8, 16, 16);
        }
        else
        {
            Main.instance.LoadTiles(tile.TileType);
            short x = tile.TileFrameX, y = tile.TileFrameY;
            Main.instance.TilesRenderer.GetTileDrawData(hit.X, hit.Y, tile, tile.TileType, ref x, ref y,
                out int width, out int height, out _, out _, out int extraX, out int extraY, out _, out _, out _, out _);
            texture = Main.instance.TilesRenderer.GetTileDrawTexture(tile, hit.X, hit.Y);
            frame = new Rectangle(x + extraX, y + extraY, width, height);
        }
        frame = Rectangle.Intersect(frame, texture.Bounds);
        return new MiningSurface(texture, frame);
    }

    public void Emit(MiningSurface surface, NVector3 position, bool broken)
    {
        if (surface.Frame.Width < 2 || surface.Frame.Height < 2) return;
        int count = broken ? 18 : 6;
        if (chips.Count + count > Limit) chips.RemoveRange(0, Math.Min(chips.Count, chips.Count + count - Limit));
        for (int i = 0; i < count; i++)
        {
            Rectangle source = new(surface.Frame.X + random.Next(surface.Frame.Width - 1), surface.Frame.Y + random.Next(surface.Frame.Height - 1), 2, 2);
            NVector3 velocity = new((Next() - 0.5f) * 3, -0.8f - Next() * 2.2f, 0.4f + Next() * 1.2f);
            MiningDebris motion = new(position + new NVector3((Next() - 0.5f) * 0.25f, (Next() - 0.5f) * 0.25f, 0.04f), velocity, 0.40f + Next() * 0.35f);
            chips.Add(new Chip(motion, surface.Texture, source, 0.10f + Next() * 0.08f, Next() * MathF.Tau));
        }
    }

    public void Update(float seconds)
    {
        for (int i = chips.Count - 1; i >= 0; i--)
        {
            Chip chip = chips[i];
            chip.Motion.Update(seconds);
            chip.Rotation += seconds * 3;
            if (!chip.Motion.Active) chips.RemoveAt(i);
        }
    }

    public void Draw(GraphicsDevice device, AlphaTestEffect effect)
    {
        if (chips.Count == 0) return;
        device.SetVertexBuffer(null);
        device.BlendState = BlendState.AlphaBlend;
        device.DepthStencilState = DepthStencilState.DepthRead;
        bool fog = effect.FogEnabled;
        int alpha = effect.ReferenceAlpha;
        effect.FogEnabled = false;
        effect.ReferenceAlpha = 1;
        Vector3 right = new(effect.View.M11, effect.View.M21, effect.View.M31);
        Vector3 up = new(effect.View.M12, effect.View.M22, effect.View.M32);
        try
        {
            foreach (Chip chip in chips)
            {
                Vector3 center = WorldMeshBuilder.P(chip.Motion.Position.X, chip.Motion.Position.Y, chip.Motion.Position.Z);
                Color color = Color.White * chip.Motion.Fade;
                float sin = MathF.Sin(chip.Rotation), cos = MathF.Cos(chip.Rotation);
                float u0 = (chip.Source.Left + 0.01f) / chip.Texture.Width, u1 = (chip.Source.Right - 0.01f) / chip.Texture.Width;
                float v0 = (chip.Source.Top + 0.01f) / chip.Texture.Height, v1 = (chip.Source.Bottom - 0.01f) / chip.Texture.Height;
                VertexPositionColorTexture a = Vertex(-0.5f, 0.5f, u0, v0), b = Vertex(0.5f, 0.5f, u1, v0);
                VertexPositionColorTexture c = Vertex(0.5f, -0.5f, u1, v1), d = Vertex(-0.5f, -0.5f, u0, v1);
                vertices[0] = a; vertices[1] = b; vertices[2] = c;
                vertices[3] = a; vertices[4] = c; vertices[5] = d;
                effect.Texture = chip.Texture;
                foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    device.SamplerStates[0] = SamplerState.PointClamp;
                    device.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, 2);
                }

                VertexPositionColorTexture Vertex(float x, float y, float u, float v) =>
                    new(center + right * ((x * cos - y * sin) * chip.Size) + up * ((x * sin + y * cos) * chip.Size), color, new(u, v));
            }
        }
        finally
        {
            effect.FogEnabled = fog;
            effect.ReferenceAlpha = alpha;
            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;
        }
    }

    public void Reset() => chips.Clear();
    private float Next() => (float)random.NextDouble();
}
