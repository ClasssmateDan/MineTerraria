using System;
using System.Reflection;
using DimensionalTerraria.Core;
using Terraria;
using Terraria.GameContent.Achievements;
using Terraria.ModLoader;
using Terraria.Enums;
using DimensionalTerraria.Tiles;
using Microsoft.Xna.Framework;
using DimensionalTerraria.Systems;
using DimensionalTerraria.Items;

namespace DimensionalTerraria.World;

internal static class NativeInteractions
{
    internal static bool IsBoat(TileRayHit target) => target.Layer == RayLayer.Tile && BoatCaptureSystem.FindAt(target.X, target.Y) is not null;
    internal static bool BreakBoat(TileRayHit target)
    {
        if (target.Layer != RayLayer.Tile || BoatCaptureSystem.FindAt(target.X, target.Y) is not { } boat) return false;
        boat.Break(); return true;
    }
    private delegate void MineTool(Player player, Item tool, out bool canHitWalls, int x, int y);
    private static readonly MineTool Mine = typeof(Player).GetMethod("ItemCheck_UseMiningTools_ActuallyUseMiningTool",
        BindingFlags.Instance | BindingFlags.NonPublic)!.CreateDelegate<MineTool>();
    private static readonly Func<Player, int, int, Item, Item> Pickup = typeof(Player).GetMethod("PickupItem",
        BindingFlags.Instance | BindingFlags.NonPublic)!.CreateDelegate<Func<Player, int, int, Item, Item>>();
    private static readonly Action<Player, int> RestoreUseCount = typeof(Player).GetProperty(nameof(Player.ItemUsesThisAnimation))!
        .GetSetMethod(true)!.CreateDelegate<Action<Player, int>>();
    private static readonly Func<Player, Item, bool[]> CutIgnore = typeof(Player).GetMethod("ItemCheck_GetTileCutIgnoreList",
        BindingFlags.Instance | BindingFlags.NonPublic)!.CreateDelegate<Func<Player, Item, bool[]>>();
    private static readonly Action<Player, Item, Rectangle, bool[]> Cut = typeof(Player).GetMethod("ItemCheck_CutTiles",
        BindingFlags.Instance | BindingFlags.NonPublic)!.CreateDelegate<Action<Player, Item, Rectangle, bool[]>>();

    public static bool HasTool(Player player, TileRayHit target)
    {
        Item tool = player.HeldItem;
        if (tool.IsAir || player.noBuilding) return false;
        if (target.Layer == RayLayer.Wall) return tool.hammer > 0;
        if (target.Layer != RayLayer.Tile) return false;
        if (BoatCaptureSystem.OverlayAt(target.X, target.Y) is not null)
            return tool.pick > 0 || tool.axe > 0 || tool.hammer > 0 || tool.damage > 0;
        Tile tile = Main.tile[target.X, target.Y];
        if (tile.TileType == ModContent.TileType<BedrockTile>()) return CanBreakBedrock(player);
        bool miningTool = tool.pick > 0 || tool.axe > 0 || tool.hammer > 0;
        if (Main.tileCut[tile.TileType])
            return (miningTool || tool.damage > 0 && !tool.noMelee) && !CutIgnore(player, tool)[tile.TileType] &&
                WorldGen.CanCutTile(target.X, target.Y, TileCuttingContext.AttackMelee);
        if (Main.tileNoFail[tile.TileType] && !Main.tileAxe[tile.TileType] && !Main.tileHammer[tile.TileType]) return miningTool;
        return Main.tileAxe[tile.TileType] ? tool.axe > 0 : Main.tileHammer[tile.TileType] ? tool.hammer > 0 : tool.pick > 0;
    }

