using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace DimensionalTerraria.Systems;

internal static class GroundBossCapture
{
    internal static bool IsBoss(NPC npc) => npc.boss || npc.type > NPCID.None && npc.type < NPCID.Sets.ShouldBeCountedAsBoss.Length && NPCID.Sets.ShouldBeCountedAsBoss[npc.type];
    internal static bool Allowed(NPC npc)
    {
        // Deerclops walks with custom terrain/gravity code despite both native
        // flags being true. Queen Slime's second half is explicitly airborne.
        bool grounded = npc.type == NPCID.Deerclops ? npc.ai[0] < 6 : !npc.noGravity && !npc.noTileCollide;
        if (!IsBoss(npc) || !grounded || MultipartRoot(npc.type)) return false;
        if (npc.type == NPCID.KingSlime && !KingSlimeBoatTrial.CanCapture(npc)) return false;
        if (npc.type == NPCID.QueenSlimeBoss && (npc.life <= npc.lifeMax / 2 || npc.localAI[0] == 0 || npc.ai[0] is 2 or 3)) return false;
        if (npc.timeLeft <= 10) return false;
        Player player = Main.LocalPlayer;
        return player.active && !player.dead && (player.ghost || Vector2.DistanceSquared(player.Center, npc.Center) <= 3000f * 3000f);
    }
    private static bool MultipartRoot(int type) => type is NPCID.Golem or NPCID.Plantera or NPCID.SkeletronHead or
        NPCID.SkeletronPrime or NPCID.WallofFlesh or NPCID.MoonLordCore or NPCID.BrainofCthulhu;

    internal static void OnReleased(NPC npc, Vector2 velocity)
    {
        if (!IsBoss(npc) || !npc.active || npc.life <= 0) return;
        if (npc.type == NPCID.KingSlime) KingSlimeBoatTrial.OnReleased(npc);
        else
        {
            npc.velocity = velocity;
            npc.target = Main.myPlayer;
            npc.targetRect = Main.LocalPlayer.Hitbox;
        }
    }
}
