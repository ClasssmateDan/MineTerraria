using System;
using System.Numerics;

namespace DimensionalTerraria.Core;

public static class SwordVisuals
{
    public static readonly Vector3 DragonCore = new(1, 0.80f, 0.28f);
    public static readonly Vector3 DragonGlow = new(1, 0.28f, 0.06f);

    public static float CameraFade(SwordEffect effect, Vector3 position, Vector3 eye)
    {
        if (effect != SwordEffect.Zenith) return 1;
        float t = Math.Clamp((Vector3.Distance(position, eye) - 3) / 4, 0, 1);
        return t * t * (3 - 2 * t);
    }
}
