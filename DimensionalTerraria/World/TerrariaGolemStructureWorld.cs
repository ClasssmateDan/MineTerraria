using System.Collections.Generic;
using DimensionalTerraria.Core;
using DimensionalTerraria.NPCs;
using DimensionalTerraria.Systems;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Vector2 = System.Numerics.Vector2;

namespace DimensionalTerraria.World;

internal sealed class TerrariaGolemStructureWorld(int ironTile, int headX, int headY) : IGolemStructureWorld
{
    private readonly TerrariaCollisionWorld collision = new();
    public int Width => Main.maxTilesX;
    public int Height => Main.maxTilesY;
    public WorldCell GetCell(int x, int y) => collision.GetCell(x, y);

    public GolemStructureCell StructureAt(int x, int y)
    {
        Tile tile = Main.tile[x, y];
        if (!tile.HasUnactuatedTile || tile.IsHalfBlock || tile.Slope != SlopeType.Solid)
            return GolemStructureCell.Other;
        if (tile.TileType == ironTile) return GolemStructureCell.IronBlock;
        return tile.TileType == TileID.PumpkinBlock ? GolemStructureCell.Pumpkin : GolemStructureCell.Other;
    }

    public bool CanConsume(int x, int y) => WorldGen.CanKillTile(x, y);

    public bool TrySpawn(Vector2 center)
    {
        int slot = NPC.NewNPC(new EntitySource_TileUpdate(headX, headY, "IronGolemStructure"),
            (int)(center.X * 16), (int)(center.Y * 16), ModContent.NPCType<MinecraftIronGolem>());
        if (slot < 0 || slot >= Main.maxNPCs) return false;
        Main.npc[slot].Center = new(center.X * 16, center.Y * 16);
        Main.npc[slot].netUpdate = true;
        return true;
    }

    public void Consume(IReadOnlyList<StructureTile> cells)
    {
        // This is a conversion, not mining. ClearTile preserves walls, liquids and wires;
        // no KillTile hooks/drops or framing can interrupt the five-cell commit.
        foreach (StructureTile cell in cells) Main.tile[cell.X, cell.Y].ClearTile();
        foreach (StructureTile cell in cells)
        {
            WorldGen.SquareTileFrame(cell.X, cell.Y);
            DimensionSystem.Instance.InvalidateStructureTile(cell.X, cell.Y);
        }
    }
}
