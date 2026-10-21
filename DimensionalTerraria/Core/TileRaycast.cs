using System;
using System.Numerics;

namespace DimensionalTerraria.Core;

public readonly record struct InteractionCell(bool HasTile, bool HasWall, BlockShape Shape);
public interface IInteractionWorld
{
    int Width { get; }
    int Height { get; }
    InteractionCell GetInteractionCell(int x, int y);
}

public enum RayLayer { Tile, Wall, Unexplored }
public readonly record struct TileRayHit(int X, int Y, RayLayer Layer, float Distance, Vector3 Min, Vector3 Max);

public static class TileRaycast
{
    public static TileRayHit? Cast(IInteractionWorld world, Vector3 origin, Vector3 direction, float reach, IExplorationMap? exploration = null)
    {
        if (!Finite(origin) || !Finite(direction) || !float.IsFinite(reach) || reach <= 0 || reach > 128 ||
            !float.IsFinite(direction.LengthSquared()) || direction.LengthSquared() < 0.000001f) return null;
        direction = Vector3.Normalize(direction);
        Vector3 end = origin + direction * reach;
        int x0 = Math.Max(10, (int)MathF.Floor(MathF.Min(origin.X, end.X)) - 1);
        int y0 = Math.Max(10, (int)MathF.Floor(MathF.Min(origin.Y, end.Y)) - 1);
        int x1 = Math.Min(world.Width - 11, (int)MathF.Floor(MathF.Max(origin.X, end.X)) + 1);
        int y1 = Math.Min(world.Height - 11, (int)MathF.Floor(MathF.Max(origin.Y, end.Y)) + 1);
        TileRayHit? closest = null;
        for (int x = x0; x <= x1; x++)
        for (int y = y0; y <= y1; y++)
        {
            if (exploration is not null && !exploration.IsExplored(x, y))
            {
                Test(RayLayer.Unexplored, new(x, y, SpatialRules.WallMinZ), new(x + 1, y + 1, 0.68f));
                continue;
            }
            InteractionCell cell = world.GetInteractionCell(x, y);
            if (cell.HasTile)
            {
                float top = y + (cell.Shape == BlockShape.HalfBlock ? 0.5f : 0);
                float bottom = y + (cell.Shape == BlockShape.Platform ? 0.125f : 1);
                bool sprite = cell.Shape == BlockShape.Empty;
                Test(RayLayer.Tile, new(x, top, sprite ? 0.50f : SpatialRules.FrontMinZ),
                    new(x + 1, bottom, sprite ? 0.55f : SpatialRules.FrontMaxZ));
            }
            if (cell.HasWall)
                Test(RayLayer.Wall, new(x, y, SpatialRules.WallMinZ), new(x + 1, y + 1, SpatialRules.WallMaxZ));

            void Test(RayLayer layer, Vector3 min, Vector3 max)
            {
                if (!Intersect(origin, direction, min, max, reach, out float distance)) return;
                if (closest is null || distance < closest.Value.Distance - 0.00001f)
                    closest = new TileRayHit(x, y, layer, distance, min, max);
            }
        }
        return closest;
    }

    public static bool Intersect(Vector3 origin, Vector3 direction, Vector3 min, Vector3 max, float reach, out float distance)
    {
        float near = 0, far = reach;
        bool hit = Axis(origin.X, direction.X, min.X, max.X) && Axis(origin.Y, direction.Y, min.Y, max.Y) &&
            Axis(origin.Z, direction.Z, min.Z, max.Z);
        distance = near;
        return hit;

        bool Axis(float o, float d, float a, float b)
        {
            if (MathF.Abs(d) < 0.000001f) return o >= a && o <= b;
            float t0 = (a - o) / d, t1 = (b - o) / d;
            if (t0 > t1) (t0, t1) = (t1, t0);
            near = MathF.Max(near, t0); far = MathF.Min(far, t1);
            return near <= far;
        }
    }

    private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
}
