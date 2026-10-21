using System.Collections.Generic;
using DimensionalTerraria.Core;
using DimensionalTerraria.NPCs;
using DimensionalTerraria.World;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace DimensionalTerraria.Systems;

public sealed class VillagerDefenseSystem : ModSystem
{
    private readonly TerrariaCollisionWorld world = new();
    private readonly List<MinecraftVillager> villagers = new();
    private readonly List<MinecraftVillager> attempts = new();
    private int threatScan, guardScan;

    // Boat passengers skip NPC.AI, so perception and cooldowns belong here.
    public override void PostUpdateNPCs()
    {
        if (Main.gameMenu || Main.netMode != NetmodeID.SinglePlayer) return;
        villagers.Clear(); attempts.Clear();
        for (int i = 0; i < Main.maxNPCs; i++)
            if (Main.npc[i] is { active: true, life: > 0, ModNPC: MinecraftVillager villager }) villagers.Add(villager);
        bool senseThreat = --threatScan <= 0, senseGuard = --guardScan <= 0;
        if (senseThreat) threatScan = 10;
        if (senseGuard) guardScan = 600;
        foreach (MinecraftVillager villager in villagers)
        {
            if (villager.Memory.Tick()) attempts.Add(villager);
            if (senseThreat) SenseThreat(villager);
            if (senseGuard && HasGuard(villager.NPC.Center)) villager.Memory.ObserveGuard();
        }
        foreach (MinecraftVillager villager in attempts) TrySummon(villager);
    }

    internal static bool Threat(NPC npc) => npc.active && npc.life > 0 && npc.damage > 0 && !npc.friendly && !npc.townNPC &&
        !npc.hide && npc.catchItem == 0 && npc.ModNPC is not (MinecraftVillager or MinecraftSheep or MinecraftIronGolem or DebugTargetDummy);
    private static void SenseThreat(MinecraftVillager villager)
    {
        NPC self = villager.NPC;
        float closest = 8 * 16 * (8 * 16) + 1;
        NPC? seen = null;
        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC other = Main.npc[i];
            float distance = Vector2.DistanceSquared(self.Center, other.Center);
            if (Threat(other) && distance < closest && Collision.CanHitLine(self.position, self.width, self.height, other.position, other.width, other.height))
            { closest = distance; seen = other; }
        }
        if (seen is not null) villager.Memory.Frighten(seen.Center.X);
    }
    private static bool Near(Vector2 a, Vector2 b, int radius) => VillagerMemory.Within(new(a.X, a.Y), new(b.X, b.Y), radius);
    private static bool HasGuard(Vector2 position)
    {
        for (int i = 0; i < Main.maxNPCs; i++)
            if (Main.npc[i] is { active: true, life: > 0, ModNPC: MinecraftIronGolem } golem && Near(position, golem.Center, 16)) return true;
        return false;
    }
    private void TrySummon(MinecraftVillager initiator)
    {
        if (!initiator.Memory.Panicking || !initiator.Memory.Eligible) return;
        Vector2 center = initiator.NPC.Center;
        // Always recheck live guards immediately, including manual/boat guards.
        if (HasGuard(center)) { initiator.Memory.ObserveGuard(); return; }
        int eligible = 0;
        foreach (MinecraftVillager other in villagers)
            if (other.Memory.Eligible && Near(center, other.NPC.Center, 10)) eligible++;
        if (eligible < 3) return;
        int x = (int)(center.X / 16), y = (int)(center.Y / 16);
        if (!VillageGolemSpawnRules.TryFind(world, x, y, out var spawn)) return;
        int slot = NPC.NewNPC(new EntitySource_Misc("DimensionalTerraria_VillagerDefense"), (int)(spawn.X * 16), (int)(spawn.Y * 16), ModContent.NPCType<MinecraftIronGolem>());
        if (slot < 0 || slot >= Main.maxNPCs || Main.npc[slot].ModNPC is not MinecraftIronGolem golem) return;
        Main.npc[slot].Center = new(spawn.X * 16, spawn.Y * 16);
        golem.VillageGuardian = true;
        foreach (MinecraftVillager other in villagers)
            if (Near(center, other.NPC.Center, 10)) other.Memory.ObserveGuard();
    }
    public override void OnWorldUnload() { villagers.Clear(); attempts.Clear(); threatScan = guardScan = 0; }
}
