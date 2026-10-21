using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Localization;

namespace DimensionalTerraria.Tiles;

public sealed class RedstoneBlockTile : ModTile
{
    public override string Texture => "DimensionalTerraria/Textures/Minecraft/redstone_block";
    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = Main.tileBlockLight[Type] = true;
        Main.tileMergeDirt[Type] = false;
        DustType = DustID.RedTorch; HitSound = SoundID.Tink;
        AddMapEntry(new Color(166, 20, 14), Language.GetText("Mods.DimensionalTerraria.Items.RedstoneBlock.DisplayName"));
    }
    public override bool TileFrame(int i, int j, ref bool resetFrame, ref bool noBreak)
    { Main.tile[i, j].TileFrameX = Main.tile[i, j].TileFrameY = 0; return false; }
}
