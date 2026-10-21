using System;
using System.Collections.Generic;
using System.Linq;
using DimensionalTerraria.Configuration;
using DimensionalTerraria.Core;
using DimensionalTerraria.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace DimensionalTerraria.Rendering;

internal sealed class DimensionRenderer : IDisposable
{
    private readonly Dictionary<Point, SceneChunk> chunks = new();
    private readonly AlphaTestEffect effect;
    private readonly AlphaTestEffect heldItem;
    private readonly BasicEffect lines;
    private readonly CreatureRenderer creatures = new();
    private readonly LiveBoatRenderer boats = new();
    private readonly DroppedItemRenderer drops = new();
    private readonly MiningParticleRenderer miningParticles = new();
    private readonly CombatTraceRenderer combatTraces = new();
    private readonly SwordEffectRenderer swordEffects = new();
    private readonly ProjectedBackground background = new();
    private readonly HashSet<Point> dirtyChunks = new();
    private readonly TerrariaCollisionWorld interactionWorld = new();
    private RenderTarget2D? target;
    private int targetSamples;
    private int refreshIndex;
    private bool disposed;

    public DimensionRenderer()
    {
        GraphicsDevice device = Main.instance.GraphicsDevice;
        effect = new AlphaTestEffect(device) { VertexColorEnabled = true, AlphaFunction = CompareFunction.GreaterEqual, ReferenceAlpha = 96 };
        heldItem = new AlphaTestEffect(device) { VertexColorEnabled = true, AlphaFunction = CompareFunction.GreaterEqual, ReferenceAlpha = 32 };
        lines = new BasicEffect(device) { VertexColorEnabled = true, TextureEnabled = false };
    }

