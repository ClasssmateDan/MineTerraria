using DimensionalTerraria.Core;
using DimensionalTerraria.NPCs;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace DimensionalTerraria.Systems;

public sealed partial class DimensionSystem
{
    private static void UpdateIdleEnemy(NPC npc, DimensionEnemyState state)
    {
        IdleMovement movement = npc.aiStyle switch
        {
            NPCAIStyleID.Slime => IdleMovement.Hop,
            NPCAIStyleID.Piranha or NPCAIStyleID.Jellyfish => npc.wet ? IdleMovement.Swim : IdleMovement.Stationary,
            NPCAIStyleID.FaceClosestPlayer or NPCAIStyleID.Worm or NPCAIStyleID.Caster or NPCAIStyleID.Spell or
                NPCAIStyleID.ManEater or NPCAIStyleID.Vulture or NPCAIStyleID.Antlion or NPCAIStyleID.SpikeBall or
                NPCAIStyleID.BlazingWheel or NPCAIStyleID.Mimic => IdleMovement.Stationary,
            _ => npc.noGravity ? IdleMovement.Fly : IdleMovement.Walk
        };
        state.Idle ??= new IdleWander(new(npc.Center.X, npc.Center.Y),
            (uint)(npc.whoAmI + 1) * 2654435761u + (uint)Main.GameUpdateCount);
        bool grounded = npc.velocity.Y == 0 || npc.collideY && npc.velocity.Y >= 0;
        bool canLeft = true, canRight = true, canUp = true, canDown = true;
        if (movement is IdleMovement.Walk or IdleMovement.Hop && grounded)
        {
            canLeft = HasFooting(npc.Left.X - 8, npc.Bottom.Y);
            canRight = HasFooting(npc.Right.X + 8, npc.Bottom.Y);
        }
        if (movement == IdleMovement.Swim)
        {
            canLeft = HasWater(npc.Left.X - 8, npc.Center.Y);
            canRight = HasWater(npc.Right.X + 8, npc.Center.Y);
            canUp = HasWater(npc.Center.X, npc.Top.Y - 8);
            canDown = HasWater(npc.Center.X, npc.Bottom.Y + 8);
        }
        IdleMotion motion = state.Idle.Step(movement, new IdleObservation(new(npc.Center.X, npc.Center.Y),
            new(npc.velocity.X, npc.velocity.Y), grounded, npc.collideX, npc.collideY,
            canLeft, canRight, canUp, canDown, !npc.noGravity));
        npc.velocity = new Vector2(motion.Velocity.X, motion.Velocity.Y);
        npc.direction = npc.spriteDirection = motion.Facing;
        npc.target = TargetExclusionScope.NoPlayer;
        npc.targetRect = Rectangle.Empty;
        // Replace only AI decisions: UpdateNPC still applies buffs, gravity,
        // collisions, animation and the normal activity/despawn checks.
    }

    private static bool HasFooting(float x, float bottom)
    {
        int tileX = (int)(x / 16), tileY = (int)((bottom + 8) / 16);
        for (int y = tileY; y <= tileY + 1; y++)
        {
            if (!WorldGen.InWorld(tileX, y, 10)) return false;
            Tile tile = Main.tile[tileX, y];
            if (tile.HasUnactuatedTile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType])) return true;
        }
        return false;
    }

    private static bool HasWater(float x, float y)
    {
        int tileX = (int)(x / 16), tileY = (int)(y / 16);
        return WorldGen.InWorld(tileX, tileY, 10) && Main.tile[tileX, tileY].LiquidAmount > 100;
    }
}
