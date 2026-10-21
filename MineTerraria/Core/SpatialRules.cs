using System;
using System.Numerics;

namespace DimensionalTerraria.Core;

// Coordinates are in tiles; X points right, Y down, and Z out of the screen.
public enum BlockShape { Empty, Solid, HalfBlock, Platform }
public readonly record struct WorldCell(BlockShape Shape, bool HasWall = false, bool HasLiquid = false);
public interface ICollisionWorld
{
    int Width { get; }
    int Height { get; }
    WorldCell GetCell(int x, int y);
}

public enum ReturnFailure { None, OutOfWorld, Unexplored, Obstructed, Liquid }

public static class SpatialRules
{
    public const float FrontMinZ = -0.5f;
    public const float FrontMaxZ = 0.5f;
    public const float WallMinZ = -1.5f;
    public const float WallMaxZ = -0.5f;
    public const float BodyHalfDepth = 0.3f;
    public const float MinFlightDepth = FrontMaxZ + BodyHalfDepth;
    public const float MaxFlightDepth = 160f;
    private const float Epsilon = 0.0001f;

    public static ReturnFailure ValidateReturn(ICollisionWorld world, Vector2 center, Vector2 bodySize, int border = 10, IExplorationMap? exploration = null)
    {
        if (!Finite(center.X, center.Y, bodySize.X, bodySize.Y) || bodySize.X <= 0 || bodySize.Y <= 0)
            return ReturnFailure.OutOfWorld;

        Vector2 min = center - bodySize / 2;
        Vector2 max = center + bodySize / 2;
        if (!InsideWorld(world, min, max, border))
            return ReturnFailure.OutOfWorld;
        // Check exploration before terrain, so a return hint cannot disclose hidden obstacles.
        if (exploration is not null && !ExplorationMask.Covers(exploration, center, bodySize))
            return ReturnFailure.Unexplored;

        for (int x = (int)MathF.Floor(min.X + Epsilon); x <= (int)MathF.Floor(max.X - Epsilon); x++)
        for (int y = (int)MathF.Floor(min.Y + Epsilon); y <= (int)MathF.Floor(max.Y - Epsilon); y++)
        {
            WorldCell cell = world.GetCell(x, y);
            if (IntersectsBlock(cell.Shape, x, y, min, max))
                return ReturnFailure.Obstructed;
            if (cell.HasLiquid)
                return ReturnFailure.Liquid;
        }
        return ReturnFailure.None;
    }

    public static bool CanOccupyFlight(ICollisionWorld world, Vector3 center, Vector2 bodySize, int border = 10)
    {
        if (!Finite(center.X, center.Y, center.Z, bodySize.X, bodySize.Y) || bodySize.X <= 0 || bodySize.Y <= 0)
            return false;

        Vector2 min = new Vector2(center.X, center.Y) - bodySize / 2;
        Vector2 max = new Vector2(center.X, center.Y) + bodySize / 2;
        // The entire sheet is an air wall, including empty cells and holes in the terrain.
        if (!InsideWorld(world, min, max, border) || center.Z < MinFlightDepth - Epsilon || center.Z + BodyHalfDepth > MaxFlightDepth)
            return false;

        bool inFront = Overlaps(center.Z - BodyHalfDepth, center.Z + BodyHalfDepth, FrontMinZ, FrontMaxZ);
        bool inWall = Overlaps(center.Z - BodyHalfDepth, center.Z + BodyHalfDepth, WallMinZ, WallMaxZ);
        if (!inFront && !inWall)
            return true;

        for (int x = (int)MathF.Floor(min.X + Epsilon); x <= (int)MathF.Floor(max.X - Epsilon); x++)
        for (int y = (int)MathF.Floor(min.Y + Epsilon); y <= (int)MathF.Floor(max.Y - Epsilon); y++)
        {
            WorldCell cell = world.GetCell(x, y);
            if (inFront && IntersectsBlock(cell.Shape, x, y, min, max))
                return false;
            if (inWall && cell.HasWall)
                return false;
        }
        return true;
    }

    // Small substeps prevent boost movement from tunnelling through the two-layer wall.
    public static Vector3 Move(ICollisionWorld world, Vector3 start, Vector3 delta, Vector2 bodySize, int border = 10)
    {
        if (!Finite(start.X, start.Y, start.Z, delta.X, delta.Y, delta.Z))
            return start;

        float length = delta.Length();
        if (!float.IsFinite(length) || length > 64f)
            return start;
        int steps = Math.Max(1, (int)MathF.Ceiling(length / 0.15f));
        Vector3 step = delta / steps;
        Vector3 position = start;
        for (int i = 0; i < steps; i++)
        {
            Vector3 next = position + new Vector3(step.X, 0, 0);
            if (CanOccupyFlight(world, next, bodySize, border)) position = next;
            next = position + new Vector3(0, step.Y, 0);
            if (CanOccupyFlight(world, next, bodySize, border)) position = next;
            next = position + new Vector3(0, 0, step.Z);
            if (CanOccupyFlight(world, next, bodySize, border)) position = next;
        }
        return position;
    }

    private static bool InsideWorld(ICollisionWorld world, Vector2 min, Vector2 max, int border) =>
        min.X >= border && min.Y >= border && max.X <= world.Width - border && max.Y <= world.Height - border;

    private static bool IntersectsBlock(BlockShape shape, int x, int y, Vector2 min, Vector2 max)
    {
        if (shape == BlockShape.Empty)
            return false;
        float top = y + (shape == BlockShape.HalfBlock ? 0.5f : 0f);
        float bottom = y + (shape == BlockShape.Platform ? 0.125f : 1f);
        return Overlaps(min.X, max.X, x, x + 1) && Overlaps(min.Y, max.Y, top, bottom);
    }

    private static bool Overlaps(float minA, float maxA, float minB, float maxB) =>
        maxA > minB + Epsilon && minA < maxB - Epsilon;

    private static bool Finite(params float[] values)
    {
        foreach (float value in values)
            if (!float.IsFinite(value)) return false;
        return true;
    }
}
