using System;
using System.Reflection;
using DimensionalTerraria.Core;
using Terraria;

namespace DimensionalTerraria.World;

internal static class NativeBackgroundLayout
{
    private const BindingFlags Hidden = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static readonly FieldInfo topField = typeof(Main).GetField("bgTopY", Hidden)!;
    private static readonly FieldInfo scaleField = typeof(Main).GetField("bgScale", Hidden)!;
    private static readonly FieldInfo parallaxField = typeof(Main).GetField("bgParallax", Hidden)!;
    private static readonly FieldInfo adjustmentField = typeof(Main).GetField("scAdj", Hidden)!;
    private static readonly Action<Main, int, double, int> Offsets = typeof(Main).GetMethod("SetBackgroundOffsets", Hidden)!.CreateDelegate<Action<Main, int, double, int>>();
    private static readonly Func<Main, int, int> LayerOffset = typeof(Main).GetMethod("DrawSurfaceBG_GetLayerYOffset", Hidden)!.CreateDelegate<Func<Main, int, int>>();

    internal static (float Top, float Scale) Sample(Main main, int id, int layer, ProjectedBiome biome, BackdropCamera camera)
    {
        float scale = layer == 0 ? 1.25f : layer == 1 ? 1.31f : 1.34f;
        int coefficient = layer == 0 ? 1800 : layer == 1 ? 1950 : 2100;
        int offset = layer == 0 ? 1500 : layer == 1 ? 1750 : 2000;
        if (biome == ProjectedBiome.Desert) { coefficient = layer == 0 ? 1800 : 2100; offset = layer == 0 ? 1750 : 2150; scale = layer == 0 ? 1.25f : 1.34f; }
        if (biome == ProjectedBiome.Jungle) offset += layer == 0 ? 160 : layer == 1 ? 90 : 60;
        if (biome == ProjectedBiome.Crimson && layer == 0) offset += id == 105 ? 50 : id == 174 ? -350 : 0;
        if (biome == ProjectedBiome.Ocean) { coefficient = 2100; offset = 2000; scale = 1.34f; }
        if (biome == ProjectedBiome.Forest && layer == 0)
        {
            if (id is 91 or 173 or 184) { coefficient = 1600; offset = 1400; }
            scale = id switch { 91 or 178 or 184 => 1.2f, 173 => 1.3f, 282 => 1.4f, _ => scale };
        }
        object? oldTop = topField.GetValue(main), oldScale = scaleField.GetValue(null), oldParallax = parallaxField.GetValue(main), oldAdjustment = adjustmentField.GetValue(main);
        try
        {
            topField.SetValue(main, (int)(camera.Magic * coefficient + offset) + (int)camera.Adjustment);
            scaleField.SetValue(null, scale); adjustmentField.SetValue(main, camera.Adjustment);
            // Only these four background scalars are touched. Restore all of
            // them even on failure; never move the player or native camera.
            Offsets(main, id, camera.Magic, 0);
            float top = (int)topField.GetValue(main)!;
            scale = (float)scaleField.GetValue(null)!;
            if (biome == ProjectedBiome.Forest)
                top += id switch { 50 or 51 or 52 => -50, 53 or 54 or 55 => -100, 91 or 173 => 200, 178 => 75, 92 => 150, _ => 0 };
            else if (biome is ProjectedBiome.Snow or ProjectedBiome.Jungle or ProjectedBiome.Ocean)
                top += LayerOffset(main, id);
            return (SurfaceBackdrop.WorldTop(camera, top), scale * 2);
        }
        finally
        {
            topField.SetValue(main, oldTop); scaleField.SetValue(null, oldScale);
            parallaxField.SetValue(main, oldParallax); adjustmentField.SetValue(main, oldAdjustment);
        }
    }
}
