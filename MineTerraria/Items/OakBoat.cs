using DimensionalTerraria.Core;
using DimensionalTerraria.Systems;
using DimensionalTerraria.Tiles;
using DimensionalTerraria.World;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DimensionalTerraria.Items;

public sealed class OakBoat : ModItem
{
    private bool placed;
    private readonly TerrariaCollisionWorld world = new();
    public override string Texture => "DimensionalTerraria/Textures/Minecraft/oak_boat";
    public override void SetDefaults()
    {
        placed = false;
        Item.width = Item.height = 24; Item.maxStack = 999;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.useTime = Item.useAnimation = 24;
        Item.consumable = true; Item.UseSound = SoundID.Dig;
    }
    public override bool CanUseItem(Player player) { placed = false; return FindPlace(player, out _); }
    public override bool? UseItem(Player player) => placed || FindPlace(player, out BoatPlacement position) && TryPlace(player, position);
    public override bool ConsumeItem(Player player) => placed;
    private bool FindPlace(Player player, out BoatPlacement position)
    {
        position = default;
        if (Main.netMode != NetmodeID.SinglePlayer) return false;
        int x = (int)(Main.MouseWorld.X / 16), y = (int)(Main.MouseWorld.Y / 16);
        for (int down = 0; down < 6; down++)
            if (BoatRules.TryPlacement(world, x, y + down, out position) &&
                Vector2.Distance(player.Center, new Vector2(x + 0.5f, position.SurfaceY - 1) * 16) <= 7 * 16 &&
                Collision.CanHitLine(player.Center, 1, 1, new Vector2(x + 0.5f, position.SurfaceY - 1) * 16, 1, 1)) return true;
        return false;
    }
    internal bool TryPlace(Player player, BoatPlacement position, IExplorationMap? exploration = null)
    {
        placed = false;
        if (Main.netMode != NetmodeID.SinglePlayer || player.noItems || player.CCed ||
            !BoatRules.TryPlacement(world, position.X + 1, position.Y + 2, out _, exploration)) return false;
        // Validate the complete footprint before changing anything. Preview
        // checks never cut foliage; actual placement uses native hooks/drops.
        for (int x = position.X; x < position.X + BoatRules.Width; x++)
        for (int y = position.Y; y < position.Y + BoatRules.Height; y++)
        {
            if (!Main.tile[x, y].HasTile) continue;
            if (!TerrariaCollisionWorld.CanClearForBoat(x, y)) return false;
            WorldGen.KillTile(x, y);
            if (Main.tile[x, y].HasTile) return false;
        }
        if (!BoatRules.TryPlacement(world, position.X + 1, position.Y + 2, out _, exploration)) return false;
        if (!WorldGen.PlaceObject(position.X + 1, position.Y + 1, ModContent.TileType<OakBoatTile>(), mute: true)) return false;
        ModContent.GetInstance<OakBoatEntity>().Place(position.X, position.Y);
        return placed = true;
    }
    public override void AddRecipes() => CreateRecipe().AddIngredient<OakPlanks>(5).Register();
}
