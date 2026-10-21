using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace DimensionalTerraria.Tiles;

public sealed class MinecraftCraftingTableTile : ModTile
{
    public const string FrontTexture = "DimensionalTerraria/Textures/Minecraft/crafting_table_front";
    public const string SideTexture = "DimensionalTerraria/Textures/Minecraft/crafting_table_side";
    public const string TopTexture = "DimensionalTerraria/Textures/Minecraft/crafting_table_top";
    public override string Texture => FrontTexture;
    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = Main.tileBlockLight[Type] = true;
        Main.tileMergeDirt[Type] = false;
        // Its own adjacency flag gates MC recipes; a vanilla work bench cannot substitute.
        DustType = DustID.WoodFurniture;
        HitSound = SoundID.Dig;
        MineResist = 0.5f;
        AddMapEntry(new Color(133, 87, 51), Language.GetText("Mods.DimensionalTerraria.Items.MinecraftCraftingTable.DisplayName"));
    }
    public override bool TileFrame(int i, int j, ref bool resetFrame, ref bool noBreak)
    {
        Main.tile[i, j].TileFrameX = Main.tile[i, j].TileFrameY = 0;
        return false;
    }
}
