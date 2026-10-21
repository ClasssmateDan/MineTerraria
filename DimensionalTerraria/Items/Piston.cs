using DimensionalTerraria.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DimensionalTerraria.Items;

public sealed class Piston : ModItem
{
    public override string Texture => "DimensionalTerraria/Textures/Minecraft/piston_side";
    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<PistonTile>());
        Item.width = Item.height = 24; Item.maxStack = 9999;
    }
    public override void AddRecipes() => CreateRecipe().AddIngredient<OakPlanks>(3).AddIngredient(ItemID.StoneBlock, 4)
        .AddIngredient(ItemID.IronBar).AddIngredient<Redstone>().Register();
}
public sealed class StickyPiston : ModItem
{
    public override string Texture => "DimensionalTerraria/Textures/Minecraft/piston_top_sticky";
    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<StickyPistonTile>());
        Item.width = Item.height = 24; Item.maxStack = 9999;
    }
    public override void AddRecipes() => CreateRecipe().AddIngredient<Piston>().AddIngredient(ItemID.Gel).Register();
}
