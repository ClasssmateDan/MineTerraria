using System.Collections.Generic;
using Terraria.ID;
using Terraria.ModLoader;
using DimensionalTerraria.Items;
using DimensionalTerraria.Tiles;

namespace DimensionalTerraria.Content;

public sealed record StorageMaterial(string Key, int RawIngredient, int Color, bool Gem = false, int BarIngredient = ItemID.None)
{
    public int Ingredient => BarIngredient != ItemID.None ? BarIngredient : RawIngredient;
    public int ItemType { get; internal set; }
    public int TileType { get; internal set; }
}

public static class StorageMaterials
{
    public static readonly IReadOnlyList<StorageMaterial> All = new StorageMaterial[] {
        new("Copper", ItemID.CopperOre, 0xBA7045, BarIngredient: ItemID.CopperBar), new("Tin", ItemID.TinOre, 0xB6AD94, BarIngredient: ItemID.TinBar),
        new("Iron", ItemID.IronOre, 0xCDD0D4, BarIngredient: ItemID.IronBar), new("Lead", ItemID.LeadOre, 0x626C91, BarIngredient: ItemID.LeadBar),
        new("Silver", ItemID.SilverOre, 0xDCE9EF, BarIngredient: ItemID.SilverBar), new("Tungsten", ItemID.TungstenOre, 0x8FAE9A, BarIngredient: ItemID.TungstenBar),
        new("Gold", ItemID.GoldOre, 0xF6CF45, BarIngredient: ItemID.GoldBar), new("Platinum", ItemID.PlatinumOre, 0xD7E1D5, BarIngredient: ItemID.PlatinumBar),
        new("Demonite", ItemID.DemoniteOre, 0x615086, BarIngredient: ItemID.DemoniteBar), new("Crimtane", ItemID.CrimtaneOre, 0x9F3543, BarIngredient: ItemID.CrimtaneBar),
        new("Meteorite", ItemID.Meteorite, 0x9B6351, BarIngredient: ItemID.MeteoriteBar), new("Hellstone", ItemID.Hellstone, 0xDF5A31, BarIngredient: ItemID.HellstoneBar),
        new("Cobalt", ItemID.CobaltOre, 0x3B79D2, BarIngredient: ItemID.CobaltBar), new("Palladium", ItemID.PalladiumOre, 0xD98A53, BarIngredient: ItemID.PalladiumBar),
        new("Mythril", ItemID.MythrilOre, 0x63C6AA, BarIngredient: ItemID.MythrilBar), new("Orichalcum", ItemID.OrichalcumOre, 0xD364B6, BarIngredient: ItemID.OrichalcumBar),
        new("Adamantite", ItemID.AdamantiteOre, 0xC14C69, BarIngredient: ItemID.AdamantiteBar), new("Titanium", ItemID.TitaniumOre, 0x788A9F, BarIngredient: ItemID.TitaniumBar),
        new("Chlorophyte", ItemID.ChlorophyteOre, 0x66B53E, BarIngredient: ItemID.ChlorophyteBar), new("Luminite", ItemID.LunarOre, 0x71D6BA, BarIngredient: ItemID.LunarBar),
        new("Amethyst", ItemID.Amethyst, 0xA05CDB, true), new("Topaz", ItemID.Topaz, 0xF0AC41, true),
        new("Sapphire", ItemID.Sapphire, 0x386DE0, true), new("Emerald", ItemID.Emerald, 0x47CE75, true),
        new("Ruby", ItemID.Ruby, 0xDE3F62, true), new("Diamond", ItemID.Diamond, 0x6DE6E9, true),
        new("Amber", ItemID.Amber, 0xDD8A32, true)
    };

    internal static void Register(Mod mod)
    {
        foreach (StorageMaterial material in All)
        {
            var tile = new StorageBlockTile(material);
            mod.AddContent(tile);
            material.TileType = tile.Type;
            var item = new StorageBlockItem(material);
            mod.AddContent(item);
            material.ItemType = item.Type;
        }
    }
}
