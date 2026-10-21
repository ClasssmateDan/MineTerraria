using System;
using System.Numerics;

namespace DimensionalTerraria.Core;

public sealed class FlightState
{
    public Vector3 Position { get; private set; }
    public Vector2 Anchor { get; }
    public Vector2 BodySize { get; }
    public float Yaw { get; private set; }
    public float Pitch { get; private set; }
    public float Elapsed { get; private set; }
    public Vector3 BoostVelocity { get; private set; }
    public bool Departing => Elapsed < 0.35f;
    public float Fade => Math.Clamp(Elapsed / 0.2f, 0, 1);
    public Vector3 Eye => Position - new Vector3(0, BodySize.Y * 0.28f, 0);
    private readonly float entryDistance;

    public FlightState(Vector2 center, Vector2 bodySize, float entryDistance = 32f)
    {
        Anchor = center;
        BodySize = bodySize;
        this.entryDistance = float.IsFinite(entryDistance) ? Math.Clamp(entryDistance, 16f, 64f) : 32f;
        Position = new Vector3(center, this.entryDistance * 0.75f);
    }

    public Vector3 Forward => new(
        MathF.Sin(Yaw) * MathF.Cos(Pitch),
        -MathF.Sin(Pitch),
        -MathF.Cos(Yaw) * MathF.Cos(Pitch));

    public void Look(float yawDelta, float pitchDelta)
    {
        Yaw = MathF.IEEERemainder(Yaw + yawDelta, MathF.Tau);
        Pitch = Math.Clamp(Pitch + pitchDelta, -1.45f, 1.45f);
    }

    public bool UseFirework()
    {
        if (Departing) return false;
        BoostVelocity += Forward * 32f;
        if (BoostVelocity.LengthSquared() > 60f * 60f)
            BoostVelocity = Vector3.Normalize(BoostVelocity) * 60f;
        return true;
    }

    public void Update(ICollisionWorld world, float seconds, Vector3 input, bool boost, float speed = 12f)
    {
        if (!float.IsFinite(seconds) || seconds <= 0)
            return;
        seconds = MathF.Min(seconds, 0.05f);
        Elapsed += seconds;
        if (Elapsed < 0.35f)
        {
            float t = Elapsed / 0.35f;
            float eased = t * t * (3 - 2 * t);
            Position = new Vector3(Anchor, entryDistance * (0.75f + eased * 0.25f));
            return;
        }

        if (Elapsed - seconds < 0.35f)
            Position = new Vector3(Anchor, entryDistance);

        Vector3 right = new(MathF.Cos(Yaw), 0, MathF.Sin(Yaw));
        Vector3 direction = right * input.X + Vector3.UnitY * input.Y + Forward * input.Z;
        if (direction.LengthSquared() > 1)
            direction = Vector3.Normalize(direction);
        Vector3 displacement = (direction * Math.Clamp(speed, 2f, 40f) * (boost ? 2.5f : 1f) + BoostVelocity) * seconds;
        Vector3 previous = Position;
        Position = SpatialRules.Move(world, Position, displacement, BodySize);
        Vector3 actual = Position - previous;
        BoostVelocity *= MathF.Exp(-1.8f * seconds);
        // Discard momentum on a blocked axis; retaining it would pin the player
        // against terrain after the input direction changes.
        BoostVelocity = new Vector3(
            MathF.Abs(actual.X - displacement.X) > .001f ? 0 : BoostVelocity.X,
            MathF.Abs(actual.Y - displacement.Y) > .001f ? 0 : BoostVelocity.Y,
            MathF.Abs(actual.Z - displacement.Z) > .001f ? 0 : BoostVelocity.Z);
        if (BoostVelocity.LengthSquared() < .01f) BoostVelocity = Vector3.Zero;
    }
}
