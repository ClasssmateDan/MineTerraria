using System;
using System.Collections.Generic;
using DimensionalTerraria.Core;
using DimensionalTerraria.Rendering;
using DimensionalTerraria.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using NVector2 = System.Numerics.Vector2;

namespace DimensionalTerraria.NPCs;

public sealed class MinecraftIronGolem : ModNPC, IPixelCreature
{
    private IronGolemBrain brain = new();
    private readonly List<GolemTarget> candidates = new();
    private int pose, hurtTicks;
    internal bool VillageGuardian;
    public override string Texture => "DimensionalTerraria/Textures/IronGolem";
    private PixelSpriteAtlas Atlas => PixelSpriteAtlas.Request(Texture);
    bool IPixelCreature.SpriteReady => Atlas.Ready;
    float IPixelCreature.SpriteScale => IronGolemBrain.Height / Atlas.RestHeight;
    Rectangle IPixelCreature.SpriteFrame => Atlas.TryFrame(CurrentFrame, out var frame) ? PixelSpriteAtlas.Source(frame) : Rectangle.Empty;
    Vector2 IPixelCreature.SpriteOrigin => Atlas.TryFrame(CurrentFrame, out var frame) ? PixelSpriteAtlas.Origin(frame) : Vector2.Zero;
    private int CurrentFrame => hurtTicks > 0 ? 7 : pose;

    public override void SetStaticDefaults()
    {
        Main.npcFrameCount[Type] = 8;
        NPCID.Sets.CountsAsCritter[Type] = false;
        NPCID.Sets.TakesDamageFromHostilesWithoutBeingFriendly[Type] = true;
    }

    public override void SetDefaults()
    {
        brain = new(); candidates.Clear(); pose = hurtTicks = 0;
        VillageGuardian = false;
        NPC.width = IronGolemBrain.Width; NPC.height = IronGolemBrain.Height;
        NPC.lifeMax = IronGolemBrain.Life; NPC.defense = IronGolemBrain.Defense;
        NPC.damage = 0; // Damage is dealt by the telegraphed uppercut, never by touch.
        NPC.aiStyle = -1;
        NPC.friendly = false; // Neutral NPCs must still accept deliberate player hits.
        NPC.chaseable = false; // Do not draw friendly homing/minion attacks while neutral.
        NPC.npcSlots = 0; NPC.value = 0;
        NPC.knockBackResist = 0.1f;
        NPC.HitSound = SoundID.NPCHit4; NPC.DeathSound = SoundID.NPCDeath14;
    }

    public override void ApplyDifficultyAndPlayerScaling(int numPlayers, float balance, float bossAdjustment) => NPC.lifeMax = IronGolemBrain.Life;
    public override void ModifyNPCLoot(NPCLoot npcLoot) => npcLoot.Add(ItemDropRule.Common(ItemID.IronBar, 1, 3, 5));
    public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;
    public override bool CanHitNPC(NPC target) => false;
    public override bool CheckActive() => false;
    public override bool NeedSaving() => VillageGuardian;
    public override void SaveData(TagCompound tag)
    {
        if (VillageGuardian) tag["VillageGuardian"] = true;
        tag["Life"] = NPC.life;
    }
    public override void LoadData(TagCompound tag)
    {
        VillageGuardian = tag.GetBool("VillageGuardian");
        if (tag.ContainsKey("Life")) NPC.life = Math.Clamp(tag.GetInt("Life"), 1, NPC.lifeMax);
    }
    public override bool? CanBeHitByProjectile(Projectile projectile) => projectile.hostile ? true : null;

    internal static bool HostileTarget(NPC npc) => npc.active && npc.life > 0 && npc.damage > 0 &&
        !npc.friendly && !npc.townNPC && !npc.dontTakeDamage && !npc.hide && npc.catchItem == 0 &&
        npc.ModNPC is not (MinecraftIronGolem or MinecraftSheep or MinecraftVillager or DebugTargetDummy);

    private static bool Available(Player player) => player.active && !player.dead && !player.ghost && !DimensionSystem.IsDetached(player);

    private bool Visible(Vector2 position, int width, int height) => Collision.CanHitLine(NPC.position, NPC.width, NPC.height, position, width, height);

