using System.Collections.Generic;
using System.IO;
using DimensionalTerraria.Content;
using DimensionalTerraria.Core;
using DimensionalTerraria.Items;
using DimensionalTerraria.NPCs;
using DimensionalTerraria.Tiles;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace DimensionalTerraria.Systems;

[Autoload(Side = ModSide.Client)]
public sealed class StorageTextures : ModSystem
{
    private readonly List<Asset<Texture2D>> generated = new();
    private static readonly Dictionary<string, Asset<Texture2D>> sprites = new();
    internal static Asset<Texture2D> Sprite(string path) => sprites.TryGetValue(path, out var asset) ? asset :
        ModContent.Request<Texture2D>(path, AssetRequestMode.ImmediateLoad);
    public override void PostSetupContent()
    {
        // Pixel preparation is CPU-only. The asset reader transfers the final
        // generated textures to the graphics thread for ImmediateLoad.
        foreach (string name in new[] { "Steve", "Sheep", "Villager", "IronGolem" })
        {
            byte[] raw = StorageIconPixels.Read(Mod, "Textures/" + name, out int sw, out int sh);
            sprites["DimensionalTerraria/Textures/" + name] = Make(name + "Clean", sw, sh, SpriteEdges.Clean(raw, sw, sh));
            if (name == "Steve") foreach (string part in new[] { "Head", "Body", "Legs" })
            {
                string path = "Textures/Equipment/SteveArmor_" + part;
                byte[] overlay = StorageIconPixels.Read(Mod, path, out int aw, out int ah);
                if (aw != sw || ah != sh) throw new InvalidDataException("Steve armor silhouette mismatch");
                sprites["DimensionalTerraria/" + path] = Make(part + "Clean", aw, ah, SpriteEdges.Clean(overlay, aw, ah, raw));
            }
        }
        TextureAssets.Npc[ModContent.NPCType<MinecraftSheep>()] = sprites["DimensionalTerraria/Textures/Sheep"];
        TextureAssets.Npc[ModContent.NPCType<MinecraftVillager>()] = sprites["DimensionalTerraria/Textures/Villager"];
        TextureAssets.Npc[ModContent.NPCType<MinecraftIronGolem>()] = sprites["DimensionalTerraria/Textures/IronGolem"];
        foreach (StorageMaterial material in StorageMaterials.All)
        {
            material.TileType = Mod.Find<ModTile>(material.Key + "StorageTile").Type;
            material.ItemType = Mod.Find<ModItem>(material.Key + "StorageBlock").Type;
            TextureAssets.Tile[material.TileType] = Make(material.Key + "Tile", 16, 16, ContentPixelArt.BlockFace(material.Color, material.Gem));
            TextureAssets.Item[material.ItemType] = Make(material.Key + "Item", 24, 24, ContentPixelArt.Cube(material.Color, material.Gem));
        }
        TextureAssets.Item[ModContent.ItemType<SheepSpawnEgg>()] = Make("SheepEgg", 16, 24, ContentPixelArt.SheepEgg());
        TextureAssets.Item[ModContent.ItemType<SlimeSpawnEgg>()] = Make("SlimeEgg", 16, 24, ContentPixelArt.SlimeEgg());
        TextureAssets.Item[ModContent.ItemType<VillagerSpawnEgg>()] = Make("VillagerEgg", 16, 24, ContentPixelArt.VillagerEgg());
        byte[] chest = StorageIconPixels.Read(Mod, "Textures/Minecraft/chest_normal", out int cw, out int ch);
        if (cw != 64 || ch != 64) throw new InvalidDataException("Chest texture must retain the original 64x64 UV layout");
        TextureAssets.Tile[ModContent.TileType<MinecraftChestTile>()] = Make("MinecraftChestAtlas", 36, 114, ChestArt.Atlas(chest));
        TextureAssets.Item[ModContent.ItemType<MinecraftChest>()] = Make("MinecraftChestIcon", 24, 24,
            ContentPixelArt.TexturedCube(ChestArt.Face(chest, 0), ChestArt.Face(chest, 1), ChestArt.Face(chest, 2)));
        byte[] planks = StorageIconPixels.ReadFace(Mod, "Textures/Minecraft/oak_planks");
        byte[] bedrock = StorageIconPixels.ReadFace(Mod, "Textures/Minecraft/bedrock");
        TextureAssets.Item[ModContent.ItemType<Bedrock>()] = Make("BedrockCube", 24, 24, ContentPixelArt.TexturedCube(bedrock, bedrock, bedrock));
        TextureAssets.Item[ModContent.ItemType<OakPlanks>()] = Make("OakPlanksCube", 24, 24, ContentPixelArt.TexturedCube(planks, planks, planks));
        TextureAssets.Item[ModContent.ItemType<MinecraftCraftingTable>()] = Make("CraftingTableCube", 24, 24,
            ContentPixelArt.TexturedCube(StorageIconPixels.ReadFace(Mod, MinecraftCraftingTableTile.TopTexture),
                StorageIconPixels.ReadFace(Mod, MinecraftCraftingTableTile.FrontTexture), StorageIconPixels.ReadFace(Mod, MinecraftCraftingTableTile.SideTexture)));
        TextureAssets.Item[ModContent.ItemType<PrefabIronGolem>()] = Make("PrefabGolemThumbnail", 32, 40,
            StorageIconPixels.GolemThumbnail(Mod));
        TextureAssets.Tile[ModContent.TileType<OakBoatTile>()] = Make("OakBoatAtlas", 54, 36,
            ContentPixelArt.BoatTileAtlas(StorageIconPixels.ReadFace(Mod, "Textures/Minecraft/oak_boat")));
        byte[] redstone = StorageIconPixels.ReadFace(Mod, "Textures/Minecraft/redstone_block");
        TextureAssets.Item[ModContent.ItemType<RedstoneBlock>()] = Make("RedstoneCube", 24, 24, ContentPixelArt.TexturedCube(redstone, redstone, redstone));
        byte[] pulseOff = StorageIconPixels.ReadFace(Mod, RedstonePulserTile.OffTexture);
        byte[] pulseOn = StorageIconPixels.ReadFace(Mod, RedstonePulserTile.OnTexture);
        byte[] pulseSide = StorageIconPixels.ReadFace(Mod, RedstonePulserTile.SideTexture);
        TextureAssets.Item[ModContent.ItemType<RedstonePulser>()] = Make("RedstonePulserCube", 24, 24,
            ContentPixelArt.TexturedCube(pulseSide, pulseOn, pulseSide));
        TextureAssets.Tile[ModContent.TileType<RedstonePulserTile>()] = Make("RedstonePulserAtlas", 16, 36,
            ContentPixelArt.PulseAtlas(pulseOff, pulseOn));
        byte[] side = StorageIconPixels.ReadFace(Mod, "Textures/Minecraft/piston_side");
        byte[] inner = StorageIconPixels.ReadFace(Mod, "Textures/Minecraft/piston_inner");
        byte[] normalTop = StorageIconPixels.ReadFace(Mod, "Textures/Minecraft/piston_top");
        byte[] stickyTop = StorageIconPixels.ReadFace(Mod, "Textures/Minecraft/piston_top_sticky");
        TextureAssets.Item[ModContent.ItemType<Piston>()] = Make("PistonCube", 24, 24, ContentPixelArt.TexturedCube(normalTop, side, side));
        TextureAssets.Item[ModContent.ItemType<StickyPiston>()] = Make("StickyPistonCube", 24, 24, ContentPixelArt.TexturedCube(stickyTop, side, side));
        TextureAssets.Tile[ModContent.TileType<PistonTile>()] = Make("PistonAtlas", 72, 36, ContentPixelArt.PistonAtlas(side, inner, normalTop, stickyTop, false, false));
        TextureAssets.Tile[ModContent.TileType<StickyPistonTile>()] = Make("StickyPistonAtlas", 72, 36, ContentPixelArt.PistonAtlas(side, inner, normalTop, stickyTop, true, false));
        TextureAssets.Tile[ModContent.TileType<PistonHeadTile>()] = Make("PistonHeadAtlas", 72, 36, ContentPixelArt.PistonAtlas(side, inner, normalTop, stickyTop, false, true));
    }
    private Asset<Texture2D> Make(string name, int width, int height, byte[] pixels)
    {
        using var stream = new MemoryStream(ContentPixelArt.RawImage(width, height, pixels));
        Asset<Texture2D> asset = Mod.Assets.CreateUntracked<Texture2D>(stream, name + ".rawimg", AssetRequestMode.ImmediateLoad);
        generated.Add(asset);
        return asset;
    }
    public override void Unload()
    {
        Asset<Texture2D>[] old = generated.ToArray();
        generated.Clear();
        sprites.Clear();
        Main.QueueMainThreadAction(() => { foreach (Asset<Texture2D> asset in old) asset.Dispose(); });
    }
}
