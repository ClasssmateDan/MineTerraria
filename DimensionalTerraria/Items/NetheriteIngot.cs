using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DimensionalTerraria.Items;

public sealed class NetheriteIngot : ModItem
{
    public override string Texture => "DimensionalTerraria/Textures/Minecraft/netherite_ingot";
    public override void SetDefaults()
    {
        Item.width = Item.height = 16;
        Item.maxStack = 9999;
        Item.material = true;
        Item.rare = ItemRarityID.Red;
        Item.value = Item.sellPrice(gold: 1);
    }
    public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.HellstoneBar, 4)
        .AddIngredient(ItemID.ChlorophyteBar, 4).AddIngredient(ItemID.LunarBar).Register();
}
