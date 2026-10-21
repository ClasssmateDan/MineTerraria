using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DimensionalTerraria.Items;

public sealed class Bedrock : ModItem
{
    public override string Texture => "DimensionalTerraria/Textures/Minecraft/bedrock";
    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(ModContent.TileType<Tiles.BedrockTile>());
        Item.width = Item.height = 24;
        Item.maxStack = 9999;
        Item.rare = ItemRarityID.Cyan;
        Item.value = 0;
    }
    public override void AddRecipes() => CreateRecipe(4).AddIngredient(ItemID.StoneBlock, 10)
        .AddIngredient(ItemID.Obsidian).AddIngredient<NetheriteIngot>().Register();
}
