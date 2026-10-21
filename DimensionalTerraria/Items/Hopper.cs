using DimensionalTerraria.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DimensionalTerraria.Items;

public sealed class Hopper : ModItem
{
    public override string Texture => HopperTile.ItemTexture;
    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<HopperTile>());
        Item.width = Item.height = 16;
        Item.maxStack = 9999;
    }
    public override void AddRecipes()
    {
        CreateRecipe().AddIngredient(ItemID.IronBar, 5).AddIngredient(ItemID.Chest).Register();
        CreateRecipe().AddIngredient(ItemID.IronBar, 5).AddIngredient<MinecraftChest>().Register();
    }
}
