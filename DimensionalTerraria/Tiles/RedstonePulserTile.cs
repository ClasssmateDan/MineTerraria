using DimensionalTerraria.Systems;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Terraria.Audio;

namespace DimensionalTerraria.Tiles;

public sealed class RedstonePulserTile : ModTile
{
    public const string OffTexture = "DimensionalTerraria/Textures/Minecraft/observer_back";
    public const string OnTexture = "DimensionalTerraria/Textures/Minecraft/observer_back_on";
    public const string SideTexture = "DimensionalTerraria/Textures/Minecraft/observer_side";
    public override string Texture => OffTexture;
    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = Main.tileBlockLight[Type] = true;
        Main.tileFrameImportant[Type] = true; // Persist the per-source phase bit in world tile frames.
        Main.tileMergeDirt[Type] = false;
        TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
        TileObjectData.newTile.AnchorBottom = default;
        TileObjectData.newTile.LavaDeath = false;
        TileObjectData.addTile(Type);
        AnimationFrameHeight = 18;
        DustType = DustID.Stone;
        HitSound = SoundID.Tink;
        RegisterItemDrop(ModContent.ItemType<Items.RedstonePulser>());
        AddMapEntry(new Color(142, 78, 74), Language.GetText("Mods.DimensionalTerraria.Items.RedstonePulser.DisplayName"));
    }
    public override bool TileFrame(int i, int j, ref bool resetFrame, ref bool noBreak)
    {
        Tile tile = Main.tile[i, j];
        tile.TileFrameX = (short)(tile.TileFrameX == 18 ? 18 : 0);
        tile.TileFrameY = 0;
        return false;
    }
    public override bool RightClick(int i, int j)
    {
        if (Main.netMode != NetmodeID.SinglePlayer) return false;
        Tile tile = Main.tile[i, j];
        tile.TileFrameX = (short)(tile.TileFrameX == 18 ? 0 : 18);
        if (!Main.dedServ) SoundEngine.PlaySound(SoundID.MenuTick);
        return true;
    }
    public override void MouseOver(int i, int j)
    {
        Main.LocalPlayer.noThrow = 2;
        Main.LocalPlayer.cursorItemIconEnabled = true;
        Main.LocalPlayer.cursorItemIconID = ModContent.ItemType<Items.RedstonePulser>();
    }
    public override void AnimateIndividualTile(int type, int i, int j, ref int frameXOffset, ref int frameYOffset)
    {
        frameXOffset = -Main.tile[i, j].TileFrameX;
        frameYOffset = RedstoneSystem.PulserOn(Main.tile[i, j]) ? 18 : 0;
    }
    public override void AnimateTile(ref int frame, ref int frameCounter)
    {
        // Visuals follow world simulation; drawing never advances the clock.
        frame = RedstoneSystem.PulseOn ? 1 : 0;
        frameCounter = 0;
    }
}
