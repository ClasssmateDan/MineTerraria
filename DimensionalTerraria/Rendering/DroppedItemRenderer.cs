using System;
using DimensionalTerraria.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;

namespace DimensionalTerraria.Rendering;

internal sealed class DroppedItemRenderer
{
    private readonly VertexPositionColorTexture[] vertices = new VertexPositionColorTexture[6];

    public void Draw(GraphicsDevice device, AlphaTestEffect effect, FlightState flight, float radius, IExplorationMap? exploration)
    {
        device.SetVertexBuffer(null);
        device.BlendState = BlendState.AlphaBlend;
        device.DepthStencilState = DepthStencilState.Default;
        int alpha = effect.ReferenceAlpha;
        bool fog = effect.FogEnabled;
        effect.ReferenceAlpha = 8;
        effect.FogEnabled = false;
        try
        {
            for (int i = 0; i < Main.maxItems; i++)
            {
                Item item = Main.item[i];
                if (!item.active || item.IsAir || item.type >= TextureAssets.Item.Length) continue;
                Vector2 center = item.Center / 16;
                if (MathF.Abs(center.X - flight.Position.X) > radius || MathF.Abs(center.Y - flight.Position.Y) > radius) continue;
                Main.instance.LoadItem(item.type);
                Texture2D texture = TextureAssets.Item[item.type].Value;
                Rectangle frame = Main.itemAnimations[item.type]?.GetFrame(texture) ?? texture.Bounds;
                Vector2 size = frame.Size() * item.scale / 16;
                if (exploration is not null && !ExplorationMask.Covers(exploration, new(center.X, center.Y), new(size.X, size.Y))) continue;
                float distance = Vector3.Distance(new(flight.Position.X, flight.Position.Y, flight.Position.Z), new(center, InteractionRules.ItemDepth));
                float fade = fog ? 1 - MathHelper.SmoothStep(0, 1, MathHelper.Clamp((distance - effect.FogStart) / Math.Max(1, effect.FogEnd - effect.FogStart), 0, 1)) : 1;
                Color color = item.GetAlpha(Color.White) * fade;
                float u0 = (frame.Left + 0.05f) / texture.Width, u1 = (frame.Right - 0.05f) / texture.Width;
                float v0 = (frame.Top + 0.05f) / texture.Height, v1 = (frame.Bottom - 0.05f) / texture.Height;
                VertexPositionColorTexture a = Vertex(-0.5f, -0.5f, u0, v0), b = Vertex(0.5f, -0.5f, u1, v0);
                VertexPositionColorTexture c = Vertex(0.5f, 0.5f, u1, v1), d = Vertex(-0.5f, 0.5f, u0, v1);
                vertices[0] = a; vertices[1] = b; vertices[2] = c;
                vertices[3] = a; vertices[4] = c; vertices[5] = d;
                effect.Texture = texture;
                foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    device.SamplerStates[0] = SamplerState.PointClamp;
                    device.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, 2);
                }

                VertexPositionColorTexture Vertex(float x, float y, float u, float v)
                {
                    float dx = x * size.X, dy = y * size.Y;
                    return new(WorldMeshBuilder.P(center.X + dx, center.Y + dy, InteractionRules.ItemDepth), color, new(u, v));
                }
            }
        }
        finally
        {
            effect.ReferenceAlpha = alpha;
            effect.FogEnabled = fog;
            device.BlendState = BlendState.Opaque;
        }
    }
}
