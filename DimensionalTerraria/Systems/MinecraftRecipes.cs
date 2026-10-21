using DimensionalTerraria.Items;
using DimensionalTerraria.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace DimensionalTerraria.Systems;

public sealed class MinecraftRecipes : ModSystem
{
    internal const string WoodGroup = "DimensionalTerraria:AnyTerrariaWood";
    public override void AddRecipeGroups() => RecipeGroup.RegisterGroup(WoodGroup,
        new RecipeGroup(() => Language.GetTextValue("Mods.DimensionalTerraria.Recipes.AnyTerrariaWood"),
            ItemID.Wood, ItemID.Ebonwood, ItemID.RichMahogany, ItemID.Pearlwood, ItemID.Shadewood,
            ItemID.SpookyWood, ItemID.BorealWood, ItemID.PalmWood, ItemID.DynastyWood, ItemID.AshWood));

    public override void PostAddRecipes()
    {
        int planks = ModContent.ItemType<OakPlanks>(), tableItem = ModContent.ItemType<MinecraftCraftingTable>();
        int tableTile = ModContent.TileType<MinecraftCraftingTableTile>();
        for (int i = 0; i < Recipe.numRecipes; i++)
        {
            Recipe recipe = Main.recipe[i];
            // Include reversible block recipes even when their result is a vanilla bar or gem.
            // Both wood recipes are deliberate bootstrap exceptions.
            if (recipe.Mod != Mod || recipe.createItem.type == planks || recipe.createItem.type == tableItem) continue;
            if (!recipe.requiredTile.Contains(tableTile)) recipe.AddTile(tableTile);
        }
    }
}
