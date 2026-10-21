using DimensionalTerraria.Systems;
using DimensionalTerraria.Tiles;
using Terraria.ModLoader;

namespace DimensionalTerraria.Items;

public sealed class MinecraftCraftingTable : ModItem
{
    public override string Texture => MinecraftCraftingTableTile.FrontTexture;
    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<MinecraftCraftingTableTile>());
        Item.width = Item.height = 24;
        Item.maxStack = 9999;
        Item.value = 0;
    }
    public override void AddRecipes() => CreateRecipe().AddRecipeGroup(MinecraftRecipes.WoodGroup, 4).Register();
}
