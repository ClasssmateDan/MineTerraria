using System;
using System.Numerics;

namespace DimensionalTerraria.Core;

// All timers count simulation frames, independently of the world's day clock.
public sealed class VillagerMemory
{
    public const int PanicDuration = 3 * 60, GuardDuration = 30 * 60, AttemptInterval = 5 * 60;
    public const int Width = 20, Height = 42, Life = 100;
    public int PanicTicks { get; private set; }
    public int GuardTicks { get; private set; }
    public int AttemptTicks { get; private set; } = AttemptInterval;
    public float ThreatX { get; private set; }
    public bool Panicking => PanicTicks > 0;
    public bool Eligible => GuardTicks == 0;

    public bool Tick()
    {
        if (PanicTicks > 0) PanicTicks--;
        if (GuardTicks > 0) GuardTicks--;
        if (--AttemptTicks > 0) return false;
        AttemptTicks = AttemptInterval;
        return true;
    }

    public void Frighten(float threatX)
    {
        if (!float.IsFinite(threatX)) return;
        PanicTicks = PanicDuration;
        ThreatX = threatX;
    }
    public void ObserveGuard() => GuardTicks = GuardDuration;
    public void Restore(int panic, int guard, int attempt, float threatX)
    {
        PanicTicks = Math.Clamp(panic, 0, PanicDuration);
        GuardTicks = Math.Clamp(guard, 0, GuardDuration);
        AttemptTicks = Math.Clamp(attempt, 1, AttemptInterval);
        ThreatX = float.IsFinite(threatX) ? threatX : 0;
    }
    public static bool Within(Vector2 origin, Vector2 other, int tileRadius) =>
        float.IsFinite(origin.X) && float.IsFinite(origin.Y) && float.IsFinite(other.X) && float.IsFinite(other.Y) &&
        MathF.Abs(origin.X - other.X) <= tileRadius * 16 && MathF.Abs(origin.Y - other.Y) <= tileRadius * 16;
}
