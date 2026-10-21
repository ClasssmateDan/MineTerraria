using System;
using System.Collections.Generic;
using System.Numerics;

namespace DimensionalTerraria.Core;

public enum SwordEffect { None, Zenith, FlyingDragon }

// Native projectile AI only has XY coordinates. These adapted attacks instead
// have an actual depth, a bounded lifetime and swept 3D collision.
public sealed class SwordFlight
{
    public const int TrailLength = 18;
    private readonly Vector3 start, end, side, returnSide;
    private readonly float duration, curve;
    private readonly Queue<Vector3> trail = new();
    private readonly HashSet<int> hitIds = new();
    public SwordEffect Effect { get; }
    public int Skin { get; }
    public Vector3 Previous { get; private set; }
    public Vector3 Position { get; private set; }
    public Vector3 Direction { get; private set; }
    public Vector3 ReturnPoint { get; }
    public float Age { get; private set; }
    public float Radius => Effect == SwordEffect.Zenith ? 0.5f : 1.25f;
    public float Opacity => Math.Clamp(Age / 0.04f, 0, 1) * Math.Clamp((duration - Age) / 0.1f, 0, 1);
    public bool Active { get; private set; } = true;
    public IEnumerable<Vector3> Trail => trail;
    public bool CanHitMore => Effect != SwordEffect.FlyingDragon || hitIds.Count < 5;

    public SwordFlight(SwordEffect effect, int skin, Vector3 start, Vector3 end, Vector3 right, float speed, int variant)
    {
        if (effect == SwordEffect.None || !Finite(start) || !Finite(end) || !Finite(right) || !float.IsFinite(speed) || speed <= 0)
            throw new ArgumentException("Invalid sword flight");
        Effect = effect; Skin = skin;
        this.start = Position = Previous = start;
        Vector3 delta = end - start;
        float distance = delta.Length();
        if (!float.IsFinite(distance) || distance < 0.001f) throw new ArgumentException("Sword flight needs a finite path");
        Direction = delta / distance;
        float reach = effect == SwordEffect.Zenith ? CombatRules.ZenithReach : CombatRules.ShotReach;
        this.end = start + Direction * Math.Min(distance, reach);
        // Keep the orbit perpendicular to the initial path, including vertical aim.
        right -= Vector3.Dot(right, Direction) * Direction;
        if (right.LengthSquared() < 0.001f) right = Vector3.Cross(Direction, MathF.Abs(Direction.Y) < 0.9f ? Vector3.UnitY : Vector3.UnitX);
        right = Vector3.Normalize(right);
        returnSide = right * (variant % 2 == 0 ? 1 : -1);
        ReturnPoint = start + returnSide * 3.5f + Direction * 3;
        Vector3 down = Vector3.Normalize(Vector3.Cross(right, Direction));
        float angle = (variant % 6) * MathF.PI / 3;
        side = right * MathF.Cos(angle) + down * MathF.Sin(angle);
        curve = Math.Clamp(distance * 0.12f, 0.6f, 4);
        duration = effect == SwordEffect.Zenith ? 0.65f + Math.Clamp((Vector3.Distance(start, this.end) - 48) / 48, 0, 1) * 0.25f :
            Vector3.Distance(start, this.end) / speed;
        trail.Enqueue(start);
    }

    public void Update(float seconds)
    {
        if (!Active || !float.IsFinite(seconds) || seconds <= 0) return;
        Previous = Position;
        Age = Math.Min(duration, Age + seconds);
        float t = Age / duration;
        if (Effect != SwordEffect.Zenith) Position = Vector3.Lerp(start, end, t);
        else if (t <= 0.5f)
            Position = start + (end - start) * MathF.Sin(MathF.PI * t) + side * (MathF.Sin(MathF.Tau * t) * curve);
        else
        {
            float back = (t - 0.5f) * 2;
            // Return through alternating left/right corridors instead of
            // converging on the camera. Both ends remain continuous.
            Position = Vector3.Lerp(end, ReturnPoint, back) + returnSide * (MathF.Sin(MathF.PI * back) * curve);
        }
        Vector3 movement = Position - Previous;
        if (movement.LengthSquared() > 0.000001f) Direction = Vector3.Normalize(movement);
        trail.Enqueue(Position);
        if (trail.Count > TrailLength) trail.Dequeue();
        Active = Age < duration;
    }

    public bool HasHit(int id) => hitIds.Contains(id);
    public void MarkHit(int id) { hitIds.Add(id); if (!CanHitMore) Stop(); }
    public void Stop() => Active = false;

    public IEnumerable<CombatHit> Sweep(IEnumerable<CombatBody> bodies, float stopDistance, IExplorationMap? exploration, IInteractionWorld? world = null)
    {
        Vector3 delta = Position - Previous;
        float length = delta.Length();
        if (length < 0.000001f) yield break;
        Vector3 direction = delta / length;
        float reach = Math.Min(length, stopDistance);
        if (!float.IsFinite(reach) || reach <= 0) yield break;
        foreach (CombatBody body in bodies)
        {
            if (!CanHitMore) yield break;
            if (!body.Eligible || HasHit(body.Id) || !float.IsFinite(body.Center.X) || !float.IsFinite(body.Center.Y) ||
                !float.IsFinite(body.Size.X) || !float.IsFinite(body.Size.Y) || body.Size.X <= 0 || body.Size.Y <= 0 ||
                exploration is not null && !ExplorationMask.Covers(exploration, body.Center, body.Size)) continue;
            float z = body.BehindTiles ? 0 : 0.57f;
            Vector3 center = new(body.Center, z), half = new(body.Size / 2, 0.025f);
            half += new Vector3(Radius);
            if (TileRaycast.Intersect(Previous, direction, center - half, center + half, reach, out float distance))
            {
                Vector3 sight = center - Previous;
                // A broad blade may overlap around a corner without its center
                // crossing the wall. Check visibility of the actual victim too.
                if (world is not null && sight.Length() > 0.0001f && TileRaycast.Cast(world, Previous, sight, sight.Length(), exploration) is not null) continue;
                yield return new(body.Id, distance, Previous + direction * distance);
            }
        }
    }

    private static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
}
