using System.Collections.Generic;
using DimensionalTerraria.Core;
using DimensionalTerraria.World;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace DimensionalTerraria.Systems;

[Autoload(Side = ModSide.Client)]
public sealed class GolemSummoningSystem : ModSystem
{
    private readonly HashSet<Point> pending = new();
    internal void PumpkinPlaced(int x, int y) => pending.Add(new(x, y));
    public override void OnWorldLoad() => pending.Clear();
    public override void OnWorldUnload() => pending.Clear();
    public override void Unload() => pending.Clear();

    public override void PostUpdateWorld()
    {
        if (Main.netMode != NetmodeID.SinglePlayer || Main.gameMenu) { pending.Clear(); return; }
        if (pending.Count == 0) return;
        // Wait until placement and the native item consumption have completed.
        Point[] placed = new Point[pending.Count];
        pending.CopyTo(placed); pending.Clear();
        int iron = Mod.Find<ModTile>("IronStorageTile").Type;
        foreach (Point head in placed)
        {
            GolemSummonResult result = GolemSummonRules.TrySummon(new TerrariaGolemStructureWorld(iron, head.X, head.Y), head.X, head.Y);
            if (result == GolemSummonResult.NotPattern) continue;
            string key = result.ToString();
            Main.NewText(Language.GetTextValue("Mods.DimensionalTerraria.GolemSummoning." + key),
                result == GolemSummonResult.Summoned ? Color.LightGreen : Color.Orange);
        }
    }
}

public sealed class GolemPumpkinPlacement : GlobalTile
{
    public override void PlaceInWorld(int i, int j, int type, Item item)
    {
        if (Main.netMode == NetmodeID.SinglePlayer && type == TileID.PumpkinBlock && item.type == ItemID.Pumpkin)
            ModContent.GetInstance<GolemSummoningSystem>().PumpkinPlaced(i, j);
    }
}
