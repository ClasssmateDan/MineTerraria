using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DimensionalTerraria.NPCs;

public sealed class DebugTargetDummy : ModNPC
{
    public override string Texture => $"Terraria/Images/NPC_{NPCID.TargetDummy}";

    public override void SetStaticDefaults()
    {
        // The vanilla texture is an eleven-frame sheet, not one tall sprite.
        Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.TargetDummy];
        NPCID.Sets.DontDoHardmodeScaling[Type] = true;
    }

    public override void SetDefaults()
    {
        NPC.width = 32;
        NPC.height = 48;
        NPC.lifeMax = 9999;
        NPC.defense = NPC.damage = 0;
        NPC.knockBackResist = 0;
        NPC.aiStyle = -1;
        NPC.noGravity = NPC.noTileCollide = NPC.lavaImmune = true;
        NPC.friendly = NPC.dontTakeDamage = NPC.immortal = false;
        NPC.dontCountMe = true;
        NPC.npcSlots = 0;
        NPC.value = 0;
        NPC.HitSound = SoundID.NPCHit1;
        NPC.DeathSound = SoundID.NPCDeath1;
    }

    public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment)
    {
        NPC.lifeMax = 9999;
        NPC.defense = NPC.damage = 0;
        NPC.knockBackResist = 0;
    }

    public override void AI() => NPC.velocity = Vector2.Zero;
    public override void FindFrame(int frameHeight)
    {
        NPC.frame.Y = 0;
        NPC.frame.Height = frameHeight;
    }
    public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;
    public override bool CheckActive() => false;
    public override bool CheckDead()
    {
        Mod.Logger.Info($"Debug dummy defeated: slot={NPC.whoAmI}; active={NPC.active}; life={NPC.life}");
        return true;
    }
    public override bool PreKill() => false;
}
