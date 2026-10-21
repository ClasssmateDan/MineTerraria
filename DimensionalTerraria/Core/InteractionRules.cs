using System;
using System.Numerics;

namespace DimensionalTerraria.Core;

public enum DimensionAction { Mine, Pickup, Attack, UseItem, Place }
public readonly record struct InteractionContext(bool InFlight, bool SinglePlayer, bool Alive, bool Focused, bool Paused, bool InventoryOpen)
{
    public bool Allows(bool permission) => permission && InFlight && SinglePlayer && Alive && Focused && !Paused && !InventoryOpen;
}

public static class InteractionRules
{
    public const float MiningReach = 6;
    public const float PickupRadius = 6;
    public const float ItemDepth = 0.62f;

    public static bool CanReachDrop(Vector3 observer, Vector2 itemCenter, Vector2 itemSize, float radius, IExplorationMap? exploration)
    {
        if (!float.IsFinite(radius) || radius <= 0 || !float.IsFinite(observer.X) || !float.IsFinite(observer.Y) ||
            !float.IsFinite(observer.Z) || !float.IsFinite(itemCenter.X) || !float.IsFinite(itemCenter.Y) ||
            !float.IsFinite(itemSize.X) || !float.IsFinite(itemSize.Y) || itemSize.X <= 0 || itemSize.Y <= 0) return false;
        return Vector3.DistanceSquared(observer, new Vector3(itemCenter, ItemDepth)) <= radius * radius &&
            (exploration is null || ExplorationMask.Covers(exploration, itemCenter, itemSize));
    }
}

public sealed class MiningInput
{
    private bool armed;
    private int cooldown;

    public void Suspend() { armed = false; cooldown = 0; }
    public void Hit(int ticks) => cooldown = Math.Clamp(ticks, 1, 600);

    public bool Tick(bool allowed, bool held, bool hasTarget)
    {
        if (!allowed) { Suspend(); return false; }
        if (cooldown > 0) cooldown--;
        if (!held) { armed = true; return false; }
        return armed && cooldown == 0 && hasTarget;
    }
}