    public static int Hit(Player player, TileRayHit target)
    {
        int time = player.itemTime, maxTime = player.itemTimeMax, animation = player.itemAnimation;
        int uses = player.ItemUsesThisAnimation;
        bool pound = player.poundRelease, mining = AchievementsHelper.CurrentlyMining;
        try
        {
            if (target.Layer == RayLayer.Tile && BoatCaptureSystem.OverlayAt(target.X, target.Y) is { } boat)
            { boat.Break(); return System.Math.Max(1, player.HeldItem.useTime); }
            if (target.Layer == RayLayer.Wall)
            {
                if (Player.CanPlayerSmashWall(target.X, target.Y)) player.PickWall(target.X, target.Y, (int)(player.HeldItem.hammer * 1.5f));
                return Math.Max(1, player.HeldItem.useTime / 2);
            }
            else
            {
                Tile tile = Main.tile[target.X, target.Y];
                if (tile.TileType == ModContent.TileType<BedrockTile>())
                {
                    if (CanBreakBedrock(player))
                    {
                        using (BedrockMining.Allow(target.X, target.Y)) WorldGen.KillTile(target.X, target.Y);
                        player.ApplyItemTime(player.HeldItem, player.pickSpeed);
                    }
                }
                else if (Main.tileCut[tile.TileType])
                {
                    // The native cutting path honors harvesting rules, nature
                    // protection and special tools. Restrict its area to one cell.
                    Cut(player, player.HeldItem, new Rectangle(target.X * 16 + 1, target.Y * 16 + 1, 1, 1), CutIgnore(player, player.HeldItem));
                    player.ApplyItemTime(player.HeldItem);
                }
                else if (Main.tileNoFail[tile.TileType] && !Main.tileAxe[tile.TileType] && !Main.tileHammer[tile.TileType])
                {
                    player.PickTile(target.X, target.Y, player.HeldItem.pick);
                    player.ApplyItemTime(player.HeldItem, player.HeldItem.pick > 0 ? player.pickSpeed : 1);
                }
                else Mine(player, player.HeldItem, out _, target.X, target.Y);
            }
            return Math.Max(1, player.itemTime);
        }
        finally
        {
            player.itemTime = time; player.itemTimeMax = maxTime; player.itemAnimation = animation;
            player.poundRelease = pound;
            RestoreUseCount(player, uses);
            AchievementsHelper.CurrentlyMining = mining;
        }
    }

    public static bool TryPickup(Player player, int slot)
    {
        Item item = Main.item[slot];
        if (!item.active || item.IsAir || item.shimmerTime != 0 || item.noGrabDelay != 0 ||
            item.ownIgnore == player.whoAmI || item.playerIndexTheItemIsReservedFor != player.whoAmI && item.playerIndexTheItemIsReservedFor != 255 ||
            !player.CanAcceptItemIntoInventory(item) || !player.ItemSpace(item).CanTakeItem ||
            item.shimmered && item.velocity.Length() >= 0.2f || !ItemLoader.CanPickup(item, player)) return false;
        int before = item.stack;
        if (!CombinedHooks.OnPickup(item, player)) Main.item[slot] = new Item();
        else Pickup(player, player.whoAmI, slot, item);
        return !Main.item[slot].active || Main.item[slot].IsAir || Main.item[slot].stack < before;
    }

    public static void ShootTile(Player player, TileRayHit target, int damage)
    {
        if (player.noBuilding || target.Layer != RayLayer.Tile) return;
        if (BoatCaptureSystem.OverlayAt(target.X, target.Y) is { } boat) { boat.Break(); return; }
        Tile tile = Main.tile[target.X, target.Y];
        if (!tile.HasTile || tile.TileType == ModContent.TileType<BedrockTile>()) return;
        bool mining = AchievementsHelper.CurrentlyMining;
        try
        {
            if (Main.tileCut[tile.TileType])
            {
                if (!CutIgnore(player, player.HeldItem)[tile.TileType] && WorldGen.CanCutTile(target.X, target.Y, TileCuttingContext.AttackProjectile))
                    Cut(player, player.HeldItem, new Rectangle(target.X * 16 + 1, target.Y * 16 + 1, 1, 1), CutIgnore(player, player.HeldItem));
            }
            else player.PickTile(target.X, target.Y, ProjectionWeaponStats.ShotPickPower(damage));
        }
        finally { AchievementsHelper.CurrentlyMining = mining; }
    }

    private static bool CanBreakBedrock(Player player) => !player.noBuilding &&
        DimensionSystem.IsDetached(player) && player.HeldItem.ModItem is HigherDimensionalPickaxe;
}
