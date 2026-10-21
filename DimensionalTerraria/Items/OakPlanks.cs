using DimensionalTerraria.Systems;
using DimensionalTerraria.Tiles;
using Terraria.ModLoader;

namespace DimensionalTerraria.Items;

public sealed class OakPlanks : ModItem
{
    public override string Texture => "DimensionalTerraria/Textures/Minecraft/oak_planks";
    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<OakPlanksTile>());
        Item.width = Item.height = 24;
        Item.maxStack = 9999;
        Item.value = 0;
    }
    public override void AddRecipes() => CreateRecipe(4).AddRecipeGroup(MinecraftRecipes.WoodGroup).Register();
}
