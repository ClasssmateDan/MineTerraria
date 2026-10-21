using System;
using System.Collections.Generic;
using System.Numerics;

namespace DimensionalTerraria.Core;

public readonly record struct GolemTarget(int Id, bool Player, Vector2 Center, Vector2 Size, bool Eligible, bool Visible);
public readonly record struct GolemMotion(float VelocityX, int Facing, int Frame, GolemTarget? Strike);

// All distances are pixels. This controller never writes vertical velocity:
// the native NPC update supplies gravity and collisions, but no jumping AI.
public sealed class IronGolemBrain
{
    public const int Width = 44, Height = 76, Life = 500, Defense = 20, Damage = 45;
    public const float EnemyRange = 20 * 16, PlayerNoticeRange = 40 * 16, PlayerLoseRange = 64 * 16;
    public const int AngerDuration = 30 * 60, WindupTicks = 10, AttackTicks = 42;
    private int angerTicks, attackTicks, wanderTicks, walkTicks, facing = 1;
    private int aggressor = -1;
    private bool pursuingPlayer;
    private GolemTarget? lockedTarget;
    public int AngryPlayer => angerTicks > 0 ? aggressor : -1;
    public int AngerTicks => angerTicks;

    public void Retaliate(int player, bool hostilesNearby)
    {
        if (hostilesNearby) { ClearPlayerAggro(); return; }
        if (player < 0) return;
        if (aggressor != player) pursuingPlayer = false;
        aggressor = player; angerTicks = AngerDuration;
        // Finish the current swing rather than teleporting its hit to a new victim.
    }

    private void ClearPlayerAggro()
    {
        aggressor = -1; angerTicks = 0; pursuingPlayer = false;
        // An enemy arriving during a player windup cancels that swing completely.
        // A swing already aimed at a monster keeps its original target and timing.
        if (lockedTarget is { Player: true }) { attackTicks = 0; lockedTarget = null; }
    }

    public GolemMotion Step(Vector2 center, float velocityX, bool grounded, bool canLeft, bool canRight, IReadOnlyList<GolemTarget> candidates)
    {
        if (angerTicks > 0 && --angerTicks == 0) aggressor = -1;
        GolemTarget? player = null, enemy = null;
        bool hostilesNearby = false;
        float closestEnemy = EnemyRange * EnemyRange;
        foreach (GolemTarget candidate in candidates)
        {
            if (!Valid(candidate)) continue;
            float distance = Vector2.DistanceSquared(center, candidate.Center);
            if (candidate.Player)
            {
                if (candidate.Id != AngryPlayer) continue;
                if (distance > PlayerLoseRange * PlayerLoseRange) { angerTicks = 0; aggressor = -1; continue; }
                if (candidate.Visible && (pursuingPlayer || distance <= PlayerNoticeRange * PlayerNoticeRange)) player = candidate;
            }
            else if (distance <= EnemyRange * EnemyRange)
            {
                hostilesNearby = true;
                if (candidate.Visible && (enemy is null || distance < closestEnemy)) { closestEnemy = distance; enemy = candidate; }
            }
        }
        // Suppression includes temporarily occluded monsters; old accidental hits
        // must not become a delayed grudge when the final enemy dies or moves away.
        if (hostilesNearby) { ClearPlayerAggro(); player = null; }
        GolemTarget? chosen = enemy ?? player;
        pursuingPlayer = player is not null;
        if (attackTicks > 0)
        {
            attackTicks++;
            GolemTarget? strike = null;
            if (attackTicks == WindupTicks)
                foreach (GolemTarget candidate in candidates)
                    if (lockedTarget is { } target && candidate.Id == target.Id && candidate.Player == target.Player &&
                        Valid(candidate) && candidate.Visible && (!candidate.Player || candidate.Id == AngryPlayer) && CanReach(center, candidate))
                    { strike = candidate; break; }
            int frame = attackTicks < WindupTicks ? 5 : attackTicks <= WindupTicks + 8 ? 6 : 0;
            if (attackTicks >= AttackTicks) { attackTicks = 0; lockedTarget = null; }
            float brakedVelocity = Brake(velocityX);
            if (brakedVelocity < 0 && !canLeft || brakedVelocity > 0 && !canRight) brakedVelocity = 0;
            return new(brakedVelocity, facing, frame, strike);
        }
        float desired = 0;
        if (chosen is { } victim)
        {
            if (MathF.Abs(victim.Center.X - center.X) > 2) facing = victim.Center.X > center.X ? 1 : -1;
            if (CanReach(center, victim)) { attackTicks = 1; lockedTarget = victim; return new(Brake(velocityX), facing, 5, null); }
            if (MathF.Abs(victim.Center.X - center.X) > Width / 2 + victim.Size.X / 2 + 12) desired = facing * 1.25f;
        }
        else
        {
            wanderTicks = (wanderTicks + 1) % 300;
            if (wanderTicks >= 180) { facing = wanderTicks < 240 ? 1 : -1; desired = facing * 0.45f; }
        }
        if (desired < 0 && !canLeft || desired > 0 && !canRight) desired = 0;
        float velocity = desired == 0 ? Brake(velocityX) : velocityX + Math.Clamp(desired - velocityX, -0.1f, 0.1f);
        if (velocity < 0 && !canLeft || velocity > 0 && !canRight) velocity = 0;
        int pose = grounded && MathF.Abs(velocity) > 0.08f ? 1 + (++walkTicks / 9) % 4 : 0;
        return new(velocity, facing, pose, null);
    }

    public static bool CanReach(Vector2 center, GolemTarget target) => Valid(target) &&
        MathF.Abs(target.Center.X - center.X) <= Width / 2f + target.Size.X / 2 + 30 &&
        MathF.Abs(target.Center.Y - center.Y) <= Height / 2f + target.Size.Y / 2 + 8;

    public static Vector2 LaunchVelocity(Vector2 velocity, float resistance, int direction)
    {
        if (!float.IsFinite(resistance) || resistance <= 0) return velocity;
        resistance = Math.Clamp(resistance, 0, 1);
        return new(direction * 4 * resistance, MathF.Min(velocity.Y, -8 * resistance));
    }

    private static bool Valid(GolemTarget target) => target.Eligible && float.IsFinite(target.Center.X) &&
        float.IsFinite(target.Center.Y) && float.IsFinite(target.Size.X) && float.IsFinite(target.Size.Y) && target.Size.X > 0 && target.Size.Y > 0;
    private static float Brake(float velocity) => MathF.Abs(velocity) < 0.06f ? 0 : velocity * 0.75f;
}
