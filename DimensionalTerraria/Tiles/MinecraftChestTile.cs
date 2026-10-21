using DimensionalTerraria.Items;
using DimensionalTerraria.Systems;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent.ObjectInteractions;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace DimensionalTerraria.Tiles;

public sealed class MinecraftChestTile : ModTile
{
    public override string Texture => "DimensionalTerraria/Textures/Minecraft/chest_normal";
    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = Main.tileContainer[Type] = Main.tileNoAttach[Type] = true;
        TileID.Sets.BasicChest[Type] = TileID.Sets.IsAContainer[Type] = TileID.Sets.DisableSmartCursor[Type] = true;
        TileID.Sets.AvoidedByNPCs[Type] = TileID.Sets.InteractibleByNPCs[Type] = true;
        TileID.Sets.GeneralPlacementTiles[Type] = false;
        AdjTiles = new int[] { TileID.Containers };
        VanillaFallbackOnModDeletion = TileID.Containers;
        MinPick = 0; MineResist = .15f;
        DustType = DustID.WoodFurniture; HitSound = SoundID.Dig;
        RegisterItemDrop(ModContent.ItemType<MinecraftChest>());
        TileObjectData.newTile.CopyFrom(TileObjectData.Style2x2);
        TileObjectData.newTile.Origin = new Point16(0, 1);
        TileObjectData.newTile.CoordinateHeights = new[] { 16, 18 };
        TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.newTile.LavaDeath = false;
        TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, 2, 0);
        TileObjectData.newTile.HookCheckIfCanPlace = new PlacementHook(Chest.FindEmptyChest, -1, 0, true);
        TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(Chest.AfterPlacement_Hook, -1, 0, false);
        TileObjectData.addTile(Type);
        AddMapEntry(new Color(163, 111, 43), DefaultContainerName(0, 0), MapName);
    }
    internal static Point16 Origin(int i, int j) => new(i - Main.tile[i, j].TileFrameX / 18 % 2, j - Main.tile[i, j].TileFrameY / 18 % 2);
    public override LocalizedText DefaultContainerName(int frameX, int frameY) => Language.GetText("Mods.DimensionalTerraria.Items.MinecraftChest.DisplayName");
    public override bool IsLockedChest(int i, int j) => false;
    public override bool HasSmartInteract(int i, int j, SmartInteractScanSettings settings) => true;
    public override void PlaceInWorld(int i, int j, Item item)
    {
        Point16 at = Origin(i, j);
        if (Chest.FindChest(at.X, at.Y) < 0) Chest.CreateChest(at.X, at.Y);
    }
    public override bool CanKillTile(int i, int j, ref bool blockDamaged)
    {
        Point16 at = Origin(i, j);
        return Chest.CanDestroyChest(at.X, at.Y);
    }
    public override bool CanExplode(int i, int j) { bool damaged = false; return CanKillTile(i, j, ref damaged); }
    public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem)
    {
        bool damaged = false;
        if (!CanKillTile(i, j, ref damaged)) { fail = true; noItem = true; }
    }
    public override void KillMultiTile(int i, int j, int frameX, int frameY) => Chest.DestroyChest(i, j);
    public override bool RightClick(int i, int j)
    {
        if (Main.netMode != NetmodeID.SinglePlayer || DimensionSystem.IsDetached(Main.LocalPlayer)) return false;
        Point16 at = Origin(i, j);
        int index = Chest.FindChest(at.X, at.Y);
        if (index < 0) return false;
        Player player = Main.LocalPlayer;
        player.CloseSign(); player.SetTalkNPC(-1);
        Main.npcChatCornerItem = 0; Main.npcChatText = ""; Main.editChest = false; player.editedChestName = false;
        Main.mouseRightRelease = false; Main.stackSplit = 600;
        bool close = player.chest == index;
        if (close) player.chest = -1;
        else player.OpenChest(at.X, at.Y, index);
        if (!Main.dedServ)
        {
            SoundEngine.PlaySound(close ? SoundID.MenuClose : SoundID.MenuOpen);
            Recipe.FindRecipes();
        }
        return true;
    }
    public override void MouseOver(int i, int j)
    {
        var at = Origin(i, j);
        int index = Chest.FindChest(at.X, at.Y);
        Player player = Main.LocalPlayer;
        player.noThrow = 2; player.cursorItemIconEnabled = true;
        player.cursorItemIconID = ModContent.ItemType<MinecraftChest>();
        player.cursorItemIconText = index >= 0 ? Main.chest[index].name : "";
    }
    private static string MapName(string name, int i, int j)
    {
        var at = Origin(i, j);
        int index = Chest.FindChest(at.X, at.Y);
        return index >= 0 && Main.chest[index].name.Length > 0 ? name + ": " + Main.chest[index].name : name;
    }
}
