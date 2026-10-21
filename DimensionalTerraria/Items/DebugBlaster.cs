using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DimensionalTerraria.Items;

public sealed class DebugBlaster : ModItem
{
    public override string Texture => $"Terraria/Images/Item_{ItemID.FlintlockPistol}";

    public override void SetDefaults()
    {
        Item.CloneDefaults(ItemID.FlintlockPistol);
        Item.damage = 999;
        Item.DamageType = DamageClass.Ranged;
        Item.useTime = Item.useAnimation = 18;
        Item.knockBack = 4;
        Item.useAmmo = AmmoID.None;
        Item.shoot = ProjectileID.Bullet;
        Item.shootSpeed = 16;
        Item.autoReuse = true;
        Item.rare = ItemRarityID.Cyan;
        Item.value = 0;
    }
}
