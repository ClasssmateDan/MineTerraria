using System;
using DimensionalTerraria.Core;
using DimensionalTerraria.Systems;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;
using NVector3 = System.Numerics.Vector3;

namespace DimensionalTerraria.World;

internal readonly record struct PlacementPlan(int X, int Y, int Left, int Top, int Width, int Height, bool Wall);

internal static class NativePlacement
{
    internal static bool Supported(Item item) => !item.IsAir && item.stack > 0 && item.consumable && item.tileWand < 0 &&
        (item.createTile >= TileID.Dirt || item.createWall > WallID.None);

    internal static bool Plan(Player player, NVector3 eye, NVector3 direction, out PlacementPlan plan, IExplorationMap? fog = null, FlightState? flight = null)
    {
        plan = default;
        Item item = player.HeldItem;
        if (!Supported(item) || player.noBuilding || player.noItems || player.CCed ||
            !PlacementRules.Aim(new TerrariaCollisionWorld(), eye, direction, item.createTile < TileID.Dirt, out var cell, fog)) return false;
        int left = cell.X, top = cell.Y, width = 1, height = 1;
        bool wall = item.createTile < TileID.Dirt;
        if (!wall)
        {
            TileObjectData? data = TileObjectData.GetTileData(item.createTile, item.placeStyle);
            if (data is not null)
            {
                left -= data.Origin.X; top -= data.Origin.Y; width = data.Width; height = data.Height;
            }
            else if (Main.tileFrameImportant[item.createTile] && !TileID.Sets.Platforms[item.createTile]) return false;
        }
        if (left < 10 || top < 10 || left + width > Main.maxTilesX - 10 || top + height > Main.maxTilesY - 10 ||
            !PlacementRules.InReach(eye, left, top, width, height, wall, fog)) return false;
        for (int x = left; x < left + width; x++) for (int y = top; y < top + height; y++)
        {
            Tile tile = Main.tile[x, y];
            if (BoatCaptureSystem.FindAt(x, y) is not null || (wall ? tile.WallType != WallID.None : tile.HasTile)) return false;
            if (!wall && !TileObjectData.CheckLiquidPlacement(item.createTile, item.placeStyle, tile)) return false;
            NVector3 end = new(x + .5f, y + .5f, wall ? -.5f : .5f), delta = end - eye;
            float distance = delta.Length();
            TileRayHit? blocked = distance > .001f ? TileRaycast.Cast(new TerrariaCollisionWorld(), eye, delta, distance - .001f, fog) : null;
            if (blocked is { } obstacle && !(wall && obstacle.Layer == RayLayer.Tile && obstacle.X == x && obstacle.Y == y)) return false;
        }
        Rectangle area = new(left * 16, top * 16, width * 16, height * 16);
        if (flight is not null)
        {
            var p = flight.Position; var size = flight.BodySize;
            float z0 = wall ? -1.5f : -.5f, z1 = wall ? -.5f : .5f;
            if (p.Z + SpatialRules.BodyHalfDepth > z0 && p.Z - SpatialRules.BodyHalfDepth < z1 &&
                p.X + size.X / 2 > left && p.X - size.X / 2 < left + width &&
                p.Y + size.Y / 2 > top && p.Y - size.Y / 2 < top + height) return false;
        }
        if (!wall)
        {
            foreach (Player other in Main.player) if (other.active && !other.dead && !DimensionSystem.IsDetached(other) && area.Intersects(other.Hitbox)) return false;
            foreach (NPC npc in Main.npc) if (npc.active && area.Intersects(npc.Hitbox)) return false;
            if (!TileLoader.CanPlace(cell.X, cell.Y, item.createTile)) return false;
            if (TileObjectData.GetTileData(item.createTile, item.placeStyle) is not null)
            {
                // onlyCheck=true is the engine's cursor-preview path and leaves
                // the out object empty. Normal validation returns the selected
                // alternate/coordinates without placing anything.
                if (!TileObject.CanPlace(cell.X, cell.Y, item.createTile, item.placeStyle, player.direction, out TileObject data)) return false;
                // Directional alternate styles may have different origins.
                if (data.xCoord != left || data.yCoord != top) return false;
            }
            else if (!Adjacent(cell.X, cell.Y)) return false;
        }
        else if (!WallLoader.CanPlace(cell.X, cell.Y, item.createWall) || !Adjacent(cell.X, cell.Y)) return false;
        plan = new(cell.X, cell.Y, left, top, width, height, wall);
        return true;
    }

    internal static bool TryPlace(Player player, NVector3 eye, NVector3 direction, out PlacementPlan plan, IExplorationMap? fog = null, FlightState? flight = null)
    {
        if (!Plan(player, eye, direction, out plan, fog, flight) || !ItemLoader.CanUseItem(player.HeldItem, player)) return false;
        Item item = player.HeldItem;
        int type = item.createTile;
        if (plan.Wall)
        {
            WorldGen.PlaceWall(plan.X, plan.Y, item.createWall, mute: true);
            if (Main.tile[plan.X, plan.Y].WallType != item.createWall) return false;
            WallLoader.PlaceInWorld(plan.X, plan.Y, item);
        }
        else
        {
            bool placed;
            if (TileObjectData.GetTileData(type, item.placeStyle) is not null)
            {
                if (!TileObject.CanPlace(plan.X, plan.Y, type, item.placeStyle, player.direction, out TileObject data)) return false;
                placed = TileObject.Place(data);
                if (placed) TileObjectData.CallPostPlacementPlayerHook(plan.X, plan.Y, type, item.placeStyle, player.direction, data.alternate, data);
            }
            else placed = WorldGen.PlaceTile(plan.X, plan.Y, type, mute: true, forced: false, plr: player.whoAmI, style: item.placeStyle);
            if (!placed) return false;
            TileLoader.PlaceInWorld(plan.X, plan.Y, item);
        }
        if (ItemLoader.ConsumeItem(item, player) && --item.stack <= 0) item.TurnToAir();
        WorldGen.SquareTileFrame(plan.X, plan.Y);
        WorldGen.SquareWallFrame(plan.X, plan.Y);
        return true;
    }
    private static bool Adjacent(int x, int y)
    {
        foreach (var d in new[] { (0, -1), (1, 0), (0, 1), (-1, 0) })
        {
            Tile near = Main.tile[x + d.Item1, y + d.Item2];
            if (near.WallType != WallID.None || near.HasUnactuatedTile && (Main.tileSolid[near.TileType] || Main.tileRope[near.TileType] || TileID.Sets.IsBeam[near.TileType])) return true;
        }
        return false;
    }
}
