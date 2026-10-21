using System;
using System.Collections.Generic;
using System.Numerics;

namespace DimensionalTerraria.Core;

public enum WeaponMode { None, Melee, Ranged, Magic }
public enum PrimaryAction { None, Mine, Attack }
public readonly record struct CombatBody(int Id, Vector2 Center, Vector2 Size, bool Eligible, bool BehindTiles = false);
public readonly record struct CombatHit(int Id, float Distance, Vector3 Point);

public static class CombatRules
{
    public const float MeleeReach = 4.5f;
    public const float ShotReach = 48;
    public const float ZenithReach = 96;
    public const float MeleeDepthWeight = 0.5f;

    // Keep intersection, occlusion and hit points in real world coordinates.
    // Only the melee range budget uses the compressed depth metric.
    public static float RayReach(Vector3 direction, float reach, float depthWeight = 1)
    {
        if (!Finite(direction) || direction.LengthSquared() < 0.000001f || !float.IsFinite(direction.LengthSquared()) ||
            !float.IsFinite(reach) || reach <= 0 || !float.IsFinite(depthWeight) || depthWeight <= 0 || depthWeight > 1) return 0;
        direction = Vector3.Normalize(direction);
        return MathF.Min(128, reach / new Vector3(direction.X, direction.Y, direction.Z * depthWeight).Length());
    }

    public static CombatHit? Target(IEnumerable<CombatBody> bodies, Vector3 eye, Vector3 direction, float reach,
        float obstructionDistance, IExplorationMap? exploration, float depthWeight = 1)
    {
        if (!Finite(eye) || !Finite(direction) || direction.LengthSquared() < 0.000001f ||
            !float.IsFinite(direction.LengthSquared()) || !float.IsFinite(reach) || reach <= 0 ||
            !float.IsFinite(obstructionDistance) || obstructionDistance < 0) return null;
        direction = Vector3.Normalize(direction);
        float closest = MathF.Min(RayReach(direction, reach, depthWeight), obstructionDistance);
        CombatHit? hit = null;
        foreach (CombatBody body in bodies)
        {
            if (!body.Eligible || !float.IsFinite(body.Center.X) || !float.IsFinite(body.Center.Y) ||
                !float.IsFinite(body.Size.X) || !float.IsFinite(body.Size.Y) || body.Size.X <= 0 || body.Size.Y <= 0) continue;
            if (exploration is not null && !ExplorationMask.Covers(exploration, body.Center, body.Size)) continue;
            float z = body.BehindTiles ? 0 : 0.57f;
            Vector2 half = body.Size * 0.5f;
            Vector3 min = new(body.Center - half, z - 0.025f), max = new(body.Center + half, z + 0.025f);
            if (!TileRaycast.Intersect(eye, direction, min, max, closest, out float distance) || distance >= closest - 0.00001f) continue;
            closest = distance;
            hit = new CombatHit(body.Id, distance, eye + direction * distance);
        }
        return hit;
    }

    public static PrimaryAction Choose(bool mineAllowed, bool mineTarget, float? tileDistance, bool attackAllowed,
        bool hasWeapon, float? enemyDistance)
    {
        if (attackAllowed && hasWeapon && enemyDistance is { } enemy && (tileDistance is null || enemy < tileDistance)) return PrimaryAction.Attack;
        if (mineAllowed && mineTarget) return PrimaryAction.Mine;
        return attackAllowed && hasWeapon ? PrimaryAction.Attack : PrimaryAction.None;
    }

    private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
}
