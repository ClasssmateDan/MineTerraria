using DimensionalTerraria.Items;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;

namespace DimensionalTerraria.Systems;

[Autoload(Side = ModSide.Client)]
public sealed class ProjectionSwordAnimation : ModSystem
{
    public override void Load() => On_Player.ItemCheck_ApplyUseStyle += ApplyUseStyle;
    public override void Unload() => On_Player.ItemCheck_ApplyUseStyle -= ApplyUseStyle;

    internal static Rectangle AnimationFrame(Item item, Rectangle frame) => item.ModItem is HigherDimensionalProjection
        ? new Rectangle(frame.X, frame.Y, HigherDimensionalProjection.VisualSize, HigherDimensionalProjection.VisualSize) : frame;

    private static void ApplyUseStyle(On_Player.orig_ItemCheck_ApplyUseStyle original, Player player,
        float mountOffset, Item item, Rectangle frame)
    {
        // Vanilla uses the raw frame width for hand positioning, independently
        // of Item.scale. Keep its animation, but give it the logical sword size.
        original(player, mountOffset, item, AnimationFrame(item, frame));
    }
}
