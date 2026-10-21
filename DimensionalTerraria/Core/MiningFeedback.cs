using System;
using System.Numerics;

namespace DimensionalTerraria.Core;

public sealed class MiningSwing
{
    private float elapsed;
    private float duration;
    public bool Active => duration > 0 && elapsed < duration;
    public float Amount => Active ? MathF.Sin(MathF.PI * elapsed / duration) : 0;

    public void Start(int toolTicks)
    {
        if (Active) return;
        elapsed = 0;
        duration = Math.Clamp(toolTicks / 60f, 0.10f, 0.32f);
    }
    public void Reset() { elapsed = duration = 0; }
    public void Update(float seconds)
    {
        if (float.IsFinite(seconds) && seconds > 0) elapsed = MathF.Min(elapsed + seconds, duration);
    }
}

public struct MiningDebris(Vector3 position, Vector3 velocity, float lifetime)
{
    public Vector3 Position = position;
    public Vector3 Velocity = velocity;
    public float Age;
    public readonly float Lifetime = lifetime;
    public readonly bool Active => Age < Lifetime;
    public readonly float Fade => Lifetime > 0 ? Math.Clamp(1 - Age / Lifetime, 0, 1) : 0;

    public void Update(float seconds)
    {
        if (!float.IsFinite(seconds) || seconds <= 0 || !Active) return;
        seconds = MathF.Min(seconds, 0.05f);
        Age += seconds;
        Velocity.Y += 6 * seconds;
        Position += Velocity * seconds;
    }
}
