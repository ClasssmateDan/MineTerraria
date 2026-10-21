using System;
using System.Collections.Generic;

namespace DimensionalTerraria.Core;

public readonly record struct GridCell(int X, int Y)
{
    public static GridCell operator +(GridCell a, GridCell b) => new(a.X + b.X, a.Y + b.Y);
    public static GridCell operator -(GridCell a, GridCell b) => new(a.X - b.X, a.Y - b.Y);
}
public enum PistonCell { Empty, Movable, Obstacle }
public interface IPistonWorld
{
    int Width { get; }
    int Height { get; }
    PistonCell At(GridCell cell);
}
public sealed record PistonPlan(GridCell Base, GridCell Direction, bool Extend, IReadOnlyList<GridCell> Sources)
{
    public GridCell Head => Base + Direction;
    public GridCell Delta => Extend ? Direction : new(-Direction.X, -Direction.Y);
}
public static class PistonRules
{
    public const int PushLimit = 12;
    public static GridCell Direction(int direction) => (direction & 3) switch { 0 => new(0, -1), 1 => new(1, 0), 2 => new(0, 1), _ => new(-1, 0) };
    public static bool Inside(IPistonWorld world, GridCell p) => p.X >= 10 && p.Y >= 10 && p.X < world.Width - 10 && p.Y < world.Height - 10;
    public static bool TryPlan(IPistonWorld world, GridCell origin, int direction, bool extend, bool sticky, out PistonPlan plan)
    {
        GridCell d = Direction(direction), head = origin + d;
        plan = new(origin, d, extend, Array.Empty<GridCell>());
        if (!Inside(world, origin) || !Inside(world, head)) return false;
        if (!extend)
        {
            GridCell front = head + d;
            if (sticky && Inside(world, front) && world.At(front) == PistonCell.Movable)
                plan = new(origin, d, false, new[] { front });
            return true;
        }
        List<GridCell> sources = new();
        for (GridCell next = head; Inside(world, next); next += d)
        {
            PistonCell cell = world.At(next);
            if (cell == PistonCell.Empty) { plan = new(origin, d, true, sources); return true; }
            if (cell == PistonCell.Obstacle || sources.Count == PushLimit) return false;
            sources.Add(next);
        }
        return false;
    }
}
