using DimensionalTerraria.Core;
using DimensionalTerraria.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace DimensionalTerraria.NPCs;

public sealed class MinecraftVillager : ModNPC, IPixelCreature
{
    internal VillagerMemory Memory = new();
    private int wanderTicks, walking, hurtTicks;
    public override string Texture => "DimensionalTerraria/Textures/Villager";
    private PixelSpriteAtlas Atlas => PixelSpriteAtlas.Request(Texture);
    bool IPixelCreature.SpriteReady => Atlas.Ready;
    float IPixelCreature.SpriteScale => VillagerMemory.Height / Atlas.RestHeight;
    Rectangle IPixelCreature.SpriteFrame => Atlas.TryFrame(CurrentFrame, out var frame) ? PixelSpriteAtlas.Source(frame) : Rectangle.Empty;
    Vector2 IPixelCreature.SpriteOrigin => Atlas.TryFrame(CurrentFrame, out var frame) ? PixelSpriteAtlas.Origin(frame) : Vector2.Zero;
    private int CurrentFrame => hurtTicks > 0 ? 7 : Memory.Panicking ? 5 :
        System.MathF.Abs(NPC.velocity.X) > .1f ? 1 + (int)NPC.frameCounter % 4 : Main.GameUpdateCount % 180 < 6 ? 6 : 0;

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 8;
        NPCID.Sets.TakesDamageFromHostilesWithoutBeingFriendly[Type] = true;
    }
    public override void SetDefaults()
    {
        Memory = new(); wanderTicks = walking = hurtTicks = 0;
        NPC.width = VillagerMemory.Width; NPC.height = VillagerMemory.Height;
        NPC.lifeMax = VillagerMemory.Life; NPC.defense = NPC.damage = 0;
        NPC.aiStyle = -1;
        NPC.friendly = false; // Deliberate player hits still work, like our neutral golem.
        NPC.chaseable = false;
        NPC.npcSlots = NPC.value = 0;
        NPC.knockBackResist = .5f;
        NPC.HitSound = SoundID.NPCHit1; NPC.DeathSound = SoundID.NPCDeath1;
    }
    public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) => NPC.lifeMax = VillagerMemory.Life;
    public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;
    public override bool CanHitNPC(NPC target) => false;
    public override bool? CanBeHitByProjectile(Projectile projectile) => projectile.hostile ? true : null;
    public override bool CheckActive() => false;
    public override bool NeedSaving() => true;
    public override void SaveData(TagCompound tag)
    {
        tag["Panic"] = Memory.PanicTicks; tag["Guard"] = Memory.GuardTicks;
        tag["Attempt"] = Memory.AttemptTicks; tag["ThreatX"] = Memory.ThreatX;
        tag["Life"] = NPC.life;
    }
    public override void LoadData(TagCompound tag)
    {
        Memory.Restore(tag.GetInt("Panic"), tag.GetInt("Guard"), tag.GetInt("Attempt"), tag.GetFloat("ThreatX"));
        if (tag.ContainsKey("Life")) NPC.life = System.Math.Clamp(tag.GetInt("Life"), 1, NPC.lifeMax);
    }
    public override void AI()
    {
        if (hurtTicks > 0) hurtTicks--;
        if (--wanderTicks <= 0) { wanderTicks = Main.rand.Next(100, 241); walking = Main.rand.Next(-1, 2); }
        int direction = Memory.Panicking ? (NPC.Center.X >= Memory.ThreatX ? 1 : -1) : walking;
        if (direction == 0) NPC.velocity.X *= .8f;
        else
        {
            NPC.velocity.X = direction * (Memory.Panicking ? 1.8f : .45f);
            NPC.direction = NPC.spriteDirection = direction;
            if (NPC.collideX && NPC.velocity.Y == 0) NPC.velocity.Y = -5.2f;
        }
        // No damage, weapon use, projectile creation or player targeting.
        NPC.target = TargetExclusionScope.NoPlayer; NPC.targetRect = Rectangle.Empty;
    }
    public override void HitEffect(NPC.HitInfo hit)
    {
        if (hit.Damage <= 0) return;
        hurtTicks = 10;
        Memory.Frighten(NPC.Center.X - hit.HitDirection * 32);
    }
    public override void FindFrame(int frameHeight)
    {
        NPC.frameCounter += System.MathF.Abs(NPC.velocity.X) * .2f;
        if (!Main.dedServ && Atlas.TryFrame(CurrentFrame, out var crop)) NPC.frame = PixelSpriteAtlas.Source(crop);
    }
    public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
    {
        PixelSpriteAtlas atlas = Atlas;
        if (!atlas.Ready || !atlas.TryFrame(CurrentFrame, out var frame)) return false;
        Vector2 origin = PixelSpriteAtlas.Origin(frame);
        SpriteEffects flip = NPC.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        if (flip != SpriteEffects.None) origin.X = frame.Width - origin.X;
        spriteBatch.Draw(atlas.Texture, NPC.Bottom - screenPos + new Vector2(0, NPC.gfxOffY), PixelSpriteAtlas.Source(frame),
            NPC.GetAlpha(drawColor), NPC.rotation, origin, NPC.scale * VillagerMemory.Height / atlas.RestHeight, flip, 0);
        return false;
    }
}
