using System;
using System.Collections.Generic;
using System.Numerics;

namespace DimensionalTerraria.Core;

public sealed class DamagePopup(Vector3 origin, int damage, bool critical, Vector3 visibilityOrigin)
{
    public Vector3 Origin { get; } = origin;
    public Vector3 VisibilityOrigin { get; } = visibilityOrigin;
    public int Damage { get; } = damage;
    public bool Critical { get; } = critical;
    public float Age { get; private set; }
    public float Duration => Critical ? 1.1f : 0.85f;
    public Vector3 Position => Origin - Vector3.UnitY * (Age * 0.85f);
    public float Opacity => Math.Clamp((Duration - Age) / 0.25f, 0, 1);
    public bool Active => Age < Duration;
    public void Update(float seconds) => Age += seconds;
}

public sealed class CombatFeedback
{
    private readonly List<DamagePopup> popups = new();
    public IReadOnlyList<DamagePopup> Popups => popups;
    public long Events { get; private set; }

    public void Add(Vector3 origin, int damage, bool critical, Vector3? visibilityOrigin = null)
    {
        if (damage <= 0 || !float.IsFinite(origin.X) || !float.IsFinite(origin.Y) || !float.IsFinite(origin.Z)) return;
        if (visibilityOrigin is { } sight && (!float.IsFinite(sight.X) || !float.IsFinite(sight.Y) || !float.IsFinite(sight.Z))) return;
        if (popups.Count == 64) popups.RemoveAt(0);
        popups.Add(new(origin, damage, critical, visibilityOrigin ?? origin));
        Events++;
    }

    public void Update(float seconds)
    {
        if (!float.IsFinite(seconds) || seconds <= 0) return;
        for (int i = popups.Count - 1; i >= 0; i--)
        {
            popups[i].Update(seconds);
            if (!popups[i].Active) popups.RemoveAt(i);
        }
    }

    public void Clear() => popups.Clear();
}

public static class CreatureOverlayRules
{
    public static float HealthFraction(int life, int maximum) => maximum <= 0 ? 0 : Math.Clamp(life / (float)maximum, 0, 1);

    // Input uses rendering coordinates (positive Y up); output is UI coordinates.
    public static bool Project(Vector3 position, Matrix4x4 viewProjection, float width, float height, out Vector2 screen)
    {
        screen = default;
        if (!float.IsFinite(width) || !float.IsFinite(height) || width <= 0 || height <= 0) return false;
        Vector4 clip = Vector4.Transform(new Vector4(position, 1), viewProjection);
        if (!float.IsFinite(clip.X) || !float.IsFinite(clip.Y) || !float.IsFinite(clip.Z) || !float.IsFinite(clip.W) ||
            clip.W <= 0 || clip.Z < 0 || clip.Z > clip.W || MathF.Abs(clip.X) > clip.W || MathF.Abs(clip.Y) > clip.W) return false;
        screen = new((clip.X / clip.W + 1) * width / 2, (1 - clip.Y / clip.W) * height / 2);
        return true;
    }
}
