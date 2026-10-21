using DimensionalTerraria.Core;
using Terraria.ID;
using Vector2 = System.Numerics.Vector2;

namespace DimensionalTerraria.Items;

public sealed class SlimeSpawnEgg : GroundCreatureItem
{
    public override string Texture => $"Terraria/Images/Item_{ItemID.Gel}";
    protected override int CreatureType => NPCID.BlueSlime;
    public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.Gel, 10).AddIngredient(ItemID.FallenStar).Register();
    internal override bool SpawnPosition(ICollisionWorld world, int x, int y, out Vector2 center, IExplorationMap? exploration = null)
        => GroundSpawnRules.Position(world, x, y, new(24f / 16, 18f / 16), out center, exploration);
}
