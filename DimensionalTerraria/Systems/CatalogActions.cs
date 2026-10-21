using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;

namespace DimensionalTerraria.Systems;

public sealed partial class DimensionSystem
{
    internal static bool CatalogAvailable => !Main.dedServ && !Main.gameMenu && Main.netMode == NetmodeID.SinglePlayer &&
        Main.LocalPlayer.active && !Main.LocalPlayer.dead;

    internal string GiveCatalogItem(int type, int requested, bool vanilla = false)
    {
        if (!CatalogAvailable) return Language.GetTextValue("Mods.DimensionalTerraria.Catalog.InGameOnly");
        lock (targetingGate)
        {
            if (!CatalogItems.CanGrant(Mod, type, vanilla)) return Language.GetTextValue("Mods.DimensionalTerraria.Catalog.Unavailable");
            Item item = new(type);
            if (item.IsAir || item.maxStack < 1) return Language.GetTextValue("Mods.DimensionalTerraria.Catalog.Unavailable");
            int count = System.Math.Clamp(requested, 1, item.maxStack);
            item.stack = count;
            string name = item.Name;
            Item remaining = Main.LocalPlayer.GetItem(Main.myPlayer, item, GetItemSettings.InventoryUIToInventorySettingsShowAsNew);
            int added = count - (remaining.IsAir ? 0 : remaining.stack);
            return Language.GetTextValue("Mods.DimensionalTerraria.Catalog." + (added > 0 ? "Granted" : "Full"), name, added);
        }
    }
}
