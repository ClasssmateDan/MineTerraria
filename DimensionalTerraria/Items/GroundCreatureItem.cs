using DimensionalTerraria.Core;
using DimensionalTerraria.World;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using NVector2 = System.Numerics.Vector2;

namespace DimensionalTerraria.Items;

public abstract class GroundCreatureItem : ModItem
{
    private bool spawned;
    private readonly TerrariaCollisionWorld world = new();
    protected abstract int CreatureType { get; }
    internal abstract bool SpawnPosition(ICollisionWorld world, int x, int y, out NVector2 center, IExplorationMap? exploration = null);

    public override void SetDefaults()
    {
        spawned = false;
        Item.width = 16; Item.height = 24;
        Item.maxStack = 999;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.useTime = Item.useAnimation = 24;
        Item.consumable = true;
        Item.UseSound = SoundID.Item8;
        Item.rare = ItemRarityID.Blue;
    }
    public override bool CanUseItem(Player player)
    {
        spawned = false;
        return Main.netMode == NetmodeID.SinglePlayer && FindGround(player, out _, out _);
    }
    public override bool? UseItem(Player player) => spawned || FindGround(player, out int x, out int y) && TrySpawn(player, x, y);
    public override bool ConsumeItem(Player player) => spawned;

    private bool FindGround(Player player, out int x, out int y)
    {
        x = (int)(Main.MouseWorld.X / 16); y = (int)(Main.MouseWorld.Y / 16);
        for (int below = 0; below < 5; below++)
        {
            if (!SpawnPosition(world, x, y + below, out var center)) continue;
            if (Vector2.Distance(player.Center, new Vector2(center.X, center.Y) * 16) > 7 * 16 ||
                !Collision.CanHitLine(player.Center, 1, 1, new Vector2(center.X, center.Y) * 16, 1, 1)) return false;
            y += below;
            return true;
        }
        return false;
    }

    internal bool TrySpawn(Player player, int x, int y, IExplorationMap? exploration = null)
    {
        spawned = false;
        if (Main.netMode != NetmodeID.SinglePlayer || player.noItems || player.CCed ||
            !SpawnPosition(world, x, y, out var center, exploration)) return false;
        int slot = NPC.NewNPC(new EntitySource_ItemUse(player, Item), (int)(center.X * 16), (int)(center.Y * 16), CreatureType);
        if (slot < 0 || slot >= Main.maxNPCs) return false;
        Main.npc[slot].Center = new Vector2(center.X, center.Y) * 16;
        return spawned = true;
    }
}
