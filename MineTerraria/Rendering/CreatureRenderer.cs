using System;
using System.Collections.Generic;
using DimensionalTerraria.Core;
using DimensionalTerraria.NPCs;
using DimensionalTerraria.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using NVector3 = System.Numerics.Vector3;

namespace DimensionalTerraria.Rendering;

internal readonly record struct CreatureAnchor(NPC Npc, NVector3 Top, NVector3 Center);

// Creature sprites and their health bars address the same live entities.
internal sealed class CreatureRenderer
{
    private readonly VertexPositionColorTexture[] vertices = new VertexPositionColorTexture[6];
    public readonly List<CreatureAnchor> Anchors = new();

    internal static float SpriteDepth(NPC npc)
    {
        // A seated creature belongs above its boat, including species normally
        // drawn behind tiles. Do not change that species' native behindTiles flag.
        bool seated = npc.TryGetGlobalNPC<BoatCaptive>(out var state) && state.Host is { Live: true } boat &&
            ReferenceEquals(boat.Occupant, npc);
        return npc.behindTiles && !seated ? 0 : .57f;
    }

    public void Draw(GraphicsDevice device, AlphaTestEffect effect, FlightState flight, float radius, IExplorationMap? exploration)
    {
        Anchors.Clear();
        device.SetVertexBuffer(null);
        device.BlendState = BlendState.AlphaBlend;
        device.DepthStencilState = DepthStencilState.Default;
        int oldAlpha = effect.ReferenceAlpha;
        bool oldFog = effect.FogEnabled;
        effect.ReferenceAlpha = 8;
        // AlphaTestEffect fog adds opaque sky RGB to translucent sprites. Fade the
        // premultiplied draw colors instead, so distant creatures do not wash out.
        effect.FogEnabled = false;
        try
        {
            for (int index = 0; index < Main.maxNPCs; index++)
            {
                NPC npc = Main.npc[index];
                if (!npc.active || npc.hide || npc.type <= NPCID.None || npc.type >= TextureAssets.Npc.Length || npc.scale <= 0) continue;
                Vector2 center = npc.Center / 16;
                if (MathF.Abs(center.X - flight.Position.X) > radius || MathF.Abs(center.Y - flight.Position.Y) > radius) continue;
                if (exploration is not null && !ExplorationMask.Covers(exploration,
                    new(center.X, center.Y), new(npc.width / 16f, npc.height / 16f))) continue;

                IPixelCreature? pixelCreature = npc.ModNPC as IPixelCreature;
                if (pixelCreature is { SpriteReady: false }) continue;
                Main.instance.LoadNPC(npc.type);
                Texture2D texture = TownNPCProfiles.Instance.GetProfile(npc, out var profile)
                    ? profile.GetTextureNPCShouldUse(npc).Value : TextureAssets.Npc[npc.type].Value;
                Rectangle frame = pixelCreature?.SpriteFrame ?? npc.frame;
                if (frame.Width <= 0 || frame.Height <= 0)
                    frame = new Rectangle(0, 0, texture.Width, texture.Height / Math.Max(1, Main.npcFrameCount[npc.type]));
                if (frame.X < 0 || frame.Y < 0 || frame.Right > texture.Width || frame.Bottom > texture.Height) continue;

                float drawScale = npc.scale * (pixelCreature?.SpriteScale ?? 1);
                Vector2 size = frame.Size() * drawScale / 16;
                float anchorX = pixelCreature is null ? frame.Width / 2f : pixelCreature.SpriteOrigin.X;
                if (pixelCreature is not null && npc.spriteDirection == -1) anchorX = frame.Width - anchorX;
                Vector2 drawCenter = new(npc.Center.X / 16 + (frame.Width / 2f - anchorX) * drawScale / 16,
                    (npc.Bottom.Y + (pixelCreature is null ? 4 : 0) + npc.gfxOffY) / 16 - size.Y / 2);
                float sin = MathF.Sin(npc.rotation), cos = MathF.Cos(npc.rotation);
                Vector2 extent = new(MathF.Abs(cos) * size.X + MathF.Abs(sin) * size.Y,
                    MathF.Abs(sin) * size.X + MathF.Abs(cos) * size.Y);
                if (exploration is not null && !ExplorationMask.Covers(exploration,
                    new(drawCenter.X, drawCenter.Y), new(extent.X, extent.Y))) continue;

                float u0 = (frame.X + 0.05f) / texture.Width, u1 = (frame.Right - 0.05f) / texture.Width;
                float v0 = (frame.Y + 0.05f) / texture.Height, v1 = (frame.Bottom - 0.05f) / texture.Height;
                if (pixelCreature is null ? npc.spriteDirection == 1 : npc.spriteDirection == -1) (u0, u1) = (u1, u0);
                float distance = Vector3.Distance(new Vector3(flight.Position.X, flight.Position.Y, flight.Position.Z), new Vector3(center, 0));
                float fade = oldFog ? 1 - MathHelper.SmoothStep(0, 1, MathHelper.Clamp((distance - effect.FogStart) / Math.Max(1, effect.FogEnd - effect.FogStart), 0, 1)) : 1;
                Color color = npc.GetAlpha(Color.White) * fade;
                float z = SpriteDepth(npc);
                Anchors.Add(new(npc, new(drawCenter.X, drawCenter.Y - extent.Y / 2 - 0.25f, z + 0.03f), new(center.X, center.Y, z)));
                VertexPositionColorTexture a = Vertex(-0.5f, -0.5f, u0, v0), b = Vertex(0.5f, -0.5f, u1, v0);
                VertexPositionColorTexture c = Vertex(0.5f, 0.5f, u1, v1), d = Vertex(-0.5f, 0.5f, u0, v1);
                vertices[0] = a; vertices[1] = b; vertices[2] = c;
                vertices[3] = a; vertices[4] = c; vertices[5] = d;
                effect.Texture = texture;
                DrawColor(color);
                // Vanilla slimes use a faint neutral base PLUS a colored overlay.
                // Blue Slime's base alpha is only 80/255; omitting this second pass
                // loses both its blue color and most of its visible body.
                if (npc.color != default(Color)) DrawColor(npc.GetColor(Color.White) * fade);

                void DrawColor(Color tint)
                {
                    float flash = MathHelper.Clamp(npc.GetGlobalNPC<DimensionEnemyState>().DamageFlash / 0.20f, 0, 1);
                    tint = Color.Lerp(tint, new Color(tint.A, tint.A / 3, tint.A / 3, tint.A), flash * 0.7f);
                    for (int i = 0; i < vertices.Length; i++) vertices[i].Color = tint;
                    foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        device.SamplerStates[0] = SamplerState.PointClamp;
                        device.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, 2);
                    }
                }

                VertexPositionColorTexture Vertex(float x, float y, float u, float v)
                {
                    float dx = x * size.X, dy = y * size.Y;
                    return new(WorldMeshBuilder.P(drawCenter.X + dx * cos - dy * sin, drawCenter.Y + dx * sin + dy * cos, z), color, new Vector2(u, v));
                }
            }
        }
        finally
        {
            effect.ReferenceAlpha = oldAlpha;
            effect.FogEnabled = oldFog;
            device.BlendState = BlendState.Opaque;
        }
    }
}
