using DimensionalTerraria.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Utilities;

namespace DimensionalTerraria.NPCs;

public sealed class MinecraftSheep : ModNPC, IPixelCreature
{
    private int wanderTicks, restTicks;
    private int walking;
    public override string Texture => "DimensionalTerraria/Textures/Sheep";
    internal PixelSpriteAtlas Atlas => PixelSpriteAtlas.Request(Texture);
    internal float SpriteScale => 28f / Atlas.RestHeight;
    internal Vector2 SpriteOrigin => Atlas.TryFrame(CurrentFrame, out var crop) ? PixelSpriteAtlas.Origin(crop) : Vector2.Zero;
    bool IPixelCreature.SpriteReady => Atlas.Ready;
    float IPixelCreature.SpriteScale => SpriteScale;
    Rectangle IPixelCreature.SpriteFrame => Atlas.TryFrame(CurrentFrame, out var frame) ? PixelSpriteAtlas.Source(frame) : Rectangle.Empty;
    Vector2 IPixelCreature.SpriteOrigin => SpriteOrigin;
    private int CurrentFrame => NPC.GetGlobalNPC<DimensionEnemyState>().DamageFlash > 0 ? 7 :
        System.MathF.Abs(NPC.velocity.X) > 0.1f ? 1 + (int)NPC.frameCounter % 4 : restTicks > 100 ? 5 : Main.GameUpdateCount % 180 < 6 ? 6 : 0;

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 8;
        NPCID.Sets.CountsAsCritter[Type] = true;
    }

    public override void SetDefaults()
    {
        NPC.width = 34; NPC.height = 28;
        NPC.lifeMax = 50;
        NPC.defense = NPC.damage = 0;
        NPC.aiStyle = -1;
        // Terraria's "friendly" flag makes an NPC immune to ordinary player
        // weapons. Passive critters instead use zero damage and their own AI.
        NPC.friendly = false;
        NPC.npcSlots = 0;
        NPC.knockBackResist = 0.5f;
        NPC.value = 0;
        NPC.HitSound = SoundID.NPCHit1;
        NPC.DeathSound = SoundID.NPCDeath1;
    }

    public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) => NPC.lifeMax = 50;
    public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;

    public override void AI()
    {
        if (--wanderTicks <= 0)
        {
            wanderTicks = Main.rand.Next(100, 241);
            walking = Main.rand.Next(-1, 2);
            restTicks = 0;
        }
        if (walking == 0) { NPC.velocity.X *= 0.85f; restTicks++; }
        else
        {
            NPC.velocity.X = walking * 0.65f;
            NPC.direction = NPC.spriteDirection = walking;
            if (NPC.collideX && NPC.velocity.Y == 0) NPC.velocity.Y = -4.2f;
        }
    }

    public override void FindFrame(int frameHeight)
    {
        if (Main.dedServ) return;
        NPC.frameCounter += System.MathF.Abs(NPC.velocity.X) * 0.2f;
        if (Atlas.TryFrame(CurrentFrame, out var crop)) NPC.frame = PixelSpriteAtlas.Source(crop);
    }

    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        PixelSpriteAtlas atlas = Atlas;
        if (!atlas.Ready || NPC.frame.Width <= 0 || NPC.frame.Height <= 0) return false;
        Vector2 origin = SpriteOrigin;
        SpriteEffects flip = NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        if (flip != SpriteEffects.None) origin.X = NPC.frame.Width - origin.X;
        spriteBatch.Draw(atlas.Texture, NPC.Bottom - screenPos + new Vector2(0, NPC.gfxOffY), NPC.frame,
            NPC.GetAlpha(drawColor), NPC.rotation, origin, NPC.scale * SpriteScale, flip, 0);
        return false;
    }

    public override float SpawnChance(NPCSpawnInfo spawnInfo) => NPC.CountNPCS(Type) < 6 &&
        spawnInfo.SpawnTileType == TileID.Grass && !spawnInfo.Player.ZoneCorrupt && !spawnInfo.Player.ZoneCrimson
        ? SpawnCondition.OverworldDay.Chance * 0.12f : 0;
}