    public void Draw(FlightState flight, ReturnFailure status, FirstPersonView view, IExplorationMap? exploration)
    {
        GraphicsDevice device = Main.instance.GraphicsDevice;
        DimensionConfig config = ModContent.GetInstance<DimensionConfig>();
        Main.spriteBatch.End();
        RenderTargetBinding[] oldTargets = device.GetRenderTargets();
        Viewport oldViewport = device.Viewport;
        Rectangle oldScissor = device.ScissorRectangle;
        BlendState oldBlend = device.BlendState;
        DepthStencilState oldDepth = device.DepthStencilState;
        RasterizerState oldRasterizer = device.RasterizerState;
        SamplerState oldSampler = device.SamplerStates[0];
        try
        {
            EnsureTarget(device, oldViewport.Width, oldViewport.Height, config.SmoothEdges ? 4 : 0);
            UpdateChunks(flight, config.RenderDistance, exploration);
            device.SetRenderTarget(target);
            SkyPalette palette = SkyPalette.Sample(Main.dayTime, Main.time);
            Color sky = new(palette.Horizon.X, palette.Horizon.Y, palette.Horizon.Z);
            device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer | ClearOptions.Stencil, sky, 1, 0);
            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState = RasterizerState.CullNone;
            background.DrawSky(device, lines, palette);
            device.DepthStencilState = DepthStencilState.Default;

            Vector3 eye = WorldMeshBuilder.P(flight.Eye.X, flight.Eye.Y, flight.Eye.Z);
            Vector3 forward = WorldMeshBuilder.P(flight.Forward.X, flight.Forward.Y, flight.Forward.Z);
            effect.World = Matrix.Identity;
            effect.View = Matrix.CreateLookAt(eye, eye + forward, Vector3.Up);
            effect.Projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(config.FieldOfView), target!.Width / (float)target.Height, 0.05f, 420f);
            effect.FogEnabled = true;
            effect.FogColor = sky.ToVector3();
            // Render distance describes the radius along the sheet. Pulling back must
            // not put the entire world beyond the distance fog, especially at low settings.
            float radius = config.RenderDistance * WorldMeshBuilder.ChunkSize;
            effect.FogStart = new Vector2(flight.Position.Z, radius * 0.65f).Length();
            effect.FogEnd = new Vector2(flight.Position.Z, radius * 1.15f).Length();
            background.MaskLoadedChunks(device, lines, effect.View, effect.Projection, chunks.Keys);
            background.DrawMapSide(device, effect, flight, palette);
            BoundingFrustum frustum = new(effect.View * effect.Projection);
            foreach (SceneChunk chunk in chunks.Values)
            {
                if (!frustum.Intersects(chunk.Bounds)) continue;
                foreach (MeshBatch batch in chunk.Batches)
                {
                    effect.Texture = batch.Texture;
                    device.SetVertexBuffer(batch.Buffer);
                    foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        device.SamplerStates[0] = SamplerState.PointClamp;
                        device.DrawPrimitives(PrimitiveType.TriangleList, 0, batch.VertexCount / 3);
                    }
                }
            }
            boats.Draw(device, effect, flight, config.RenderDistance * WorldMeshBuilder.ChunkSize, exploration);
            if (config.ShowCreatures) creatures.Draw(device, effect, flight, config.RenderDistance * WorldMeshBuilder.ChunkSize, exploration);
            drops.Draw(device, effect, flight, config.RenderDistance * WorldMeshBuilder.ChunkSize, exploration);
            DrawLiquids(device, frustum, eye);
            miningParticles.Draw(device, effect);
            combatTraces.Draw(device, lines, effect.View, effect.Projection);
            swordEffects.Draw(device, lines, effect, view.SwordFlights, flight);
            if (config.ShowLandingOutline) DrawLandingMarker(device, flight, status);
            if (!view.Paused && !view.InventoryOpen && view.Target is { Layer: not RayLayer.Unexplored } hit)
                DrawTarget(device, hit, view.TargetReady);
            if (!view.Paused && !view.InventoryOpen)
                FirstPersonArm.Draw(device, lines, heldItem, effect.Projection, Main.LocalPlayer, flight.Elapsed, view.Moving, view.Swing.Amount);
        }
        finally
        {
            device.SetVertexBuffer(null);
            if (oldTargets.Length == 0) device.SetRenderTarget(null); else device.SetRenderTargets(oldTargets);
            device.Viewport = oldViewport;
            device.ScissorRectangle = oldScissor;
            device.BlendState = oldBlend;
            device.DepthStencilState = oldDepth;
            device.RasterizerState = oldRasterizer;
            device.SamplerStates[0] = oldSampler;
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, Main.Rasterizer, null, Matrix.Identity);
        }
        // Composite in physical pixels. UI zoom is applied only to the following HUD pass.
        Main.spriteBatch.Draw(target!, new Rectangle(0, 0, oldViewport.Width, oldViewport.Height), Color.White * flight.Fade);
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
        if (config.ShowCreatures) CreatureStatusHud.Draw(creatures.Anchors, view, flight, interactionWorld, exploration, effect.View * effect.Projection);
        FirstPersonHud.Draw(flight, status, view);
    }

    private void EnsureTarget(GraphicsDevice device, int width, int height, int samples)
    {
        if (target is not null && target.Width == width && target.Height == height && targetSamples == samples) return;
        target?.Dispose();
        targetSamples = samples;
        target = new RenderTarget2D(device, width, height, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8, samples, RenderTargetUsage.DiscardContents);
    }

    private void DrawLiquids(GraphicsDevice device, BoundingFrustum frustum, Vector3 eye)
    {
        int oldAlpha = effect.ReferenceAlpha;
        bool oldFog = effect.FogEnabled;
        device.BlendState = BlendState.AlphaBlend;
        device.DepthStencilState = DepthStencilState.DepthRead;
        effect.ReferenceAlpha = 1;
        effect.FogEnabled = false;
        try
        {
            foreach (SceneChunk chunk in chunks.Values.Where(c => frustum.Intersects(c.Bounds))
                .OrderByDescending(c => Vector3.DistanceSquared((c.Bounds.Min + c.Bounds.Max) / 2, eye)))
            foreach (MeshBatch batch in chunk.Liquids)
            {
                effect.Texture = batch.Texture;
                device.SetVertexBuffer(batch.Buffer);
                foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply(); device.SamplerStates[0] = SamplerState.PointClamp;
                    device.DrawPrimitives(PrimitiveType.TriangleList, 0, batch.VertexCount / 3);
                }
            }
        }
        finally
        {
            effect.ReferenceAlpha = oldAlpha; effect.FogEnabled = oldFog;
            device.BlendState = BlendState.Opaque; device.DepthStencilState = DepthStencilState.Default;
        }
    }

    private void UpdateChunks(FlightState flight, int radius, IExplorationMap? exploration)
    {
        foreach (Point key in dirtyChunks.Take(3).ToArray())
        {
            dirtyChunks.Remove(key);
            if (!chunks.TryGetValue(key, out SceneChunk? old)) continue;
            SceneChunk replacement = WorldMeshBuilder.Build(key, exploration);
            chunks[key] = replacement;
            old.Dispose();
        }
        Point center = new((int)(flight.Position.X / WorldMeshBuilder.ChunkSize), (int)(flight.Position.Y / WorldMeshBuilder.ChunkSize));
        foreach (Point key in chunks.Keys.ToArray())
        {
            if (Math.Abs(key.X - center.X) <= radius && Math.Abs(key.Y - center.Y) <= radius) continue;
            chunks[key].Dispose(); chunks.Remove(key);
        }
        List<Point> missing = new();
        for (int x = center.X - radius; x <= center.X + radius; x++)
        for (int y = center.Y - radius; y <= center.Y + radius; y++)
        {
            Point key = new(x, y);
            if (x >= 0 && y >= 0 && x * WorldMeshBuilder.ChunkSize < Main.maxTilesX - 2 &&
                y * WorldMeshBuilder.ChunkSize < Main.maxTilesY - 2 && !chunks.ContainsKey(key)) missing.Add(key);
        }
        foreach (Point key in missing.OrderBy(p => (p.X - center.X) * (p.X - center.X) + (p.Y - center.Y) * (p.Y - center.Y)).Take(3))
            chunks.Add(key, WorldMeshBuilder.Build(key, exploration));

        Point[] cached = chunks.Keys.ToArray();
        for (int i = 0; i < Math.Min(3, cached.Length); i++)
        {
            if (refreshIndex >= cached.Length) refreshIndex = 0;
            Point key = cached[refreshIndex++];
            SceneChunk old = chunks[key];
            if (old.ContentStamp == WorldMeshBuilder.ContentStamp(key, exploration)) continue;
            SceneChunk replacement = WorldMeshBuilder.Build(key, exploration);
            chunks[key] = replacement;
            old.Dispose();
        }
    }

    private void DrawLandingMarker(GraphicsDevice device, FlightState flight, ReturnFailure status)
    {
        Color color = status == ReturnFailure.None ? Color.LightGreen : Color.Salmon;
        float x = flight.Position.X, y = flight.Position.Y, hw = flight.BodySize.X / 2, hh = flight.BodySize.Y / 2;
        Vector3 a = WorldMeshBuilder.P(x - hw, y - hh, 0.56f), b = WorldMeshBuilder.P(x + hw, y - hh, 0.56f);
        Vector3 c = WorldMeshBuilder.P(x + hw, y + hh, 0.56f), d = WorldMeshBuilder.P(x - hw, y + hh, 0.56f);
        VertexPositionColor[] vertices = { new(a, color), new(b, color), new(b, color), new(c, color), new(c, color), new(d, color), new(d, color), new(a, color) };
        device.DepthStencilState = DepthStencilState.None;
        lines.World = Matrix.Identity; lines.View = effect.View; lines.Projection = effect.Projection;
        foreach (EffectPass pass in lines.CurrentTechnique.Passes) { pass.Apply(); device.DrawUserPrimitives(PrimitiveType.LineList, vertices, 0, 4); }
    }

    public void InvalidateTile(int x, int y)
    {
        for (int cx = (x - 1) / WorldMeshBuilder.ChunkSize; cx <= (x + 1) / WorldMeshBuilder.ChunkSize; cx++)
        for (int cy = (y - 1) / WorldMeshBuilder.ChunkSize; cy <= (y + 1) / WorldMeshBuilder.ChunkSize; cy++) dirtyChunks.Add(new(cx, cy));
    }

    private void DrawTarget(GraphicsDevice device, TileRayHit hit, bool ready)
    {
        Vector3 min = new(hit.Min.X - 0.008f, hit.Min.Y - 0.008f, hit.Min.Z - 0.008f);
        Vector3 max = new(hit.Max.X + 0.008f, hit.Max.Y + 0.008f, hit.Max.Z + 0.008f);
        Vector3[] corners = {
            WorldMeshBuilder.P(min.X, min.Y, min.Z), WorldMeshBuilder.P(max.X, min.Y, min.Z),
            WorldMeshBuilder.P(max.X, max.Y, min.Z), WorldMeshBuilder.P(min.X, max.Y, min.Z),
            WorldMeshBuilder.P(min.X, min.Y, max.Z), WorldMeshBuilder.P(max.X, min.Y, max.Z),
            WorldMeshBuilder.P(max.X, max.Y, max.Z), WorldMeshBuilder.P(min.X, max.Y, max.Z) };
        int[] edges = { 0,1,1,2,2,3,3,0,4,5,5,6,6,7,7,4,0,4,1,5,2,6,3,7 };
        VertexPositionColor[] vertices = new VertexPositionColor[edges.Length];
        for (int i = 0; i < edges.Length; i++) vertices[i] = new(corners[edges[i]], ready ? Color.LightYellow : Color.Salmon);
        lines.World = Matrix.Identity; lines.View = effect.View; lines.Projection = effect.Projection;
        device.DepthStencilState = DepthStencilState.DepthRead;
        foreach (EffectPass pass in lines.CurrentTechnique.Passes) { pass.Apply(); device.DrawUserPrimitives(PrimitiveType.LineList, vertices, 0, 12); }
    }

    public void EmitMiningParticles(MiningSurface surface, System.Numerics.Vector3 position, bool broken) => miningParticles.Emit(surface, position, broken);
    public void UpdateMiningParticles(float seconds) => miningParticles.Update(seconds);
    public void EmitCombatTrace(System.Numerics.Vector3 start, System.Numerics.Vector3 end, WeaponMode mode, bool hit) => combatTraces.Emit(start, end, mode, hit);
    public void UpdateCombatTraces(float seconds) => combatTraces.Update(seconds);

    public void Reset() { foreach (SceneChunk chunk in chunks.Values) chunk.Dispose(); chunks.Clear(); dirtyChunks.Clear(); miningParticles.Reset(); combatTraces.Reset(); background.Reset(); refreshIndex = 0; }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; Reset(); target?.Dispose(); effect.Dispose(); heldItem.Dispose(); lines.Dispose(); background.Dispose();
    }
}
