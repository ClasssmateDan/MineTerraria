using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DimensionalTerraria.Items;

public sealed class HigherDimensionalPickaxe : ModItem
{
    public override string Texture => "DimensionalTerraria/Textures/Equipment/HigherDimensionalPickaxe";
    public override void SetDefaults()
    {
        Item.width = Item.height = 32;
        Item.damage = 30;
        Item.DamageType = DamageClass.Melee;
        Item.knockBack = 3;
        Item.pick = 350;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.useTime = 3;
        Item.useAnimation = 12;
        Item.autoReuse = true;
        Item.useTurn = true;
        Item.UseSound = SoundID.Item1;
        Item.rare = ItemRarityID.Cyan;
        Item.value = 0;
    }
    public override void AddRecipes() => CreateRecipe().AddIngredient<NetheriteIngot>(3)
        .AddIngredient<OakPlanks>(2).Register();
}
