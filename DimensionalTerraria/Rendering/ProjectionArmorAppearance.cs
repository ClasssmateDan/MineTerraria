using Terraria;
using Terraria.ModLoader;

namespace DimensionalTerraria.Rendering;

internal static class ProjectionArmorAppearance
{
    internal static bool HeadVisible(Player player) => player.head > 0 &&
        player.head == ModContent.GetInstance<Items.HigherDimensionalHelmet>().Item.headSlot;
    internal static bool ChestVisible(Player player) => player.body > 0 &&
        player.body == ModContent.GetInstance<Items.HigherDimensionalChestplate>().Item.bodySlot;
    internal static bool LegsVisible(Player player) => player.legs > 0 &&
        player.legs == ModContent.GetInstance<Items.HigherDimensionalLeggings>().Item.legSlot;
}
