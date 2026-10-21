using System;
using DimensionalTerraria.Configuration;
using DimensionalTerraria.Core;
using DimensionalTerraria.NPCs;
using DimensionalTerraria.World;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;

namespace DimensionalTerraria.Systems;

public sealed partial class DimensionSystem
{
    private int targetingDepth;
    private NPC? targetingNpc;
    private bool targetingPlayerAllowed;
    private TargetPresence? excludedPlayerSavePresence;
    private readonly object targetingGate = new();
    private bool ExcludeLocalTarget => Flight is not null && !Main.gameMenu && Main.netMode == NetmodeID.SinglePlayer;
    private static bool IsHostile(NPC npc) => npc.ModNPC is not (DebugTargetDummy or MinecraftSheep or MinecraftIronGolem or MinecraftVillager) && !npc.friendly && !npc.townNPC && npc.catchItem == 0 && npc.lifeMax > 1;
    private static DimensionEnemyState State(NPC npc) => npc.GetGlobalNPC<DimensionEnemyState>();
    private bool UsesTargetRules(NPC npc) => Main.netMode == NetmodeID.SinglePlayer &&
        (ExcludeLocalTarget || IsHostile(npc) && State(npc).Managed);

    private void LoadTargetingHooks()
    {
        On_NPC.AI += UpdateDetachedNpcAI;
        On_NPC.CheckActive += UpdateDetachedBossActivity;
        On_NPC.SetTargetTrackingValues += TrackTarget;
        On_NPC.TargetClosestUpgraded += FindUpgradedTarget;
        On_Player.FindClosest += FindClosestPlayer;
        On_Projectile.AI += UpdateHostileProjectileAI;
        On_Player.InternalSavePlayerFile += SaveDetachedPlayer;
    }

    private void UnloadTargetingHooks()
    {
        On_NPC.AI -= UpdateDetachedNpcAI;
        On_NPC.CheckActive -= UpdateDetachedBossActivity;
        On_NPC.SetTargetTrackingValues -= TrackTarget;
        On_NPC.TargetClosestUpgraded -= FindUpgradedTarget;
        On_Player.FindClosest -= FindClosestPlayer;
        On_Projectile.AI -= UpdateHostileProjectileAI;
        On_Player.InternalSavePlayerFile -= SaveDetachedPlayer;
    }

