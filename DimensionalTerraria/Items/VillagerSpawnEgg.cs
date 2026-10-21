using DimensionalTerraria.Core;
using DimensionalTerraria.NPCs;
using Terraria.ID;
using Terraria.ModLoader;
using Vector2 = System.Numerics.Vector2;

namespace DimensionalTerraria.Items;

public sealed class VillagerSpawnEgg : GroundCreatureItem
{
    public override string Texture => $"Terraria/Images/Item_{ItemID.Acorn}";
    protected override int CreatureType => ModContent.NPCType<MinecraftVillager>();
    public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.Silk, 5)
        .AddRecipeGroup(Systems.MinecraftRecipes.WoodGroup, 10).AddIngredient(ItemID.FallenStar).Register();
    internal override bool SpawnPosition(ICollisionWorld world, int x, int y, out Vector2 center, IExplorationMap? exploration = null) =>
        GroundSpawnRules.Position(world, x, y, new(VillagerMemory.Width / 16f, VillagerMemory.Height / 16f), out center, exploration);
}
