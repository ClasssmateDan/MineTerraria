using System.Collections.Generic;
using System.Linq;
using DimensionalTerraria.Core;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DimensionalTerraria.Systems;

internal static class CatalogItems
{
    internal static bool IsVanilla(int type) => type > ItemID.None && type < ItemID.Count && !ItemID.Sets.Deprecated[type];

    internal static bool CanGrant(Mod mod, int type, bool vanilla) => vanilla ? IsVanilla(type) :
        type >= ItemID.Count && type < ItemLoader.ItemCount && ModContent.GetModItem(type)?.Mod == mod;

    internal static IEnumerable<CatalogEntry> Entries(bool vanilla) => vanilla
        ? Enumerable.Range(1, ItemID.Count - 1).Where(IsVanilla)
            .Select(type => new CatalogEntry(type, Lang.GetItemNameValue(type), ItemID.Search.GetName(type)))
        : ModContent.GetContent<ModItem>().Where(item => item.Mod.Name == nameof(DimensionalTerraria))
            .Select(item => new CatalogEntry(item.Type, item.DisplayName.Value, item.Name));
}
