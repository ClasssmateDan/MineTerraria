using System;
using DimensionalTerraria.Core;
using DimensionalTerraria.World;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using NVector2 = System.Numerics.Vector2;

namespace DimensionalTerraria.Systems;

internal static class KingSlimeBoatTrial
{
    internal static bool IsParticipant(NPC npc) => npc.type == NPCID.KingSlime;

    internal static bool CanCapture(NPC npc)
    {
        // Let native AI initialize the real size first. Never freeze either
        // half of its shrinking/teleporting animation or an active retreat.
        if (!IsParticipant(npc) || Main.netMode != NetmodeID.SinglePlayer || npc.aiStyle != NPCAIStyleID.KingSlime ||
            npc.localAI[3] == 0 || npc.ai[1] >= 5 || npc.timeLeft <= 10) return false;
        Player player = Main.LocalPlayer;
        // 3D flight temporarily sets ghost, but does not kill the player.
        return player.active && !player.dead && (player.ghost || Vector2.DistanceSquared(player.Center, npc.Center) <= 3000f * 3000f);
    }

    internal static void OnReleased(NPC npc)
    {
        if (!IsParticipant(npc) || !npc.active || npc.life <= 0) return;
        // Restart a normal jump windup, leaving life, scale and ai[3]'s native
        // slime-spawn damage threshold intact. Do not resume a stale teleport.
        npc.ai[0] = -60; npc.ai[1] = 0; npc.ai[2] = 0;
        npc.localAI[0] = npc.localAI[1] = npc.localAI[2] = 0;
        npc.target = Main.myPlayer;
        npc.targetRect = Main.LocalPlayer.Hitbox;
    }

    internal static string Spawn(Vector2 center, IExplorationMap? exploration)
    {
        if (Main.netMode != NetmodeID.SinglePlayer) return "InGameOnly";
        NPC prototype = new();
        prototype.SetDefaults(NPCID.KingSlime);
        float seedScale = Main.getGoodWorld ? 2 : 1;
        NVector2 size = new(MathF.Max(prototype.width, 98 * prototype.scale * seedScale) / 16,
            MathF.Max(prototype.height, 92 * prototype.scale * seedScale) / 16);
        var world = new TerrariaCollisionWorld();
        int x = (int)(center.X / 16), feet = (int)((center.Y + Main.LocalPlayer.height / 2f) / 16);
        for (int radius = 0; radius <= 12; radius++)
        for (int dx = -radius; dx <= radius; dx++)
        for (int dy = -radius; dy <= radius; dy++)
        {
            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius ||
                !GroundSpawnRules.Position(world, x + dx, feet + dy, size, out var spawn, exploration)) continue;
            int slot = NPC.NewNPC(new EntitySource_Misc("DimensionalTerraria_DebugKingSlimeBoatTrial"),
                (int)(spawn.X * 16), (int)(spawn.Y * 16), NPCID.KingSlime, Target: Main.myPlayer);
            if (slot < 0 || slot >= Main.maxNPCs) return "NoNpcSlot";
            NPC king = Main.npc[slot];
            king.Bottom = new Vector2(spawn.X, spawn.Y + size.Y / 2) * 16;
            return "KingSlimeTrialSpawned";
        }
        return "KingSlimeNoSpace";
    }
}
