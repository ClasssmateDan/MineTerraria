using DimensionalTerraria.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DimensionalTerraria.Items;

public sealed class RedstonePulser : ModItem
{
    public override string Texture => RedstonePulserTile.OnTexture;
    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<RedstonePulserTile>());
        Item.width = Item.height = 24;
        Item.maxStack = 9999;
    }
    public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.StoneBlock, 4)
        .AddIngredient<Redstone>(2).AddIngredient(ItemID.IronBar).Register();
}
