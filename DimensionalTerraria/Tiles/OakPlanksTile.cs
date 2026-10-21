using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace DimensionalTerraria.Tiles;

public sealed class OakPlanksTile : ModTile
{
    public override string Texture => "DimensionalTerraria/Textures/Minecraft/oak_planks";
    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = Main.tileBlockLight[Type] = true;
        Main.tileMergeDirt[Type] = false;
        DustType = DustID.WoodFurniture;
        HitSound = SoundID.Dig;
        MineResist = 0.5f;
        AddMapEntry(new Color(162, 130, 79), Language.GetText("Mods.DimensionalTerraria.Items.OakPlanks.DisplayName"));
    }
    public override bool TileFrame(int i, int j, ref bool resetFrame, ref bool noBreak)
    {
        Main.tile[i, j].TileFrameX = Main.tile[i, j].TileFrameY = 0;
        return false;
    }
}
