using System;
using System.Collections.Generic;
using DimensionalTerraria.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using ReLogic.Content;
using NMatrix = System.Numerics.Matrix4x4;

namespace DimensionalTerraria.Rendering;

internal static class FirstPersonArm
{
    public static void Draw(GraphicsDevice device, BasicEffect effect, AlphaTestEffect itemEffect, Matrix projection, Player player, float elapsed, bool moving, float swing)
    {
        List<VertexPositionColor> vertices = new(72);
        Cuboid(-0.09f, 0, -0.1f, 0.09f, 0.24f, 0.1f, new Color(184, 137, 110));
        Cuboid(-0.095f, -0.65f, -0.105f, 0.095f, 0, 0.105f, new Color(0, 165, 173));
        NMatrix hand = FirstPersonPose.Hand(Numeric(projection), elapsed, moving);
        effect.World = Xna(FirstPersonPose.Swing(swing) * hand);
        effect.View = Matrix.Identity;
        effect.Projection = projection;
        effect.TextureEnabled = false;
        effect.VertexColorEnabled = true;
        device.Clear(ClearOptions.DepthBuffer, Color.Transparent, 1, 0);
        device.DepthStencilState = DepthStencilState.Default;
        foreach (EffectPass pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawUserPrimitives(PrimitiveType.TriangleList, vertices.ToArray(), 0, vertices.Count / 3);
        }
        if (ProjectionArmorAppearance.ChestVisible(player)) DrawArmorSleeve(device, itemEffect, projection, hand, swing);
        DrawItem(device, itemEffect, projection, hand, player.HeldItem, swing);

        void Cuboid(float x0, float y0, float z0, float x1, float y1, float z1, Color color)
        {
            Face(new(x0,y1,z1), new(x1,y1,z1), new(x1,y0,z1), new(x0,y0,z1), color, 1);
            Face(new(x1,y1,z0), new(x0,y1,z0), new(x0,y0,z0), new(x1,y0,z0), color, 0.72f);
            Face(new(x0,y1,z0), new(x0,y1,z1), new(x0,y0,z1), new(x0,y0,z0), color, 0.65f);
            Face(new(x1,y1,z1), new(x1,y1,z0), new(x1,y0,z0), new(x1,y0,z1), color, 0.83f);
            Face(new(x0,y1,z0), new(x1,y1,z0), new(x1,y1,z1), new(x0,y1,z1), color, 1);
            Face(new(x0,y0,z1), new(x1,y0,z1), new(x1,y0,z0), new(x0,y0,z0), color, 0.6f);
        }

        void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color baseColor, float light)
        {
            Color color = new((int)(baseColor.R * light), (int)(baseColor.G * light), (int)(baseColor.B * light), 255);
            vertices.Add(new(a, color)); vertices.Add(new(b, color)); vertices.Add(new(c, color));
            vertices.Add(new(a, color)); vertices.Add(new(c, color)); vertices.Add(new(d, color));
        }
    }

    private static void DrawArmorSleeve(GraphicsDevice device, AlphaTestEffect effect, Matrix projection, NMatrix hand, float swing)
    {
        Texture2D texture = ModContent.Request<Texture2D>("DimensionalTerraria/Textures/Minecraft/netherite_layer_1", AssetRequestMode.ImmediateLoad).Value;
        const float x0 = -0.102f, x1 = 0.102f, y0 = -0.658f, y1 = 0.006f, z0 = -0.112f, z1 = 0.112f;
        List<VertexPositionColorTexture> vertices = new(36);
        Face(new(x0,y1,z1),new(x1,y1,z1),new(x1,y0,z1),new(x0,y0,z1),44,20,4,12,1);
        Face(new(x1,y1,z0),new(x0,y1,z0),new(x0,y0,z0),new(x1,y0,z0),52,20,4,12,0.72f);
        Face(new(x0,y1,z0),new(x0,y1,z1),new(x0,y0,z1),new(x0,y0,z0),40,20,4,12,0.65f);
        Face(new(x1,y1,z1),new(x1,y1,z0),new(x1,y0,z0),new(x1,y0,z1),48,20,4,12,0.83f);
        Face(new(x0,y1,z0),new(x1,y1,z0),new(x1,y1,z1),new(x0,y1,z1),48,16,4,4,1);
        Face(new(x0,y0,z1),new(x1,y0,z1),new(x1,y0,z0),new(x0,y0,z0),44,16,4,4,0.6f);
        effect.World = Xna(FirstPersonPose.Swing(swing) * hand);
        effect.View = Matrix.Identity;
        effect.Projection = projection;
        effect.FogEnabled = false;
        effect.Texture = texture;
        foreach (EffectPass pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.SamplerStates[0] = SamplerState.PointClamp;
            device.DrawUserPrimitives(PrimitiveType.TriangleList, vertices.ToArray(), 0, vertices.Count / 3);
        }
        void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int u, int v, int w, int h, float light)
        {
            Color color = Color.White * light;
            var va = new VertexPositionColorTexture(a,color,new Vector2(u / 64f,(v+h) / 32f));
            var vb = new VertexPositionColorTexture(b,color,new Vector2((u+w) / 64f,(v+h) / 32f));
            var vc = new VertexPositionColorTexture(c,color,new Vector2((u+w) / 64f,v / 32f));
            var vd = new VertexPositionColorTexture(d,color,new Vector2(u / 64f,v / 32f));
            vertices.Add(va); vertices.Add(vb); vertices.Add(vc); vertices.Add(va); vertices.Add(vc); vertices.Add(vd);
        }
    }

    private static void DrawItem(GraphicsDevice device, AlphaTestEffect effect, Matrix projection, NMatrix hand, Item item, float swing)
    {
        if (item.IsAir) return;
        Main.instance.LoadItem(item.type);
        Texture2D texture = TextureAssets.Item[item.type].Value;
        Rectangle frame = Main.itemAnimations[item.type]?.GetFrame(texture) ?? new Rectangle(0, 0, texture.Width, texture.Height);
        bool shooting = item.useStyle == ItemUseStyleID.Shoot;
        bool tool = !shooting && (item.damage > 0 || item.pick > 0 || item.axe > 0 || item.hammer > 0);
        HeldItemLayout layout = HeldItemLayout.Create(frame.Width, frame.Height, tool, shooting);
        // The grip sits inside the fingers, slightly behind their front face.
        // Depth testing lets the hand cover the handle rather than float beside it.
        effect.World = Xna(FirstPersonPose.Item(layout, hand, Numeric(projection), swing));
        effect.View = Matrix.Identity;
        effect.Projection = projection;
        effect.FogEnabled = false;
        effect.Texture = texture;
        float left = (frame.Left + 0.01f) / texture.Width, right = (frame.Right - 0.01f) / texture.Width;
        float top = (frame.Top + 0.01f) / texture.Height, bottom = (frame.Bottom - 0.01f) / texture.Height;
        if (layout.FlipHorizontally) (left, right) = (right, left);
        VertexPositionColorTexture a = Vertex(0, 0, left, top), b = Vertex(1, 0, right, top);
        VertexPositionColorTexture c = Vertex(1, 1, right, bottom), d = Vertex(0, 1, left, bottom);
        VertexPositionColorTexture[] quad = { a, b, c, a, c, d };
        device.BlendState = BlendState.AlphaBlend;
        foreach (EffectPass pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.SamplerStates[0] = SamplerState.PointClamp;
            device.DrawUserPrimitives(PrimitiveType.TriangleList, quad, 0, 2);
        }

        VertexPositionColorTexture Vertex(float u, float v, float texU, float texV)
        {
            System.Numerics.Vector2 p = layout.At(u, v);
            return new(new Vector3(p.X, p.Y, 0), Color.White, new Vector2(texU, texV));
        }
    }

    private static NMatrix Numeric(Matrix m) => new(m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24,
        m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44);

    private static Matrix Xna(NMatrix m) => new(m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24,
        m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44);
}
