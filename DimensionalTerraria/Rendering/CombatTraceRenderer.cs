using System;
using System.Collections.Generic;
using DimensionalTerraria.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NVector3 = System.Numerics.Vector3;

namespace DimensionalTerraria.Rendering;

// Temporary first-person feedback for the aimed attacks. These are visual traces,
// not Terraria projectiles; damage is applied once by NativeCombat.
internal sealed class CombatTraceRenderer
{
    private sealed class Trace(NVector3 start, NVector3 end, WeaponMode mode, bool hit)
    {
        public readonly NVector3 Start = start, End = end;
        public readonly WeaponMode Mode = mode;
        public readonly bool Hit = hit;
        public float Life = 0.18f;
    }

    private readonly List<Trace> traces = new();

    public void Emit(NVector3 start, NVector3 end, WeaponMode mode, bool hit)
    {
        if (traces.Count == 32) traces.RemoveAt(0);
        traces.Add(new(start, end, mode, hit));
    }

    public void Update(float seconds)
    {
        for (int i = traces.Count - 1; i >= 0; i--)
        {
            traces[i].Life -= seconds;
            if (traces[i].Life <= 0) traces.RemoveAt(i);
        }
    }

    public void Draw(GraphicsDevice device, BasicEffect effect, Matrix view, Matrix projection)
    {
        if (traces.Count == 0) return;
        List<VertexPositionColor> vertices = new(traces.Count * 8);
        foreach (Trace trace in traces)
        {
            Color color = (trace.Mode == WeaponMode.Magic ? Color.LightSkyBlue : Color.LightGoldenrodYellow) * Math.Clamp(trace.Life / 0.18f, 0, 1);
            Vector3 end = WorldMeshBuilder.P(trace.End.X, trace.End.Y, trace.End.Z);
            if (trace.Mode != WeaponMode.Melee)
            {
                vertices.Add(new(WorldMeshBuilder.P(trace.Start.X, trace.Start.Y, trace.Start.Z), color));
                vertices.Add(new(end, color));
            }
            if (!trace.Hit) continue;
            Vector3 right = new(view.M11, view.M21, view.M31), up = new(view.M12, view.M22, view.M32);
            foreach (Vector3 axis in new[] { right + up, right - up })
            {
                float spread = 0.20f + (0.18f - trace.Life) * 0.7f;
                vertices.Add(new(end - axis * spread, color));
                vertices.Add(new(end + axis * spread, color));
            }
        }
        if (vertices.Count == 0) return;
        device.SetVertexBuffer(null);
        device.BlendState = BlendState.AlphaBlend;
        device.DepthStencilState = DepthStencilState.DepthRead;
        effect.World = Matrix.Identity; effect.View = view; effect.Projection = projection;
        effect.TextureEnabled = false;
        effect.VertexColorEnabled = true;
        foreach (EffectPass pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawUserPrimitives(PrimitiveType.LineList, vertices.ToArray(), 0, vertices.Count / 2);
        }
        device.BlendState = BlendState.Opaque;
        device.DepthStencilState = DepthStencilState.Default;
    }

    public void Reset() => traces.Clear();
}
