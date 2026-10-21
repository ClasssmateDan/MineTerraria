using System;
using System.Numerics;

namespace DimensionalTerraria.Core;

public enum IdleMovement { Stationary, Walk, Hop, Fly, Swim }

public readonly record struct IdleObservation(Vector2 Position, Vector2 Velocity, bool Grounded,
    bool BlockedX = false, bool BlockedY = false, bool CanLeft = true, bool CanRight = true,
    bool CanUp = true, bool CanDown = true, bool UsesGravity = true);
public readonly record struct IdleMotion(Vector2 Velocity, int Facing);

// An explicit no-target controller. Vanilla's combat AI assumes there is a
// player somewhere and otherwise keeps walking/flying toward its sentinel.
public sealed class IdleWander
{
    public Vector2 Home { get; }
    public bool Wandering { get; private set; }
    private uint random;
    private int phaseTicks;
    private int hopCooldown;
    private int direction;
    private bool firstStep = true;
    private Vector2 destination;

    public IdleWander(Vector2 home, uint seed)
    {
        Home = destination = home;
        random = seed == 0 ? 1u : seed;
        direction = Next(2) == 0 ? -1 : 1;
        phaseTicks = 60 + Next(90);
    }

    public IdleMotion Step(IdleMovement movement, IdleObservation view)
    {
        Vector2 velocity = view.Velocity;
        if (firstStep)
        {
            velocity.X = Math.Clamp(velocity.X, -1.5f, 1.5f);
            if (movement is IdleMovement.Fly or IdleMovement.Swim) velocity.Y = Math.Clamp(velocity.Y, -1.5f, 1.5f);
            firstStep = false;
        }
        if (--phaseTicks <= 0)
        {
            Wandering = !Wandering;
            phaseTicks = Wandering ? 60 + Next(100) : 75 + Next(135);
            direction = Next(2) == 0 ? -1 : 1;
            destination = Wandering ? Home + new Vector2(Next(145) - 72, Next(81) - 40)
                : Vector2.Clamp(view.Position, Home - new Vector2(72, 40), Home + new Vector2(72, 40));
        }
        if (hopCooldown > 0) hopCooldown--;

        if (movement == IdleMovement.Stationary)
            return new IdleMotion(new Vector2(Approach(velocity.X, 0, 0.2f), view.UsesGravity ? velocity.Y : Approach(velocity.Y, 0, 0.2f)), direction);

        if (movement is IdleMovement.Fly or IdleMovement.Swim)
        {
            if (view.BlockedX || view.BlockedY) destination = Home;
            float maxSpeed = movement == IdleMovement.Swim ? 0.7f : 1.1f;
            Vector2 desired = (destination - view.Position) * 0.025f;
            if (desired.LengthSquared() > maxSpeed * maxSpeed) desired = Vector2.Normalize(desired) * maxSpeed;
            velocity.X = Approach(velocity.X, desired.X, 0.07f);
            velocity.Y = Approach(velocity.Y, desired.Y, 0.07f);
            if (velocity.X < 0 && !view.CanLeft || velocity.X > 0 && !view.CanRight) { velocity.X = 0; destination.X = Home.X; }
            if (velocity.Y < 0 && !view.CanUp || velocity.Y > 0 && !view.CanDown) { velocity.Y = 0; destination.Y = Home.Y; }
        }
        else
        {
            bool outsideHome = MathF.Abs(view.Position.X - Home.X) > 96;
            if (outsideHome) direction = view.Position.X > Home.X ? -1 : 1;
            bool canMove = direction < 0 ? view.CanLeft : view.CanRight;
            if (view.BlockedX || !canMove)
            {
                direction = -direction;
                canMove = direction < 0 ? view.CanLeft : view.CanRight;
            }
            bool move = (Wandering || outsideHome) && canMove;
            if (movement == IdleMovement.Hop)
            {
                if (view.Grounded)
                {
                    velocity.X = Approach(velocity.X, 0, 0.15f);
                    if (move && hopCooldown == 0)
                    {
                        velocity = new Vector2(direction * 1.15f, -4.5f);
                        hopCooldown = 50 + Next(40);
                    }
                }
                else if (outsideHome) velocity.X = Approach(velocity.X, direction * 1.15f, 0.06f);
            }
            else velocity.X = Approach(velocity.X, move ? direction * 0.8f : 0, 0.08f);
            if (view.Grounded && (velocity.X < 0 && !view.CanLeft || velocity.X > 0 && !view.CanRight)) velocity.X = 0;
        }
        if (MathF.Abs(velocity.X) > 0.05f) direction = velocity.X > 0 ? 1 : -1;
        return new IdleMotion(velocity, direction);
    }

    private int Next(int maximum)
    {
        random ^= random << 13; random ^= random >> 17; random ^= random << 5;
        return (int)(random % (uint)maximum);
    }

    private static float Approach(float value, float target, float amount) =>
        value < target ? MathF.Min(value + amount, target) : MathF.Max(value - amount, target);
}
