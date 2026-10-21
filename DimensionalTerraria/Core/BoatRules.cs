using System;
using System.Numerics;

namespace DimensionalTerraria.Core;

public readonly record struct BoatCell(bool Occupied, BlockShape Shape, int LiquidAmount = 0, int LiquidType = 0, bool Replaceable = false);
public interface IBoatWorld
{
    int Width { get; }
    int Height { get; }
    BoatCell BoatCellAt(int x, int y);
}
public readonly record struct BoatPlacement(int X, int Y, float SurfaceY)
{
    public Vector2 SeatBottom => new(X + 1.5f, SurfaceY - 0.75f);
}

public static class BoatRules
{
    public const int Width = 3, Height = 2;
    public static bool TryAim(IBoatWorld world, Vector3 eye, Vector3 forward, float reach, out BoatPlacement placement, IExplorationMap? exploration = null)
    {
        placement = default;
        if (!float.IsFinite(eye.X + eye.Y + eye.Z + forward.X + forward.Y + forward.Z) ||
            !float.IsFinite(reach) || reach <= 0 || forward.LengthSquared() < 0.0001f) return false;
        forward = Vector3.Normalize(forward);
        if (forward.Z >= -0.0001f) return false;
        float t = (0.6f - eye.Z) / forward.Z;
        if (t < 0 || t > reach) return false;
        Vector3 point = eye + forward * t;
        int x = (int)MathF.Floor(point.X), y = (int)MathF.Floor(point.Y);
        for (int down = 0; down < 6; down++)
            if (TryPlacement(world, x, y + down, out placement, exploration) &&
                Vector3.Distance(eye, new(x + 0.5f, placement.SurfaceY - 1, 0.6f)) <= reach) return true;
        return false;
    }
    public static bool TryPlacement(IBoatWorld world, int centerX, int supportY, out BoatPlacement placement, IExplorationMap? exploration = null)
    {
        placement = default;
        int left = centerX - 1, top = supportY - Height;
        if (centerX < 13 || centerX >= world.Width - 15 || supportY < 14 || supportY >= world.Height - 12) return false;
        if (exploration is not null)
            for (int x = left; x < left + Width; x++) for (int y = top; y <= supportY; y++)
                if (!exploration.IsExplored(x, y)) return false;
        for (int x = left; x < left + Width; x++) for (int y = top; y < supportY; y++)
        {
            BoatCell cell = world.BoatCellAt(x, y);
            if (cell.Shape != BlockShape.Empty || cell.Occupied && !cell.Replaceable || cell.LiquidAmount != 0) return false;
        }
        if (!Support(world, left, supportY, out float surface)) return false;
        placement = new(left, top, surface);
        return true;
    }

    public static bool Support(IBoatWorld world, int left, int y, out float surface)
    {
        surface = 0;
        if (left < 0 || y < 0 || left > world.Width - Width || y >= world.Height) return false;
        float highest = float.PositiveInfinity;
        for (int x = left; x < left + Width; x++)
        {
            BoatCell cell = world.BoatCellAt(x, y);
            float height;
            if (cell.Shape != BlockShape.Empty)
                height = y + (cell.Shape == BlockShape.HalfBlock ? 0.5f : 0);
            else if (cell.LiquidAmount > 0)
                height = y + 1 - cell.LiquidAmount / 255f;
            else continue;
            // One supported column is enough. Rest on the highest surface so
            // shorelines, half blocks and overhangs cannot lower us into land.
            highest = MathF.Min(highest, height);
        }
        if (!float.IsFinite(highest)) return false;
        surface = highest;
        return true;
    }

    public static bool FitsPassenger(bool active, bool boss, bool segmented, bool protectedNpc, Vector2 size) =>
        active && !boss && !segmented && !protectedNpc && float.IsFinite(size.X) && float.IsFinite(size.Y) &&
        size.X > 0 && size.Y > 0;

    public static bool SweptHit(Vector2 start, Vector2 end, Vector2 halfSize, Vector2 min, Vector2 max)
    {
        if (!float.IsFinite(start.X + start.Y + end.X + end.Y + halfSize.X + halfSize.Y + min.X + min.Y + max.X + max.Y) || halfSize.X < 0 || halfSize.Y < 0) return false;
        Vector2 delta = end - start;
        float distance = delta.Length();
        min -= halfSize; max += halfSize;
        if (distance < 0.001f) return start.X >= min.X && start.X <= max.X && start.Y >= min.Y && start.Y <= max.Y;
        return TileRaycast.Intersect(new(start, 0), new(delta / distance, 0), new(min, -1), new(max, 1), distance, out _);
    }
}
