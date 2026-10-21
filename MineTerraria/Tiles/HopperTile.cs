using DimensionalTerraria.Systems;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace DimensionalTerraria.Tiles;

public sealed class HopperTile : ModTile
{
    public const string ItemTexture = "DimensionalTerraria/Textures/Minecraft/hopper";
    public const string OutsideTexture = "DimensionalTerraria/Textures/Minecraft/hopper_outside";
    public const string InsideTexture = "DimensionalTerraria/Textures/Minecraft/hopper_inside";
    public const string TopTexture = "DimensionalTerraria/Textures/Minecraft/hopper_top";
    public override string Texture => ItemTexture;
    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = Main.tileFrameImportant[Type] = true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
        TileObjectData.newTile.AnchorBottom = default;
        TileObjectData.newTile.LavaDeath = false;
        TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(ModContent.GetInstance<HopperEntity>().AfterPlacement, -1, 0, true);
        TileObjectData.addTile(Type);
        RegisterItemDrop(ModContent.ItemType<Items.Hopper>());
        DustType = DustID.Iron;
        HitSound = SoundID.Tink;
        AddMapEntry(new Color(82, 80, 88), Language.GetText("Mods.DimensionalTerraria.Items.Hopper.DisplayName"));
    }
    public override void PlaceInWorld(int i, int j, Item item) =>
        ModContent.GetInstance<HopperEntity>().AfterPlacement(i, j, Type, 0, 0, 0);
    public override bool TileFrame(int i, int j, ref bool resetFrame, ref bool noBreak)
    {
        Main.tile[i, j].TileFrameX = Main.tile[i, j].TileFrameY = 0;
        return false;
    }
    public override void KillMultiTile(int i, int j, int frameX, int frameY) => ModContent.GetInstance<HopperEntity>().Kill(i, j);
    public override bool Slope(int i, int j) => false;
}
