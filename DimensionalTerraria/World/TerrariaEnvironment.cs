using DimensionalTerraria.Core;
using Terraria;
using Terraria.ID;

namespace DimensionalTerraria.World;

// Read the flight's projection directly. Moving the real Player or replacing
// global SceneMetrics would also change spawns, music and other mods' state.
internal sealed class TerrariaEnvironment : IEnvironmentWorld
{
    public int Width => Main.maxTilesX;
    public int Height => Main.maxTilesY;

    public ProjectedBiome MaterialAt(int x, int y)
    {
        Tile tile = Main.tile[x, y];
        if (!tile.HasUnactuatedTile) return ProjectedBiome.Unknown;
        return tile.TileType switch
        {
            TileID.SnowBlock or TileID.IceBlock => ProjectedBiome.Snow,
            TileID.JungleGrass or TileID.JunglePlants or TileID.JunglePlants2 => ProjectedBiome.Jungle,
            TileID.Ebonstone or TileID.CorruptGrass or TileID.Ebonsand or TileID.CorruptIce => ProjectedBiome.Corruption,
            TileID.Crimstone or TileID.CrimsonGrass or TileID.Crimsand or TileID.FleshIce => ProjectedBiome.Crimson,
            TileID.Pearlstone or TileID.HallowedGrass or TileID.Pearlsand or TileID.HallowedIce => ProjectedBiome.Hallow,
            TileID.MushroomGrass or TileID.MushroomPlants or TileID.MushroomTrees => ProjectedBiome.Mushroom,
            TileID.BlueDungeonBrick or TileID.GreenDungeonBrick or TileID.PinkDungeonBrick => ProjectedBiome.Dungeon,
            TileID.Sand or TileID.Sandstone or TileID.HardenedSand => ProjectedBiome.Desert,
            _ => ProjectedBiome.Unknown
        };
    }
}
