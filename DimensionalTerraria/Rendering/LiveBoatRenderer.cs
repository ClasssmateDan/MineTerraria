using System;
using System.Collections.Generic;
using DimensionalTerraria.Core;
using DimensionalTerraria.Systems;
using DimensionalTerraria.Tiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace DimensionalTerraria.Rendering;

internal sealed class LiveBoatRenderer
{
    private readonly List<VertexPositionColorTexture> vertices = new();

    internal void Draw(GraphicsDevice device, AlphaTestEffect effect, FlightState flight, float radius, IExplorationMap? exploration)
    {
        vertices.Clear();
        Texture2D texture = TextureAssets.Tile[ModContent.TileType<OakBoatTile>()].Value;
        foreach (OakBoatEntity boat in BoatCaptureSystem.RenderBoats())
        {
            if (Math.Abs(boat.Position.X - flight.Position.X) > radius || Math.Abs(boat.Position.Y - flight.Position.Y) > radius) continue;
            float top = boat.DrawSurface - 2;
            float distance = System.Numerics.Vector3.Distance(flight.Eye, new(boat.Position.X + 1.5f, top + 1, WorldMeshBuilder.OverlayBoatDepth));
            float fade = effect.FogEnabled ? 1 - MathHelper.SmoothStep(0, 1,
                MathHelper.Clamp((distance - effect.FogStart) / Math.Max(1, effect.FogEnd - effect.FogStart), 0, 1)) : 1;
            Color color = Color.White * fade;
            for (int dx = 0; dx < 3; dx++) for (int dy = 0; dy < 2; dy++)
            {
                float x = boat.Position.X + dx, y = top + dy;
                if (exploration is not null && !ExplorationMask.Covers(exploration, new(x + .5f, y + .5f), System.Numerics.Vector2.One)) continue;
                float u = dx * 18f / texture.Width, v = dy * 18f / texture.Height;
                float u1 = (dx * 18 + 16f) / texture.Width, v1 = (dy * 18 + 16f) / texture.Height;
                var a = Vertex(x,y,u,v); var b = Vertex(x+1,y,u1,v); var c = Vertex(x+1,y+1,u1,v1); var d = Vertex(x,y+1,u,v1);
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(a); vertices.Add(c); vertices.Add(d);
                VertexPositionColorTexture Vertex(float px, float py, float tu, float tv) =>
                    new(WorldMeshBuilder.P(px, py, WorldMeshBuilder.OverlayBoatDepth), color, new(tu, tv));
            }
        }
        if (vertices.Count == 0) return;
        int alpha = effect.ReferenceAlpha;
        bool fog = effect.FogEnabled;
        try
        {
            effect.Texture = texture; effect.ReferenceAlpha = 8; effect.FogEnabled = false;
            device.SetVertexBuffer(null);
            device.BlendState = BlendState.AlphaBlend;
            device.DepthStencilState = DepthStencilState.Default;
            var data = vertices.ToArray();
            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply(); device.SamplerStates[0] = SamplerState.PointClamp;
                device.DrawUserPrimitives(PrimitiveType.TriangleList, data, 0, data.Length / 3);
            }
        }
        finally { effect.ReferenceAlpha = alpha; effect.FogEnabled = fog; device.BlendState = BlendState.Opaque; }
    }
}
