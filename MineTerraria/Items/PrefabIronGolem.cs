using DimensionalTerraria.Core;
using DimensionalTerraria.NPCs;
using Terraria.ID;
using Terraria.ModLoader;
using Vector2 = System.Numerics.Vector2;

namespace DimensionalTerraria.Items;

public sealed class PrefabIronGolem : GroundCreatureItem
{
    // Replaced with a cropped single-pose thumbnail during client setup.
    public override string Texture => "DimensionalTerraria/Textures/IronGolem";
    protected override int CreatureType => ModContent.NPCType<MinecraftIronGolem>();
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.width = 32; Item.height = 40;
    }
    internal override bool SpawnPosition(ICollisionWorld world, int x, int y, out Vector2 center, IExplorationMap? exploration = null)
        => GroundSpawnRules.IronGolemPosition(world, x, y, out center, exploration);
    public override void AddRecipes() => CreateRecipe().AddIngredient(Mod.Find<ModItem>("IronStorageBlock").Type, 4)
        .AddIngredient(ItemID.Pumpkin).Register();
}
