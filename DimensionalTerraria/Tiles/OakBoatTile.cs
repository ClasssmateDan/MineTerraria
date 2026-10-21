using DimensionalTerraria.Items;
using DimensionalTerraria.Core;
using DimensionalTerraria.World;
using DimensionalTerraria.Systems;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Microsoft.Xna.Framework.Graphics;

namespace DimensionalTerraria.Tiles;

public sealed class OakBoatTile : ModTile
{
    public override string Texture => "DimensionalTerraria/Textures/Minecraft/oak_boat";
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        Main.tileCut[Type] = true; Main.tileNoFail[Type] = true;
        Main.tileSolid[Type] = false;
        TileID.Sets.DisableSmartCursor[Type] = true;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x2);
        TileObjectData.newTile.Origin = new Point16(1, 1);
        TileObjectData.newTile.AnchorBottom = default;
        TileObjectData.newTile.AnchorWall = false;
        TileObjectData.newTile.WaterDeath = TileObjectData.newTile.LavaDeath = false;
        TileObjectData.newTile.WaterPlacement = LiquidPlacement.Allowed;
        TileObjectData.newTile.LavaPlacement = LiquidPlacement.Allowed;
        TileObjectData.addTile(Type);
        RegisterItemDrop(ModContent.ItemType<OakBoat>());
        DustType = DustID.WoodFurniture; HitSound = SoundID.Dig;
        AddMapEntry(new Color(153, 112, 64), Language.GetText("Mods.DimensionalTerraria.Items.OakBoat.DisplayName"));
    }
    public override void KillMultiTile(int i, int j, int frameX, int frameY) => ModContent.GetInstance<OakBoatEntity>().Kill(i, j);
    public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) => false;
    public override void SetDrawPositions(int i, int j, ref int width, ref int offsetY, ref int height, ref short tileFrameX, ref short tileFrameY)
    {
        Tile tile = Main.tile[i, j];
        int left = i - tile.TileFrameX / 18, bottom = j - tile.TileFrameY / 18 + 2;
        if (BoatRules.Support(new TerrariaCollisionWorld(), left, bottom, out float surface))
            offsetY += (int)((surface - bottom) * 16);
    }
}
