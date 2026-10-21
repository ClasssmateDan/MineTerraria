using System;
using System.Numerics;

namespace DimensionalTerraria.Core;

public static class EnemyPerception
{
    public static bool CanTarget(Vector2 enemy, Vector2 player, TargetPresence presence, bool detached,
        bool alreadyTracking, bool visible, float noticeDistance, float loseDistance)
    {
        if (detached || !presence.Active || presence.Dead || presence.Ghost ||
            !float.IsFinite(enemy.X) || !float.IsFinite(enemy.Y) || !float.IsFinite(player.X) || !float.IsFinite(player.Y) ||
            !float.IsFinite(noticeDistance) || !float.IsFinite(loseDistance) || noticeDistance <= 0 || loseDistance <= 0) return false;
        float range = alreadyTracking ? MathF.Max(noticeDistance, loseDistance) : noticeDistance;
        return Vector2.DistanceSquared(enemy, player) <= range * range && (alreadyTracking || visible);
    }
}
