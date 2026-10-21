using System;
using System.Collections.Generic;
using DimensionalTerraria.Core;
using DimensionalTerraria.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;

namespace DimensionalTerraria.Rendering;

internal sealed class ProjectedBackground : IDisposable
{
    private const float BackgroundGap = 0.35f;
    private readonly TerrariaEnvironment world = new();
    private readonly Dictionary<int, Texture2D> textures = new();
    private readonly List<VertexPositionColorTexture> plane = new();
    private readonly VertexPositionColor[] skyQuad = new VertexPositionColor[6];
    private ProjectedEnvironment environment;
    private float sampledAt = -1;
    private Vector2 sampledPosition;
    private VertexPositionColorTexture[]? caveVertices;
    private (Texture2D Texture, float Left, float Top, float Right, float Bottom, Color Tint) caveKey;
    private readonly DepthStencilState writeMask = new() { DepthBufferEnable = false, StencilEnable = true,
        StencilFunction = CompareFunction.Always, StencilPass = StencilOperation.Replace, ReferenceStencil = 1 };
    private readonly DepthStencilState insideMask = new() { DepthBufferEnable = true, DepthBufferWriteEnable = false,
        StencilEnable = true, StencilFunction = CompareFunction.Equal, StencilPass = StencilOperation.Keep, ReferenceStencil = 1 };
    private readonly BlendState noColor = new() { ColorWriteChannels = ColorWriteChannels.None };

