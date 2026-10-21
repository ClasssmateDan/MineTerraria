using DimensionalTerraria.Content;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Localization;

namespace DimensionalTerraria.Tiles;

[Autoload(false)]
public sealed class StorageBlockTile : ModTile
{
    public StorageMaterial Material { get; }
    public StorageBlockTile(StorageMaterial material) => Material = material;
    public override string Name => Material.Key + "StorageTile";
    public override string Texture => $"Terraria/Images/Tiles_{TileID.GrayBrick}";
    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = true;
        Main.tileBlockLight[Type] = true;
        Main.tileMergeDirt[Type] = false;
        MineResist = 1.5f;
        MinPick = 0;
        DustType = DustID.Stone;
        HitSound = SoundID.Tink;
        AddMapEntry(new Color((byte)(Material.Color >> 16), (byte)(Material.Color >> 8), (byte)Material.Color),
            Language.GetText("Mods.DimensionalTerraria.Items." + Material.Key + "StorageBlock.DisplayName"));
    }
    public override bool TileFrame(int i, int j, ref bool resetFrame, ref bool noBreak)
    {
        Main.tile[i, j].TileFrameX = Main.tile[i, j].TileFrameY = 0;
        return false;
    }
}
