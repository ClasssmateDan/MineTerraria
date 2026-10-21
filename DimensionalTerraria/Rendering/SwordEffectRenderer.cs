using System;
using System.Collections.Generic;
using System.Linq;
using DimensionalTerraria.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.Graphics;

namespace DimensionalTerraria.Rendering;

// Original sprite assets and Zenith profile colors, rendered with real depth.
// The original screen-space FinalFractal shader cannot consume a 3D camera.
internal sealed class SwordEffectRenderer
{
    private readonly List<VertexPositionColor> trails = new();
    private readonly VertexPositionColorTexture[] quad = new VertexPositionColorTexture[6];

    public void Draw(GraphicsDevice device, BasicEffect colorEffect, AlphaTestEffect spriteEffect, IReadOnlyList<SwordFlight> blades, FlightState flight)
    {
        if (blades.Count == 0) return;
        Matrix view = spriteEffect.View, projection = spriteEffect.Projection;
        Vector3 right = new(view.M11, view.M21, view.M31), up = new(view.M12, view.M22, view.M32);
        Vector3 eye = P(flight.Eye);
        trails.Clear();
        foreach (SwordFlight blade in blades)
        {
            Color color = blade.Effect == SwordEffect.Zenith
                ? FinalFractalHelper.GetFinalFractalProfile(blade.Skin).trailColor : new Color(255, 130, 45);
            System.Numerics.Vector3[] points = blade.Trail.ToArray();
            for (int i = 1; i < points.Length; i++)
            {
                Vector3 a = P(points[i - 1]), b = P(points[i]);
                Vector3 side = Vector3.Cross(b - a, eye - b);
                side = side.LengthSquared() < 0.000001f ? right : Vector3.Normalize(side);
                float fade = i / (float)points.Length;
                side *= (blade.Effect == SwordEffect.Zenith ? 0.65f : 0.9f) * fade;
                Color tint = color * (blade.Opacity * fade * 0.7f * SwordVisuals.CameraFade(blade.Effect, points[i], flight.Eye));
                trails.Add(new(a - side, tint)); trails.Add(new(a + side, tint)); trails.Add(new(b + side, tint));
                trails.Add(new(a - side, tint)); trails.Add(new(b + side, tint)); trails.Add(new(b - side, tint));
            }
        }
        device.SetVertexBuffer(null);
        device.DepthStencilState = DepthStencilState.DepthRead;
        device.BlendState = BlendState.Additive;
        colorEffect.World = Matrix.Identity; colorEffect.View = view; colorEffect.Projection = projection;
        colorEffect.TextureEnabled = false; colorEffect.VertexColorEnabled = true;
        if (trails.Count > 0)
        {
            VertexPositionColor[] data = trails.ToArray();
            foreach (EffectPass pass in colorEffect.CurrentTechnique.Passes)
            { pass.Apply(); device.DrawUserPrimitives(PrimitiveType.TriangleList, data, 0, data.Length / 3); }
        }

        device.BlendState = BlendState.AlphaBlend;
        bool oldFog = spriteEffect.FogEnabled;
        int oldAlpha = spriteEffect.ReferenceAlpha;
        spriteEffect.FogEnabled = false; spriteEffect.ReferenceAlpha = 4;
        try
        {
            foreach (SwordFlight blade in blades)
            {
                bool sword = blade.Effect == SwordEffect.Zenith;
                Texture2D texture;
                Rectangle frame;
                if (sword)
                {
                    Main.instance.LoadItem(blade.Skin);
                    texture = TextureAssets.Item[blade.Skin].Value;
                    frame = Main.itemAnimations[blade.Skin]?.GetFrame(texture) ?? texture.Bounds;
                }
                else
                {
                    Main.instance.LoadProjectile(blade.Skin);
                    texture = TextureAssets.Projectile[blade.Skin].Value;
                    int count = Math.Max(1, Main.projFrames[blade.Skin]), height = texture.Height / count;
                    frame = new(0, (int)(blade.Age * 20) % count * height, texture.Width, height);
                }
                float scale = sword ? Math.Min(1, 80f / Math.Max(frame.Width, frame.Height)) : 1;
                float width = frame.Width * scale / 16, heightWorld = frame.Height * scale / 16;
                Vector3 direction = P(blade.Direction);
                float x = Vector3.Dot(direction, right), y = Vector3.Dot(direction, up);
                float angle = x * x + y * y < 0.0001f ? 0 : MathF.Atan2(y, x);
                if (sword) angle -= MathF.Atan2(heightWorld, width);
                else angle -= MathF.PI / 2; // Native sonic-boom art points upward at rotation zero.
                float sin = MathF.Sin(angle), cos = MathF.Cos(angle);
                Vector3 center = P(blade.Position);
                float opacity = blade.Opacity * SwordVisuals.CameraFade(blade.Effect, blade.Position, flight.Eye);
                Color tint = (sword ? Color.White : new Color(SwordVisuals.DragonCore.X, SwordVisuals.DragonCore.Y, SwordVisuals.DragonCore.Z)) * opacity;
                // Rotate after sizing; otherwise a non-square sprite is sheared.
                VertexPositionColorTexture SizedVertex(float px, float py, float u, float v)
                {
                    float sx = px * width, sy = py * heightWorld;
                    return new(center + right * (sx * cos - sy * sin) + up * (sx * sin + sy * cos), tint, new(u, v));
                }
                float u0 = frame.X / (float)texture.Width, u1 = frame.Right / (float)texture.Width;
                float v0 = frame.Y / (float)texture.Height, v1 = frame.Bottom / (float)texture.Height;
                var a = SizedVertex(-0.5f, 0.5f, u0, v0); var b = SizedVertex(0.5f, 0.5f, u1, v0);
                var c = SizedVertex(0.5f, -0.5f, u1, v1); var d = SizedVertex(-0.5f, -0.5f, u0, v1);
                quad[0] = a; quad[1] = b; quad[2] = c; quad[3] = a; quad[4] = c; quad[5] = d;
                spriteEffect.Texture = texture;
                if (!sword)
                {
                    // The native sonic-boom texture is grayscale; its warm
                    // color comes from tinted glow passes in Main.DrawProj.
                    var glow = new Color(SwordVisuals.DragonGlow.X, SwordVisuals.DragonGlow.Y, SwordVisuals.DragonGlow.Z) * (opacity * 0.6f);
                    for (int i = 0; i < quad.Length; i++) quad[i].Color = glow;
                    device.BlendState = BlendState.Additive;
                    DrawQuad();
                    for (int i = 0; i < quad.Length; i++) quad[i].Color = tint;
                    device.BlendState = BlendState.AlphaBlend;
                }
                DrawQuad();

                void DrawQuad()
                {
                foreach (EffectPass pass in spriteEffect.CurrentTechnique.Passes)
                {
                    pass.Apply(); device.SamplerStates[0] = SamplerState.PointClamp;
                    device.DrawUserPrimitives(PrimitiveType.TriangleList, quad, 0, 2);
                }
                }
            }
        }
        finally
        {
            spriteEffect.FogEnabled = oldFog; spriteEffect.ReferenceAlpha = oldAlpha;
            device.BlendState = BlendState.Opaque; device.DepthStencilState = DepthStencilState.Default;
        }
    }

    private static Vector3 P(System.Numerics.Vector3 v) => WorldMeshBuilder.P(v.X, v.Y, v.Z);
}
