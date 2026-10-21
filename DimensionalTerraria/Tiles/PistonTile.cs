using DimensionalTerraria.Systems;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;
using DimensionalTerraria.Items;
using Terraria.Localization;

namespace DimensionalTerraria.Tiles;

public abstract class PistonBaseTile : ModTile
{
    public override string Texture => "DimensionalTerraria/Textures/Minecraft/piston_side";
    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = Main.tileBlockLight[Type] = Main.tileFrameImportant[Type] = true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
        TileObjectData.newTile.AnchorBottom = default;
        TileObjectData.newTile.LavaDeath = false;
        TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(ModContent.GetInstance<PistonEntity>().AfterPlacement, -1, 0, true);
        TileObjectData.addTile(Type);
        RegisterItemDrop(this is StickyPistonTile ? ModContent.ItemType<StickyPiston>() : ModContent.ItemType<Piston>());
        DustType = DustID.WoodFurniture; HitSound = SoundID.Tink;
        AddMapEntry(new Color(145, 132, 104), Language.GetText("Mods.DimensionalTerraria.Items." +
            (this is StickyPistonTile ? "StickyPiston" : "Piston") + ".DisplayName"));
    }
    public override bool TileFrame(int i, int j, ref bool resetFrame, ref bool noBreak) => false;
    public override void PlaceInWorld(int i, int j, Item item)
    {
        if (Main.netMode != NetmodeID.SinglePlayer) return;
        ModContent.GetInstance<PistonEntity>().AfterPlacement(i, j, Type, 0, Main.LocalPlayer.direction, 0);
    }
    public override bool RightClick(int i, int j)
    {
        if (Main.netMode != NetmodeID.SinglePlayer || !TileEntity.ByPosition.TryGetValue(new Point16(i, j), out var entity) || entity is not PistonEntity piston) return false;
        return piston.Rotate();
    }
    public override void KillMultiTile(int i, int j, int frameX, int frameY) => PistonEntity.Destroy(i, j, frameX / 18 & 3);
    public override bool Slope(int i, int j) => false;
}
public sealed class PistonTile : PistonBaseTile { }
public sealed class StickyPistonTile : PistonBaseTile { }

public sealed class PistonHeadTile : ModTile
{
    public override string Texture => "DimensionalTerraria/Textures/Minecraft/piston_side";
    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = Main.tileFrameImportant[Type] = true;
        Main.tileNoFail[Type] = true;
        DustType = DustID.WoodFurniture; HitSound = SoundID.Tink;
        AddMapEntry(new Color(170, 145, 100), Language.GetText("Mods.DimensionalTerraria.Items.Piston.DisplayName"));
    }
    public override bool TileFrame(int i, int j, ref bool resetFrame, ref bool noBreak) => false;
    public override bool CanDrop(int i, int j) => false;
    public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem)
    {
        noItem = true;
    }
    public override bool Slope(int i, int j) => false;
}
