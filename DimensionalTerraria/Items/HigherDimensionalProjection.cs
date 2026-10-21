using DimensionalTerraria.Core;
using DimensionalTerraria.Systems;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DimensionalTerraria.Items;

[LegacyName("DebugBlade")]
public sealed class HigherDimensionalProjection : ModItem
{
    internal const int VisualSize = 48;
    public override string Texture => "DimensionalTerraria/Textures/HigherDimensionalProjection";

    public override void SetDefaults()
    {
        Item.CloneDefaults(ItemID.IronBroadsword);
        Item.damage = ProjectionWeaponStats.NormalDamage;
        Item.DamageType = DamageClass.Melee;
        Item.useTime = Item.useAnimation = 18;
        Item.width = Item.height = 40;
        // The generated source is 1254px; native 2D held-item drawing uses scale.
        Item.scale = VisualSize / 1254f;
        Item.knockBack = 4;
        Item.autoReuse = true;
        Item.rare = ItemRarityID.Cyan;
        Item.value = 0;
    }

    public override void ModifyWeaponDamage(Player player, ref StatModifier damage)
    {
        if (DimensionSystem.IsDetached(player)) damage.Base += ProjectionWeaponStats.DetachedDamage - ProjectionWeaponStats.NormalDamage;
    }

    public override void MeleeEffects(Player player, Rectangle hitbox)
    {
        if (Main.rand.NextBool(3)) Dust.NewDust(hitbox.TopLeft(), hitbox.Width, hitbox.Height, DustID.Shadowflame);
    }

    public override void AddRecipes() => CreateRecipe().AddIngredient<NetheriteIngot>(2)
        .AddIngredient<OakPlanks>().Register();
}