    private void ForgetDetachedTargets()
    {
        if (!ExcludeLocalTarget) return;
        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC npc = Main.npc[i];
            if (!npc.active) continue;
            if (IsHostile(npc))
            {
                State(npc).Managed = true;
                State(npc).EngagedPlayer = TargetExclusionScope.NoPlayer;
            }
            ForgetLocal(npc);
        }
    }

    public override void PreUpdateNPCs() => ForgetDetachedTargets();
    public override void PostUpdateNPCs()
    {
        ForgetDetachedTargets();
        for (int i = 0; i < Main.maxNPCs; i++)
            if (Main.npc[i].active && State(Main.npc[i]).DamageFlash > 0) State(Main.npc[i]).DamageFlash -= 1f / 60;
    }

    private static void ForgetLocal(NPC npc) => TargetExclusionScope.ForgetPlayer(new TerrariaAggroMemory(npc), Main.myPlayer);

    private void UpdateDetachedNpcAI(On_NPC.orig_AI original, NPC npc)
    {
        if (BoatCaptureSystem.HoldAI(npc)) return;
        if (HoldDetachedBoss(npc))
        {
            State(npc).Managed = true;
            State(npc).EngagedPlayer = TargetExclusionScope.NoPlayer;
            // Native boss AI treats an empty player target as a retreat. Keep
            // its encounter state intact until a real player can be targeted.
            // UpdateNPC still runs damage, buffs, animation and ground physics.
            ForgetLocal(npc);
            npc.target = TargetExclusionScope.NoPlayer;
            npc.targetRect = Rectangle.Empty;
            npc.velocity = Vector2.Zero;
            npc.timeLeft = Math.Max(npc.timeLeft, NPC.activeTime);
            return;
        }
        if (ExcludeLocalTarget && IsHostile(npc)) State(npc).Managed = true;
        if (!UsesTargetRules(npc)) { original(npc); return; }
        DimensionEnemyState state = State(npc);
        bool allowed = CanSelectLocal(npc);
        InTargetContext(npc, allowed, () =>
        {
            if (!allowed && IsHostile(npc))
            {
                state.EngagedPlayer = TargetExclusionScope.NoPlayer;
                ForgetLocal(npc);
                // Event enemies may fight the crystal even without a player.
                if (npc.SupportsNPCTargets) npc.TargetClosestUpgraded();
                if (!HasLivingNpcTarget(npc) && !IsBossEncounter(npc))
                {
                    UpdateIdleEnemy(npc, state);
                    return;
                }
            }
            state.Idle = null;
            if (allowed && !HasLivingNpcTarget(npc))
            {
                npc.target = Main.myPlayer;
                npc.targetRect = Main.LocalPlayer.Hitbox;
                state.EngagedPlayer = Main.myPlayer;
            }
            original(npc);
            if (allowed) state.EngagedPlayer = npc.target == Main.myPlayer ? Main.myPlayer : TargetExclusionScope.NoPlayer;
        });
    }

    private void UpdateDetachedBossActivity(On_NPC.orig_CheckActive original, NPC npc)
    {
        if (!HoldDetachedBoss(npc)) original(npc);
    }

    private bool CanSelectLocal(NPC npc)
    {
        if (targetingDepth > 0 && ReferenceEquals(targetingNpc, npc)) return targetingPlayerAllowed;
        if (ExcludeLocalTarget) return false;
        Player player = Main.LocalPlayer;
        DimensionConfig config = ModContent.GetInstance<DimensionConfig>();
        bool tracking = State(npc).EngagedPlayer == Main.myPlayer;
        // Phasing enemies and bosses keep their native sensing through terrain.
        bool visible = tracking || npc.noTileCollide || IsBossEncounter(npc) ||
            Collision.CanHitLine(npc.position, npc.width, npc.height, player.position, player.width, player.height);
        float scale = IsBossEncounter(npc) ? 3 : 1;
        return EnemyPerception.CanTarget(new(npc.Center.X, npc.Center.Y), new(player.Center.X, player.Center.Y),
            new(player.active, player.dead, player.ghost), false, tracking, visible,
            config.EnemyNoticeRange * 16 * scale, config.EnemyLoseRange * 16 * scale);
    }

    private void InTargetContext(NPC? npc, bool allowed, Action update)
    {
        lock (targetingGate)
        {
            NPC? previousNpc = targetingNpc;
            bool previousAllowed = targetingPlayerAllowed;
            TargetPresence? previousSave = excludedPlayerSavePresence;
            targetingNpc = npc;
            targetingPlayerAllowed = allowed;
            targetingDepth++;
            try
            {
                if (allowed) { update(); return; }
                excludedPlayerSavePresence ??= IsDetached(Main.LocalPlayer) ? new TargetPresence(true, false, false) :
                    new TargetPresence(Main.LocalPlayer.active, Main.LocalPlayer.dead, Main.LocalPlayer.ghost);
                using var local = new TargetExclusionScope(new TerrariaPlayerBody(Main.LocalPlayer), Main.myPlayer,
                    npc is null ? null : new TerrariaAggroMemory(npc));
                using var none = new TargetExclusionScope(new TerrariaPlayerBody(Main.player[TargetExclusionScope.NoPlayer]), Main.myPlayer);
                update();
            }
            finally
            {
                targetingDepth--;
                targetingNpc = previousNpc;
                targetingPlayerAllowed = previousAllowed;
                excludedPlayerSavePresence = previousSave;
            }
        }
    }

    private void TrackTarget(On_NPC.orig_SetTargetTrackingValues original, NPC npc, bool faceTarget, float distance, int tankTarget)
    {
        if (!UsesTargetRules(npc)) { original(npc, faceTarget, distance, tankTarget); return; }
        if (npc.target != Main.myPlayer || !CanSelectLocal(npc))
        {
            // Vanilla changes an empty target into player 0. A failed search
            // must stay empty, including after returning far from an old enemy.
            if (!npc.HasNPCTarget) { npc.target = TargetExclusionScope.NoPlayer; npc.targetRect = Rectangle.Empty; }
            if (npc.oldTarget == Main.myPlayer) npc.oldTarget = TargetExclusionScope.NoPlayer;
            return;
        }
        State(npc).EngagedPlayer = Main.myPlayer;
        original(npc, faceTarget, distance, tankTarget);
    }

    private void FindUpgradedTarget(On_NPC.orig_TargetClosestUpgraded original, NPC npc, bool faceTarget, Vector2? checkPosition)
    {
        if (!UsesTargetRules(npc)) { original(npc, faceTarget, checkPosition); return; }
        bool allowed = CanSelectLocal(npc);
        InTargetContext(npc, allowed, () => original(npc, faceTarget, checkPosition));
        if (allowed && npc.target == Main.myPlayer) State(npc).EngagedPlayer = Main.myPlayer;
    }

    private byte FindClosestPlayer(On_Player.orig_FindClosest original, Vector2 position, int width, int height)
    {
        byte found = original(position, width, height);
        if (targetingDepth > 0 && !targetingPlayerAllowed) return TargetExclusionScope.NoPlayer;
        return found;
    }

    private void UpdateHostileProjectileAI(On_Projectile.orig_AI original, Projectile projectile)
    {
        if (!ExcludeLocalTarget || !projectile.hostile) { original(projectile); return; }
        InTargetContext(null, false, () => original(projectile));
    }

    private void SaveDetachedPlayer(On_Player.orig_InternalSavePlayerFile original, PlayerFileData data)
    {
        lock (targetingGate)
        {
            if (data.Player.whoAmI == Main.myPlayer && excludedPlayerSavePresence is { } presence)
                TargetExclusionScope.SerializePresence(new TerrariaPlayerBody(data.Player), presence, () => original(data));
            else if (IsDetached(data.Player))
                TargetExclusionScope.SerializeAlive(new TerrariaPlayerBody(data.Player), () => original(data));
            else original(data);
        }
    }

    private static bool HasLivingNpcTarget(NPC npc) => npc.HasNPCTarget && npc.target >= 300 && npc.target - 300 < Main.maxNPCs &&
        Main.npc[npc.target - 300].active && Main.npc[npc.target - 300].life > 0;

    internal bool HoldDetachedBoss(NPC npc) => ExcludeLocalTarget && npc.active && npc.life > 0 && IsBossEncounter(npc) && BossParentAlive(npc);

    private static bool BossParentAlive(NPC npc)
    {
        // Let native AI remove/transform orphaned limbs and worm segments after
        // a 3D kill. Keeping every boss-related NPC would retain dead encounters.
        if (npc.realLife >= 0 && npc.realLife < Main.maxNPCs && npc.realLife != npc.whoAmI)
            return Main.npc[npc.realLife].active && Main.npc[npc.realLife].life > 0;
        int parent = npc.type switch
        {
            NPCID.EaterofWorldsBody or NPCID.EaterofWorldsTail => NPCID.EaterofWorldsHead,
            NPCID.Creeper => NPCID.BrainofCthulhu,
            NPCID.GolemHead or NPCID.GolemHeadFree or NPCID.GolemFistLeft or NPCID.GolemFistRight => NPCID.Golem,
            NPCID.MoonLordHead or NPCID.MoonLordHand or NPCID.MoonLordFreeEye => NPCID.MoonLordCore,
            NPCID.PlanterasHook or NPCID.PlanterasTentacle => NPCID.Plantera,
            _ => npc.aiStyle switch
            {
                NPCAIStyleID.SkeletronHand => NPCID.SkeletronHead,
                NPCAIStyleID.WallOfFleshEye or NPCAIStyleID.TheHungry => NPCID.WallofFlesh,
                NPCAIStyleID.PrimeSaw or NPCAIStyleID.PrimeVice or NPCAIStyleID.PrimeCannon or NPCAIStyleID.PrimeLaser => NPCID.SkeletronPrime,
                NPCAIStyleID.TheDestroyer when npc.type != NPCID.TheDestroyer => NPCID.TheDestroyer,
                _ => -1
            }
        };
        if (parent < 0) return true;
        for (int i = 0; i < Main.maxNPCs; i++)
            if (Main.npc[i].active && Main.npc[i].life > 0 && Main.npc[i].type == parent) return true;
        return false;
    }

    private static bool IsBossEncounter(NPC npc) => npc.boss ||
        npc.realLife >= 0 && npc.realLife < Main.maxNPCs && Main.npc[npc.realLife].boss ||
        npc.aiStyle is NPCAIStyleID.SkeletronHand or NPCAIStyleID.WallOfFleshEye or NPCAIStyleID.TheHungry or
            NPCAIStyleID.PrimeSaw or NPCAIStyleID.PrimeVice or NPCAIStyleID.PrimeCannon or NPCAIStyleID.PrimeLaser or NPCAIStyleID.TheDestroyer ||
        npc.type is NPCID.EaterofWorldsHead or NPCID.EaterofWorldsBody or NPCID.EaterofWorldsTail or NPCID.Creeper or
            NPCID.GolemHead or NPCID.GolemHeadFree or NPCID.GolemFistLeft or NPCID.GolemFistRight or
            NPCID.MoonLordHead or NPCID.MoonLordHand or NPCID.MoonLordCore or NPCID.MoonLordFreeEye or
            NPCID.PlanterasHook or NPCID.PlanterasTentacle;
}
