using System.Linq;
using DimensionalTerraria.Core;
using DimensionalTerraria.Tiles;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace DimensionalTerraria.Systems;

public sealed class HopperEntity : ModTileEntity
{
    private int cooldown;
    private int cachedChest = -1;
    public override bool IsTileValidForEntity(int x, int y) => WorldGen.InWorld(x, y, 10) &&
        Main.tile[x, y].HasTile && Main.tile[x, y].TileType == ModContent.TileType<HopperTile>();
    public int AfterPlacement(int i, int j, int type, int style, int direction, int alternate)
    {
        if (Main.netMode != NetmodeID.SinglePlayer) return -1;
        if (TileEntity.ByPosition.TryGetValue(new Point16(i, j), out var existing)) return existing.ID;
        return Place(i, j);
    }
    private Chest? OutputChest()
    {
        Tile below = Main.tile[Position.X, Position.Y + 1];
        if (!below.HasUnactuatedTile || !TileID.Sets.BasicChest[below.TileType] || below.TileFrameY % 36 != 0) return null;
        int left = Position.X - below.TileFrameX / 18 % 2, top = Position.Y + 1;
        // Only real complete 2x2 world chests. Never personal banks or stale data.
        for (int dx = 0; dx < 2; dx++) for (int dy = 0; dy < 2; dy++)
        {
            Tile part = Main.tile[left + dx, top + dy];
            if (!part.HasUnactuatedTile || part.TileType != below.TileType || part.TileFrameX / 18 % 2 != dx || part.TileFrameY / 18 % 2 != dy)
                return null;
        }
        if (cachedChest < 0 || cachedChest >= Main.chest.Length || Main.chest[cachedChest] is not { } cached || cached.x != left || cached.y != top)
            cachedChest = Chest.FindChest(left, top);
        if (cachedChest < 0 || Chest.IsLocked(left, top)) return null;
        return Main.chest[cachedChest];
    }
    internal void Tick()
    {
        if (!IsTileValidForEntity(Position.X, Position.Y)) { Kill(Position.X, Position.Y); return; }
        if (cooldown > 0) { cooldown--; return; }
        cooldown = HopperRules.TransferInterval - 1;
        if (!Main.tile[Position.X, Position.Y].HasUnactuatedTile || OutputChest() is not { } chest) return;
        for (int i = 0; i < Main.maxItems; i++)
        {
            Item source = Main.item[i];
            if (source is null || !source.active || source.IsAir || ItemID.Sets.IsAPickup[source.type] ||
                !HopperRules.CanCollect(new(Position.X, Position.Y), new(source.Center.X / 16, source.Center.Y / 16),
                    new(source.width / 16f, source.height / 16f))) continue;
            Deposit(source, chest);
        }
    }
    private static void Deposit(Item source, Chest chest)
    {
        // Prefer existing stacks. CanStack/OnStack preserve prefix and mod data.
        foreach (Item destination in chest.item)
        {
            if (destination is null || destination.IsAir || destination.type != source.type || destination.stack >= destination.maxStack) continue;
            ItemLoader.TryStackItems(destination, source, out _);
            if (source.stack <= 0) { source.TurnToAir(); return; }
        }
        for (int slot = 0; slot < chest.item.Length; slot++)
        {
            if (chest.item[slot] is { IsAir: false }) continue;
            // Native cloning/splitting retains per-item state; do not put the
            // live Main.item object itself in an inventory or exceed maxStack.
            chest.item[slot] = ItemLoader.TransferWithLimit(source, source.maxStack);
            if (source.IsAir) return;
        }
        // Full/partially full chest: keep every untransferred item in the world.
    }
}

[Autoload(Side = ModSide.Client)]
public sealed class HopperSystem : ModSystem
{
    public override void Load() => On_WorldGen.KillTile += AfterTileKill;
    public override void Unload() => On_WorldGen.KillTile -= AfterTileKill;
    private static void AfterTileKill(On_WorldGen.orig_KillTile original, int x, int y, bool fail, bool effectOnly, bool noItem)
    {
        bool hopper = WorldGen.InWorld(x, y) && Main.tile[x, y].HasTile && Main.tile[x, y].TileType == ModContent.TileType<HopperTile>();
        original(x, y, fail, effectOnly, noItem);
        // Single-cell tiles do not reliably call KillMultiTile. Clean up only
        // after the final native/protection result, before a replacement is placed.
        if (hopper && (!Main.tile[x, y].HasTile || Main.tile[x, y].TileType != ModContent.TileType<HopperTile>()) &&
            TileEntity.ByPosition.TryGetValue(new Point16(x, y), out var entity) && entity is HopperEntity previous)
            previous.Kill(x, y);
    }
    public override void PostUpdateItems()
    {
        if (Main.netMode != NetmodeID.SinglePlayer || Main.gameMenu || Main.gamePaused) return;
        foreach (HopperEntity hopper in TileEntity.ByID.Values.OfType<HopperEntity>().ToArray()) hopper.Tick();
    }
}