    private bool HasNearbyHostile()
    {
        // Check live entities at the time of the hit, including before this NPC's AI
        // has run this tick. Suppression does not require line of sight.
        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC other = Main.npc[i];
            if (HostileTarget(other) && Vector2.DistanceSquared(NPC.Center, other.Center) <= IronGolemBrain.EnemyRange * IronGolemBrain.EnemyRange)
                return true;
        }
        return false;
    }

    private void Retaliate(int player)
    {
        if (player < 0 || player >= Main.maxPlayers) return;
        brain.Retaliate(player, HasNearbyHostile());
        NPC.chaseable = brain.AngryPlayer >= 0;
    }

    public override void AI()
    {
        if (hurtTicks > 0) hurtTicks--;
        candidates.Clear();
        int angry = brain.AngryPlayer;
        if (angry >= 0 && angry < Main.maxPlayers)
        {
            Player player = Main.player[angry];
            bool available = Available(player);
            candidates.Add(new(angry, true, new(player.Center.X, player.Center.Y), new(player.width, player.height),
                available, available && Visible(player.position, player.width, player.height)));
        }
        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC other = Main.npc[i];
            if (i == NPC.whoAmI || !HostileTarget(other) || Vector2.DistanceSquared(NPC.Center, other.Center) > IronGolemBrain.EnemyRange * IronGolemBrain.EnemyRange) continue;
            candidates.Add(new(i, false, new(other.Center.X, other.Center.Y), new(other.width, other.height), true, Visible(other.position, other.width, other.height)));
        }
        bool grounded = NPC.velocity.Y == 0 || NPC.collideY && NPC.velocity.Y >= 0;
        GolemMotion motion = brain.Step(new(NPC.Center.X, NPC.Center.Y), NPC.velocity.X, grounded,
            CanWalk(-1, grounded), CanWalk(1, grounded), candidates);
        NPC.velocity.X = motion.VelocityX;
        // NPC.velocity.Y is intentionally untouched: cannot jump or climb steps.
        NPC.direction = NPC.spriteDirection = motion.Facing;
        NPC.target = TargetExclusionScope.NoPlayer;
        NPC.targetRect = Rectangle.Empty;
        NPC.chaseable = brain.AngryPlayer >= 0;
        pose = motion.Frame;
        if (motion.Strike is { } victim && Main.netMode != NetmodeID.MultiplayerClient) Uppercut(victim);
    }

    private bool CanWalk(int direction, bool grounded)
    {
        if (Collision.SolidCollision(NPC.position + new Vector2(direction * 3, 0), NPC.width, NPC.height)) return false;
        if (!grounded) return true;
        int x = (int)((direction < 0 ? NPC.Left.X - 4 : NPC.Right.X + 4) / 16), y = (int)((NPC.Bottom.Y + 8) / 16);
        for (int depth = 0; depth < 2; depth++)
        {
            if (!WorldGen.InWorld(x, y + depth, 10)) return false;
            Tile tile = Main.tile[x, y + depth];
            if (tile.HasUnactuatedTile && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType])) return true;
        }
        return false;
    }

    private void Uppercut(GolemTarget victim)
    {
        int direction = victim.Center.X >= NPC.Center.X ? 1 : -1;
        bool landed = false;
        if (victim.Player && victim.Id >= 0 && victim.Id < Main.maxPlayers)
        {
            Player player = Main.player[victim.Id];
            int cooldown = ImmunityCooldownID.General;
            if (!Available(player) || !PlayerLoader.CanBeHitByNPC(player, NPC, ref cooldown)) return;
            landed = player.Hurt(PlayerDeathReason.ByNPC(NPC.whoAmI), IronGolemBrain.Damage, direction, cooldownCounter: cooldown) > 0;
            if (landed && !player.noKnockback)
            {
                NVector2 velocity = IronGolemBrain.LaunchVelocity(new(player.velocity.X, player.velocity.Y), 1, direction);
                player.velocity = new(velocity.X, velocity.Y);
            }
        }
        else if (!victim.Player && victim.Id >= 0 && victim.Id < Main.maxNPCs)
        {
            NPC other = Main.npc[victim.Id];
            if (!HostileTarget(other)) return;
            landed = other.SimpleStrikeNPC(IronGolemBrain.Damage, direction, knockBack: 6, damageType: DamageClass.Melee, noPlayerInteraction: true) > 0;
            if (landed && other.active)
            {
                NVector2 velocity = IronGolemBrain.LaunchVelocity(new(other.velocity.X, other.velocity.Y), other.knockBackResist, direction);
                other.velocity = new(velocity.X, velocity.Y);
                other.netUpdate = true;
            }
        }
        if (landed) SoundEngine.PlaySound(SoundID.Item1, NPC.Center);
    }

    public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone)
    {
        if (damageDone > 0) Retaliate(player.whoAmI);
    }
    public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone)
    {
        // In single player even enemy/NPC/trap projectiles can have owner == 0.
        // Only a friendly player-owned weapon may provoke player retaliation.
        if (damageDone > 0 && projectile.friendly && !projectile.hostile && !projectile.npcProj && !projectile.trap &&
            projectile.owner >= 0 && projectile.owner < Main.maxPlayers && Main.player[projectile.owner].active)
            Retaliate(projectile.owner);
    }
    public override void HitEffect(NPC.HitInfo hit) { if (hit.Damage > 0) hurtTicks = 8; }

    public override void FindFrame(int frameHeight)
    {
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
            NPC.GetAlpha(drawColor), NPC.rotation, origin, NPC.scale * IronGolemBrain.Height / atlas.RestHeight, flip, 0);
        return false;
    }
}
