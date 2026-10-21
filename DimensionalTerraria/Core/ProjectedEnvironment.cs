using System;
using System.Numerics;

namespace DimensionalTerraria.Core;

public readonly record struct SkyPalette(Vector3 Top, Vector3 Horizon)
{
    // Terraria's clock starts day at dawn and night at dusk. Match colors at
    // both clock boundaries so switching dayTime never causes a sky flash.
    public static SkyPalette Sample(bool day, double time)
    {
        SkyPalette dawn = new(new(0.25f, 0.35f, 0.57f), new(0.94f, 0.65f, 0.43f));
        SkyPalette noon = new(new(0.30f, 0.57f, 0.85f), new(0.64f, 0.81f, 0.96f));
        SkyPalette dusk = new(new(0.20f, 0.18f, 0.38f), new(0.94f, 0.40f, 0.24f));
        SkyPalette night = new(new(0.018f, 0.025f, 0.07f), new(0.07f, 0.10f, 0.19f));
        time = double.IsFinite(time) ? Math.Clamp(time, 0, day ? 54000 : 32400) : 0;
        if (day) return time < 3600 ? Lerp(dawn, noon, (float)time / 3600)
            : Lerp(noon, dusk, (float)(time - 48600) / 5400);
        return time < 3600 ? Lerp(dusk, night, (float)time / 3600)
            : Lerp(night, dawn, (float)(time - 28800) / 3600);
    }

    private static SkyPalette Lerp(SkyPalette a, SkyPalette b, float amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        amount *= amount * (3 - 2 * amount);
        return new(Vector3.Lerp(a.Top, b.Top, amount), Vector3.Lerp(a.Horizon, b.Horizon, amount));
    }
}

public enum ProjectedBiome { Unknown, Forest, Desert, Snow, Jungle, Corruption, Crimson, Hallow, Mushroom, Dungeon, Ocean }
public enum ProjectedDepth { Sky, Surface, Underground, Cavern, Underworld }

public interface IEnvironmentWorld
{
    int Width { get; }
    int Height { get; }
    ProjectedBiome MaterialAt(int x, int y);
}

public readonly record struct ProjectedEnvironment(ProjectedBiome Biome, ProjectedDepth Depth)
{
    public static ProjectedEnvironment Sample(IEnvironmentWorld world, Vector2 projection,
        float surface, float rock)
    {
        int cx = (int)projection.X, cy = (int)projection.Y;
        ProjectedDepth depth = projection.Y > world.Height - 200 ? ProjectedDepth.Underworld
            : projection.Y > rock ? ProjectedDepth.Cavern
            : projection.Y > surface ? ProjectedDepth.Underground
            : projection.Y < surface * 0.35f ? ProjectedDepth.Sky : ProjectedDepth.Surface;
        if (!float.IsFinite(projection.X) || !float.IsFinite(projection.Y) ||
            cx < 0 || cy < 0 || cx >= world.Width || cy >= world.Height) return new(ProjectedBiome.Unknown, depth);

        Span<int> counts = stackalloc int[11];
        counts.Clear();
        for (int x = Math.Max(1, cx - 60); x < Math.Min(world.Width - 1, cx + 61); x += 3)
        for (int y = Math.Max(1, cy - 42); y < Math.Min(world.Height - 1, cy + 43); y += 3)
        {
            // Backgrounds follow the projected biome even in unexplored areas.
            // Terrain, creatures and interactions enforce exploration separately.
            ProjectedBiome material = world.MaterialAt(x, y);
            if (material != ProjectedBiome.Unknown) counts[(int)material]++;
        }
        ProjectedBiome biome = ProjectedBiome.Forest;
        int best = 7;
        for (int i = (int)ProjectedBiome.Desert; i <= (int)ProjectedBiome.Dungeon; i++)
            if (counts[i] > best) { best = counts[i]; biome = (ProjectedBiome)i; }
        if (depth == ProjectedDepth.Surface && world.Width > 800 && (cx < 380 || cx > world.Width - 380))
            biome = ProjectedBiome.Ocean;
        return new(biome, depth);
    }
}