    public void MaskLoadedChunks(GraphicsDevice device, BasicEffect effect, Matrix view, Matrix projection, IEnumerable<Point> loaded)
    {
        device.SetVertexBuffer(null);
        device.DepthStencilState = writeMask;
        device.BlendState = noColor;
        effect.World = Matrix.Identity; effect.View = view; effect.Projection = projection;
        effect.TextureEnabled = false;
        foreach (Point chunk in loaded)
        {
            float left = Math.Max(2, chunk.X * WorldMeshBuilder.ChunkSize), top = Math.Max(2, chunk.Y * WorldMeshBuilder.ChunkSize);
            float right = Math.Min(Main.maxTilesX - 2, (chunk.X + 1) * WorldMeshBuilder.ChunkSize);
            float bottom = Math.Min(Main.maxTilesY - 2, (chunk.Y + 1) * WorldMeshBuilder.ChunkSize);
            skyQuad[0] = new(WorldMeshBuilder.P(left, top, SpatialRules.FrontMaxZ), Color.White);
            skyQuad[1] = new(WorldMeshBuilder.P(right, top, SpatialRules.FrontMaxZ), Color.White);
            skyQuad[2] = new(WorldMeshBuilder.P(right, bottom, SpatialRules.FrontMaxZ), Color.White);
            skyQuad[3] = skyQuad[0]; skyQuad[4] = skyQuad[2];
            skyQuad[5] = new(WorldMeshBuilder.P(left, bottom, SpatialRules.FrontMaxZ), Color.White);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) { pass.Apply(); device.DrawUserPrimitives(PrimitiveType.TriangleList, skyQuad, 0, 2); }
        }
        device.DepthStencilState = DepthStencilState.Default;
        device.BlendState = BlendState.Opaque;
    }

    public void DrawSky(GraphicsDevice device, BasicEffect effect, SkyPalette sky)
    {
        device.SetVertexBuffer(null);
        device.DepthStencilState = DepthStencilState.None;
        device.BlendState = BlendState.Opaque;
        effect.World = effect.View = Matrix.Identity;
        effect.Projection = Matrix.CreateOrthographicOffCenter(0, 1, 1, 0, 0, 1);
        Color top = new(sky.Top.X, sky.Top.Y, sky.Top.Z), bottom = new(sky.Horizon.X, sky.Horizon.Y, sky.Horizon.Z);
        skyQuad[0] = new(new(0, 0, 0), top); skyQuad[1] = new(new(1, 0, 0), top); skyQuad[2] = new(new(1, 1, 0), bottom);
        skyQuad[3] = skyQuad[0]; skyQuad[4] = skyQuad[2]; skyQuad[5] = new(new(0, 1, 0), bottom);
        foreach (EffectPass pass in effect.CurrentTechnique.Passes) { pass.Apply(); device.DrawUserPrimitives(PrimitiveType.TriangleList, skyQuad, 0, 2); }
    }

    public void DrawMapSide(GraphicsDevice device, AlphaTestEffect effect, FlightState flight, SkyPalette sky)
    {
        Vector2 position = new(flight.Position.X, flight.Position.Y);
        if (sampledAt < 0 || flight.Elapsed - sampledAt >= 0.4f || Vector2.DistanceSquared(sampledPosition, position) > 64)
        {
            environment = ProjectedEnvironment.Sample(world, new(position.X, position.Y), (float)Main.worldSurface, (float)Main.rockLayer);
            sampledAt = flight.Elapsed;
            sampledPosition = position;
        }
        if (environment.Biome == ProjectedBiome.Unknown) return;
        device.SetVertexBuffer(null);
        device.DepthStencilState = insideMask;
        device.BlendState = BlendState.AlphaBlend;
        bool oldFog = effect.FogEnabled;
        effect.FogEnabled = false;
        try
        {
            if (environment.Depth is ProjectedDepth.Surface or ProjectedDepth.Sky)
            {
                int[] layers = SurfaceLayers(environment.Biome, position.X);
                Color light = Color.Lerp(new Color(55, 63, 92), Color.White, MathHelper.Clamp(sky.Horizon.Y, 0, 1));
                BackdropCamera camera = SurfaceBackdrop.Camera(position.Y, (float)Main.worldSurface, Main.maxTilesY, device.Viewport.Height);
                for (int i = 0; i < layers.Length; i++)
                {
                    Texture2D? texture = Texture(layers[i]);
                    if (texture is null) continue;
                    var layout = NativeBackgroundLayout.Sample(Main.instance, layers[i], i, environment.Biome, camera);
                    float height = SurfaceBackdrop.Height(texture.Height, layout.Scale);
                    float gap = BackgroundGap + (1.4f - BackgroundGap) * (layers.Length - 1 - i) / Math.Max(1, layers.Length - 1);
                    DrawPlane(texture, position.X - 400, layout.Top, position.X + 400, Math.Max(layout.Top + height, position.Y + 250),
                        SpatialRules.WallMinZ - gap, height * texture.Width / texture.Height, height, light, false);
                }
            }
            else
            {
                bool cave = environment.Depth == ProjectedDepth.Cavern;
                int id = environment.Depth == ProjectedDepth.Underworld ? 6 : environment.Biome switch
                {
                    ProjectedBiome.Snow => cave ? 32 : 33,
                    ProjectedBiome.Jungle => cave ? 149 : 147,
                    _ => cave ? 3 : 2
                };
                Texture2D? texture = Texture(id);
                if (texture is not null)
                    DrawPlane(texture, MathF.Floor(position.X / 64) * 64 - 350, MathF.Floor(position.Y / 64) * 64 - 250,
                        MathF.Floor(position.X / 64) * 64 + 350, MathF.Floor(position.Y / 64) * 64 + 250,
                        SpatialRules.WallMinZ - BackgroundGap, Math.Max(1, texture.Width - 32) / 16f, texture.Height / 16f, CaveTint(environment.Biome), true);
            }
        }
        finally { effect.FogEnabled = oldFog; device.DepthStencilState = DepthStencilState.Default; device.BlendState = BlendState.Opaque; }

        void DrawPlane(Texture2D texture, float left, float top, float right, float bottom, float z, float repeatX, float repeatY, Color color, bool cropEdges)
        {
            // Actual quads sit behind the sheet. They cannot appear in the empty
            // hemisphere when the camera turns away, unlike a surrounding skybox.
            float originalTop = top;
            left = Math.Max(10, left); right = Math.Min(Main.maxTilesX - 10, right);
            top = Math.Max(10, top); bottom = Math.Min(Main.maxTilesY - 10, bottom);
            if (left >= right || top >= bottom) return;
            float originX = MathF.Floor(left / repeatX) * repeatX;
            float originY = cropEdges ? MathF.Floor(top / repeatY) * repeatY : originalTop;
            float inset = cropEdges ? 16f / texture.Width : 0;
            var key = (texture, left, top, right, bottom, color);
            if (cropEdges && caveVertices is not null && caveKey == key) { DrawVertices(caveVertices); return; }
            plane.Clear();
            for (float x = originX; x < right; x += repeatX)
            for (float y = originY; y < bottom; y += cropEdges ? repeatY : bottom - originY)
            {
                float x0 = Math.Max(x, left), x1 = Math.Min(x + repeatX, right);
                float y0 = Math.Max(y, top), y1 = cropEdges ? Math.Min(y + repeatY, bottom) : bottom;
                float u0 = inset + (x0 - x) / repeatX * (1 - inset * 2), u1 = inset + (x1 - x) / repeatX * (1 - inset * 2);
                float v0 = (y0 - y) / repeatY, v1 = (y1 - y) / repeatY;
                VertexPositionColorTexture a = new(WorldMeshBuilder.P(x0, y0, z), color, new(u0, v0));
                VertexPositionColorTexture b = new(WorldMeshBuilder.P(x1, y0, z), color, new(u1, v0));
                VertexPositionColorTexture c = new(WorldMeshBuilder.P(x1, y1, z), color, new(u1, v1));
                VertexPositionColorTexture d = new(WorldMeshBuilder.P(x0, y1, z), color, new(u0, v1));
                plane.Add(a); plane.Add(b); plane.Add(c); plane.Add(a); plane.Add(c); plane.Add(d);
            }
            VertexPositionColorTexture[] vertices = plane.ToArray();
            if (cropEdges) { caveVertices = vertices; caveKey = key; }
            DrawVertices(vertices);

            void DrawVertices(VertexPositionColorTexture[] data)
            {
                effect.Texture = texture;
                foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply(); device.SamplerStates[0] = SamplerState.PointClamp;
                    device.DrawUserPrimitives(PrimitiveType.TriangleList, data, 0, data.Length / 3);
                }
            }
        }
    }

    private Texture2D? Texture(int id)
    {
        if (id < 0 || id >= TextureAssets.Background.Length) return null;
        if (!textures.TryGetValue(id, out Texture2D? texture))
            textures[id] = texture = Main.Assets.Request<Texture2D>(TextureAssets.Background[id].Name, AssetRequestMode.ImmediateLoad).Value;
        return texture;
    }

    private static int[] SurfaceLayers(ProjectedBiome biome, float x) => biome switch
    {
        ProjectedBiome.Desert => Main.desertBG,
        ProjectedBiome.Snow => Main.snowBG,
        ProjectedBiome.Jungle => Main.jungleBG,
        ProjectedBiome.Corruption => Main.corruptBG,
        ProjectedBiome.Crimson => Main.crimsonBG,
        ProjectedBiome.Hallow => Main.hallowBG,
        ProjectedBiome.Mushroom => Main.mushroomBG,
        ProjectedBiome.Ocean => new[] { Main.oceanBG },
        _ => x < Main.maxTilesX * 0.25f ? Main.treeBGSet1 : x < Main.maxTilesX * 0.5f ? Main.treeBGSet2
            : x < Main.maxTilesX * 0.75f ? Main.treeBGSet3 : Main.treeBGSet4
    };

    private static Color CaveTint(ProjectedBiome biome) => biome switch
    {
        ProjectedBiome.Corruption => new(105, 80, 140), ProjectedBiome.Crimson => new(150, 70, 65),
        ProjectedBiome.Hallow => new(155, 120, 175), ProjectedBiome.Mushroom => new(65, 85, 165),
        ProjectedBiome.Desert => new(185, 140, 95), ProjectedBiome.Dungeon => new(85, 105, 125),
        _ => new(155, 155, 155)
    };

    public void Reset() { sampledAt = -1; textures.Clear(); caveVertices = null; }
    public void Dispose() { Reset(); writeMask.Dispose(); insideMask.Dispose(); noColor.Dispose(); }
}
