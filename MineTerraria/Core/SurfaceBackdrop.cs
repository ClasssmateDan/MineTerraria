using System;

namespace DimensionalTerraria.Core;

public readonly record struct BackdropCamera(float Top, double Magic, float Adjustment);

public static class SurfaceBackdrop
{
    // Vanilla DrawBG / DrawSurfaceBG, evaluated at the flight's XY projection.
    // Coordinates returned here remain world tiles; UI scaling is unrelated.
    public static BackdropCamera Camera(float centerY, float surface, int worldHeight, int pixelHeight)
    {
        if (!float.IsFinite(centerY) || !float.IsFinite(surface) || surface <= 0 || worldHeight <= 0 || pixelHeight <= 0) return default;
        float pixels = centerY * 16, logicHeight = Math.Min(pixelHeight, 1200);
        float top = centerY - pixelHeight / 32f;
        float sc = surface * 16 / Math.Max(1, pixels + logicHeight / 2);
        float depth = Math.Max(0, worldHeight * .15f * 16 - (pixels - logicHeight / 2)) * .00025f;
        sc *= (.45f - depth * depth) * (worldHeight <= 1200 ? -500 : worldHeight <= 1800 ? -300 : -150);
        double magic = (-pixels + 600 + (pixelHeight - 600) / 2f) / (surface * 16);
        return new(top, magic, sc);
    }
    public static float WorldTop(BackdropCamera camera, float screenTop) => camera.Top + screenTop / 16;
    public static float Height(int pixels, float scale) => pixels > 0 && float.IsFinite(scale) && scale > 0 ? pixels * scale / 16 : 0;
}
