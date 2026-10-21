using DimensionalTerraria.Core;
using DimensionalTerraria.NPCs;
using Terraria.ID;
using Terraria.ModLoader;
using Vector2 = System.Numerics.Vector2;

namespace DimensionalTerraria.Items;

public sealed class SheepSpawnEgg : GroundCreatureItem
{
    public override string Texture => $"Terraria/Images/Item_{ItemID.Silk}";
    protected override int CreatureType => ModContent.NPCType<MinecraftSheep>();
    public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.Silk, 5)
        .AddIngredient(ItemID.Hay, 10).AddIngredient(ItemID.FallenStar).Register();
    internal override bool SpawnPosition(ICollisionWorld world, int x, int y, out Vector2 center, IExplorationMap? exploration = null)
        => GroundSpawnRules.SheepPosition(world, x, y, out center, exploration);
}
