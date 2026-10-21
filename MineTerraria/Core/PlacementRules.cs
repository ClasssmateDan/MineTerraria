using System;
using System.Numerics;

namespace DimensionalTerraria.Core;

public static class PlacementRules
{
    public const float Reach = 6;
    public static bool Aim(IInteractionWorld world, Vector3 eye, Vector3 direction, bool wall, out GridCell cell, IExplorationMap? fog = null)
    {
        cell = default;
        if (!Finite(eye) || !Finite(direction) || !float.IsFinite(direction.LengthSquared()) || direction.LengthSquared() < .000001f) return false;
        direction = Vector3.Normalize(direction);
        TileRayHit? hit = TileRaycast.Cast(world, eye, direction, Reach, fog);
        if (hit is { Layer: RayLayer.Unexplored }) return false;
        int x, y;
        if (hit is { } surface)
        {
            x = surface.X; y = surface.Y;
            if (!wall && surface.Layer == RayLayer.Tile)
            {
                Vector3 point = eye + direction * surface.Distance;
                // The save remains a 2D sheet: front/back faces cannot create a
                // third foreground layer. Attach to the nearest in-plane edge.
                float[] edges = { MathF.Abs(point.Y - surface.Min.Y), MathF.Abs(point.X - surface.Max.X),
                    MathF.Abs(point.Y - surface.Max.Y), MathF.Abs(point.X - surface.Min.X) };
                int edge = 0;
                for (int i = 1; i < 4; i++) if (edges[i] < edges[edge]) edge = i;
                if (edge == 0) y--; else if (edge == 1) x++; else if (edge == 2) y++; else x--;
            }
        }
        else
        {
            float z = wall ? SpatialRules.WallMaxZ : SpatialRules.FrontMaxZ;
            if (MathF.Abs(direction.Z) < .000001f) return false;
            float t = (z - eye.Z) / direction.Z;
            if (t < 0 || t > Reach) return false;
            Vector3 point = eye + direction * t;
            x = (int)MathF.Floor(point.X); y = (int)MathF.Floor(point.Y);
        }
        if (x < 10 || y < 10 || x >= world.Width - 10 || y >= world.Height - 10 || fog is not null && !fog.IsExplored(x, y)) return false;
        cell = new(x, y);
        return true;
    }
    public static bool InReach(Vector3 eye, int left, int top, int width, int height, bool wall, IExplorationMap? fog = null)
    {
        if (!Finite(eye) || width <= 0 || height <= 0 || width > 16 || height > 16) return false;
        float z = wall ? SpatialRules.WallMaxZ : SpatialRules.FrontMaxZ;
        for (int x = left; x < left + width; x++) for (int y = top; y < top + height; y++)
            if (Vector3.DistanceSquared(eye, new(x + .5f, y + .5f, z)) > Reach * Reach || fog is not null && !fog.IsExplored(x, y)) return false;
        return true;
    }
    private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
}
