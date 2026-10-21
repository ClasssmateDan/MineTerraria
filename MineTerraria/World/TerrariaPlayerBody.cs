using DimensionalTerraria.Core;
using Microsoft.Xna.Framework;
using Terraria;
using NVector2 = System.Numerics.Vector2;

namespace DimensionalTerraria.World;

internal sealed class TerrariaPlayerBody(Player player) : IPlayerBody, ITargetablePlayer
{
    public TargetPresence Presence
    {
        get => new(player.active, player.dead, player.ghost);
        set { player.active = value.Active; player.dead = value.Dead; player.ghost = value.Ghost; }
    }

    public PlayerBodyState State
    {
        get => new(new NVector2(player.position.X, player.position.Y), new NVector2(player.velocity.X, player.velocity.Y),
            player.ghost, player.invis, player.immune, player.itemAnimation, player.itemTime);
        set
        {
            player.position = new Vector2(value.Position.X, value.Position.Y);
            player.velocity = new Vector2(value.Velocity.X, value.Velocity.Y);
            player.ghost = value.Ghost;
            player.invis = value.Invisible;
            player.immune = value.Immune;
            player.itemAnimation = value.ItemAnimation;
            player.itemTime = value.ItemTime;
        }
    }
}
