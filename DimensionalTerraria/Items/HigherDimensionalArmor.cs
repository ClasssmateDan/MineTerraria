using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DimensionalTerraria.Items;

[AutoloadEquip(EquipType.Head)]
public sealed class HigherDimensionalHelmet : ModItem
{
    public override string Texture => "DimensionalTerraria/Textures/Equipment/HigherDimensionalHelmet";
    public override void SetStaticDefaults()
    {
        if (Item.headSlot >= 0) ArmorIDs.Head.Sets.DrawHatHair[Item.headSlot] = false;
    }
    public override void SetDefaults()
    {
        Item.width = Item.height = 24;
        Item.defense = 24;
        Item.rare = ItemRarityID.Cyan;
    }
    public override bool IsArmorSet(Item head, Item body, Item legs) =>
        body.type == ModContent.ItemType<HigherDimensionalChestplate>() && legs.type == ModContent.ItemType<HigherDimensionalLeggings>();

    public override void AddRecipes() => CreateRecipe().AddIngredient<NetheriteIngot>(5).Register();

    public override void UpdateArmorSet(Player player)
    {
        player.setBonus = this.GetLocalization("SetBonus").Value;
        player.lavaImmune = player.fireWalk = player.noFallDmg = player.noKnockback = true;
        foreach (int buff in ProtectionDebuffs) player.buffImmune[buff] = true;
    }

    // Environmental and common combat ailments only. Potion sickness,
    // mana sickness and encounter-specific debuffs keep their normal rules.
    internal static readonly int[] ProtectionDebuffs = {
        BuffID.Poisoned, BuffID.Venom, BuffID.Bleeding, BuffID.Weak, BuffID.BrokenArmor,
        BuffID.Slow, BuffID.Confused, BuffID.Silenced, BuffID.Darkness, BuffID.Cursed,
        BuffID.Stoned, BuffID.Chilled, BuffID.Frozen, BuffID.OnFire, BuffID.OnFire3,
        BuffID.CursedInferno, BuffID.Frostburn, BuffID.Frostburn2, BuffID.Blackout, BuffID.Ichor,
        BuffID.Burning
    };
}

[AutoloadEquip(EquipType.Body)]
public sealed class HigherDimensionalChestplate : ModItem
{
    public override string Texture => "DimensionalTerraria/Textures/Equipment/HigherDimensionalChestplate";
    public override void SetDefaults()
    {
        Item.width = Item.height = 24;
        Item.defense = 34;
        Item.rare = ItemRarityID.Cyan;
    }
    public override void AddRecipes() => CreateRecipe().AddIngredient<NetheriteIngot>(8).Register();
}

[AutoloadEquip(EquipType.Legs)]
public sealed class HigherDimensionalLeggings : ModItem
{
    public override string Texture => "DimensionalTerraria/Textures/Equipment/HigherDimensionalLeggings";
    public override void SetDefaults()
    {
        Item.width = Item.height = 24;
        Item.defense = 20;
        Item.rare = ItemRarityID.Cyan;
    }
    public override void AddRecipes() => CreateRecipe().AddIngredient<NetheriteIngot>(7).Register();
}
