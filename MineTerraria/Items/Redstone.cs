using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DimensionalTerraria.Items;

public sealed class Redstone : ModItem
{
    public override string Texture => "DimensionalTerraria/Textures/Minecraft/redstone";
    public override void SetDefaults()
    {
        Item.width = Item.height = 16; Item.maxStack = 9999;
        Item.material = true; Item.rare = ItemRarityID.White;
    }
    public override void AddRecipes() => CreateRecipe(4).AddIngredient(ItemID.Ruby)
        .AddIngredient(ItemID.StoneBlock, 4).Register();
}
