using DimensionalTerraria.Tiles;
using Terraria;
using Terraria.ModLoader;

namespace DimensionalTerraria.Items;

public sealed class RedstoneBlock : ModItem
{
    public override string Texture => "DimensionalTerraria/Textures/Minecraft/redstone_block";
    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<RedstoneBlockTile>());
        Item.width = Item.height = 24; Item.maxStack = 9999;
    }
    public override void AddRecipes()
    {
        CreateRecipe().AddIngredient<Redstone>(9).Register();
        Recipe.Create(ModContent.ItemType<Redstone>(), 9).AddIngredient(Type).Register();
    }
}
