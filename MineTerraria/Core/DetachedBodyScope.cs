using System;
using System.Numerics;

namespace DimensionalTerraria.Core;

public readonly record struct PlayerBodyState(Vector2 Position, Vector2 Velocity, bool Ghost, bool Invisible,
    bool Immune, int ItemAnimation, int ItemTime);

public interface IPlayerBody
{
    PlayerBodyState State { get; set; }
}

// Only lease the engine's non-interactive presence during simulation. Restoring in
// finally prevents temporary spectator flags from leaking into saves or 2D play.
public sealed class DetachedBodyScope : IDisposable
{
    private IPlayerBody? body;
    private readonly PlayerBodyState saved;

    public DetachedBodyScope(IPlayerBody body)
    {
        this.body = body;
        saved = body.State;
        body.State = saved with { Velocity = Vector2.Zero, Ghost = true, Invisible = true, Immune = true, ItemAnimation = 0, ItemTime = 0 };
    }

    public void Dispose()
    {
        if (body is null) return;
        body.State = saved;
        body = null;
    }
}
