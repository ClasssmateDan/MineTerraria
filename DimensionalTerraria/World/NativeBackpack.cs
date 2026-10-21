using Terraria;
using Terraria.UI;

namespace DimensionalTerraria.World;

internal static class NativeBackpack
{
    internal static void Click(int slot, bool left, bool right)
    {
        if (slot < 0 || slot >= 50 || !left && !right) return;
        bool oldLeft = Main.mouseLeft, oldRight = Main.mouseRight;
        bool oldLeftRelease = Main.mouseLeftRelease, oldRightRelease = Main.mouseRightRelease;
        bool oldInventory = Main.playerInventory;
        int oldSplit = Main.stackSplit;
        try
        {
            Main.mouseLeft = left; Main.mouseLeftRelease = left;
            Main.mouseRight = right; Main.mouseRightRelease = right;
            Main.playerInventory = true;
            if (right) Main.stackSplit = 0;
            if (left) ItemSlot.LeftClick(Main.LocalPlayer.inventory, ItemSlot.Context.InventoryItem, slot);
            else ItemSlot.RightClick(Main.LocalPlayer.inventory, ItemSlot.Context.InventoryItem, slot);
        }
        finally
        {
            Main.mouseLeft = oldLeft; Main.mouseRight = oldRight;
            Main.mouseLeftRelease = oldLeftRelease; Main.mouseRightRelease = oldRightRelease;
            Main.playerInventory = oldInventory;
            Main.stackSplit = oldSplit;
        }
    }
    internal static bool ReturnCursorItem()
    {
        if (Main.mouseItem.IsAir) return true;
        Main.mouseItem = Main.LocalPlayer.GetItem(Main.myPlayer, Main.mouseItem, GetItemSettings.InventoryUIToInventorySettings);
        return Main.mouseItem.IsAir;
    }
}
