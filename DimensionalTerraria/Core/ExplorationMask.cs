using System;
using System.Collections;
using System.Numerics;

namespace DimensionalTerraria.Core;

public interface IExplorationMap
{
    int Width { get; }
    int Height { get; }
    bool IsExplored(int x, int y);
}

// A flight uses a snapshot: viewing terrain in 3D never expands the explored area.
public sealed class ExplorationMask : IExplorationMap
{
    private readonly BitArray explored;
    public int Width { get; }
    public int Height { get; }

    public ExplorationMask(int width, int height, Func<int, int, bool> isExplored)
    {
        Width = width;
        Height = height;
        explored = new BitArray(checked(width * height));
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
            explored[x * height + y] = isExplored(x, y);
    }

    public bool IsExplored(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && explored[x * Height + y];

    public static bool Covers(IExplorationMap map, Vector2 center, Vector2 size)
    {
        if (!float.IsFinite(center.X) || !float.IsFinite(center.Y) ||
            !float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X <= 0 || size.Y <= 0) return false;
        Vector2 min = center - size / 2, max = center + size / 2;
        if (min.X < 0 || min.Y < 0 || max.X > map.Width || max.Y > map.Height) return false;
        for (int x = (int)MathF.Floor(min.X + 0.0001f); x <= (int)MathF.Floor(max.X - 0.0001f); x++)
        for (int y = (int)MathF.Floor(min.Y + 0.0001f); y <= (int)MathF.Floor(max.Y - 0.0001f); y++)
            if (!map.IsExplored(x, y)) return false;
        return true;
    }
}
