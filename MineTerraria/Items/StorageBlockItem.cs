using DimensionalTerraria.Content;
using DimensionalTerraria.Tiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DimensionalTerraria.Items;

[Autoload(false)]
public sealed class StorageBlockItem : ModItem
{
    public StorageMaterial Material { get; }
    public StorageBlockItem(StorageMaterial material) => Material = material;
    protected override bool CloneNewInstances => true;
    public override string Name => Material.Key + "StorageBlock";
    // The client replaces only this mod item's texture with a 24px pixel cube.
    public override string Texture => $"Terraria/Images/Item_{Material.Ingredient}";
    public override void SetDefaults()
    {
        Item.DefaultToPlaceableTile(Mod.Find<ModTile>(Material.Key + "StorageTile").Type);
        Item.width = Item.height = 24;
        Item.maxStack = 9999;
        Item.value = 0;
    }
    public override void AddRecipes()
    {
        CreateRecipe().AddIngredient(Material.Ingredient, 9).Register();
        Recipe.Create(Material.Ingredient, 9).AddIngredient(Type).Register();
    }
}
