using DimensionalTerraria.Systems;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace DimensionalTerraria.Tiles;

public sealed class BedrockTile : ModTile
{
    public override string Texture => "DimensionalTerraria/Textures/Minecraft/bedrock";

    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = Main.tileBlockLight[Type] = true;
        Main.tileMergeDirt[Type] = false;
        MinPick = 350;
        MineResist = 1;
        DustType = DustID.Stone;
        HitSound = SoundID.Tink;
        RegisterItemDrop(ModContent.ItemType<Items.Bedrock>());
        AddMapEntry(new Color(80, 80, 80), Language.GetText("Mods.DimensionalTerraria.Items.Bedrock.DisplayName"));
    }

    // Permission belongs to one synchronous 3D pick operation at one cell.
    // A detached player alone must never authorize explosions or ranged mining.
    public override bool CanKillTile(int i, int j, ref bool blockDamaged) => BedrockMining.Allows(i, j);
    public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem)
    {
        // Some native destruction paths call KillTile without CanKillTile first.
        if (!BedrockMining.Allows(i, j)) { fail = true; noItem = true; }
    }
    public override bool CanExplode(int i, int j) => false;
    public override bool CanReplace(int i, int j, int tileTypeBeingPlaced) => false;
    public override bool Slope(int i, int j) => false;

    public override bool TileFrame(int i, int j, ref bool resetFrame, ref bool noBreak)
    {
        Main.tile[i, j].TileFrameX = Main.tile[i, j].TileFrameY = 0;
        return false;
    }
}
