using DimensionalTerraria.Core;
using DimensionalTerraria.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using DimensionalTerraria.Systems;

namespace DimensionalTerraria.World;

internal sealed class TerrariaCollisionWorld : ICollisionWorld, IInteractionWorld, IBoatWorld, IGolemSpawnWorld
{
    public int Width => Main.maxTilesX;
    public int Height => Main.maxTilesY;
    public BoatCell BoatCellAt(int x, int y)
    {
        if (BoatCaptureSystem.OverlayAt(x, y) is not null) return new(true, BlockShape.Empty);
        Tile tile = Main.tile[x, y];
        return new(tile.HasTile, GetShape(tile), tile.LiquidAmount, tile.LiquidType, CanClearForBoat(x, y));
    }

    internal static bool CanClearForBoat(int x, int y)
    {
        Tile tile = Main.tile[x, y];
        int type = tile.TileType;
        // Native placement may cut these objects. Existing boats, hard blocks,
        // furniture and native trap exceptions must never be replaced this way.
        return tile.HasTile && type != ModContent.TileType<OakBoatTile>() &&
            type != TileID.RollingCactus && type != TileID.TNTBarrel && !Main.tileSolid[type] && !Main.tileSolidTop[type] &&
            (Main.tileCut[type] || TileID.Sets.BreakableWhenPlacing[type]) && WorldGen.CanKillTile(x, y);
    }

    public WorldCell GetCell(int x, int y)
    {
        Tile tile = Main.tile[x, y];
        return new WorldCell(GetShape(tile), tile.WallType != WallID.None, tile.LiquidAmount != 0);
    }

    public GolemSpawnCell GolemCellAt(int x, int y)
    {
        Tile tile = Main.tile[x, y];
        return new(GetShape(tile), tile.LiquidAmount, tile.Slope != SlopeType.Solid);
    }

    public InteractionCell GetInteractionCell(int x, int y)
    {
        Tile tile = Main.tile[x, y];
        if (BoatCaptureSystem.OverlayAt(x, y) is not null) return new(true, tile.WallType != WallID.None, BlockShape.Empty);
        return new InteractionCell(tile.HasUnactuatedTile, tile.WallType != WallID.None, GetShape(tile));
    }

    internal static BlockShape GetShape(Tile tile)
    {
        if (!tile.HasUnactuatedTile)
            return BlockShape.Empty;
        if (Main.tileSolidTop[tile.TileType])
            return BlockShape.Platform;
        if (!Main.tileSolid[tile.TileType])
            return BlockShape.Empty;
        // Slopes intentionally use a full cube in v0.1: return checks are conservative.
        return tile.IsHalfBlock ? BlockShape.HalfBlock : BlockShape.Solid;
    }
}
