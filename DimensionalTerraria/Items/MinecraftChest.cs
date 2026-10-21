using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DimensionalTerraria.Items;

public sealed class MinecraftChest : ModItem
{
    public override string Texture => "DimensionalTerraria/Textures/Minecraft/chest_normal";
    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<Tiles.MinecraftChestTile>());
        Item.width = Item.height = 24;
        Item.maxStack = 9999;
    }
    public override void AddRecipes() => CreateRecipe().AddIngredient<OakPlanks>(8).Register();
}
