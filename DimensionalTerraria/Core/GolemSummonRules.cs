using System.Collections.Generic;
using System.Numerics;

namespace DimensionalTerraria.Core;

public enum GolemStructureCell { Other, IronBlock, Pumpkin }
public enum GolemSummonResult { NotPattern, NoSpace, Protected, NoNpcSlot, Summoned }
public readonly record struct StructureTile(int X, int Y);

public interface IGolemStructureWorld : ICollisionWorld
{
    GolemStructureCell StructureAt(int x, int y);
    bool CanConsume(int x, int y);
    bool TrySpawn(Vector2 center);
    // Clear the validated foreground cells together, without drops or intervening tile updates.
    void Consume(IReadOnlyList<StructureTile> cells);
}

public static class GolemSummonRules
{
    // The newly placed pumpkin is the head; four full iron blocks form the T below it.
    public static GolemSummonResult TrySummon(IGolemStructureWorld world, int x, int y)
    {
        if (x < 12 || y < 12 || x >= world.Width - 12 || y >= world.Height - 14)
            return GolemSummonResult.NotPattern;
        StructureTile[] cells = { new(x, y), new(x - 1, y + 1), new(x, y + 1), new(x + 1, y + 1), new(x, y + 2) };
        if (world.StructureAt(x, y) != GolemStructureCell.Pumpkin) return GolemSummonResult.NotPattern;
        for (int i = 1; i < cells.Length; i++)
            if (world.StructureAt(cells[i].X, cells[i].Y) != GolemStructureCell.IronBlock)
                return GolemSummonResult.NotPattern;

        Vector2 size = new(IronGolemBrain.Width / 16f, IronGolemBrain.Height / 16f);
        Vector2 center = new(x + 0.5f, y + 3 - size.Y / 2);
        if (SpatialRules.ValidateReturn(new ConsumedWorld(world, cells), center, size) != ReturnFailure.None)
            return GolemSummonResult.NoSpace;
        foreach (StructureTile cell in cells)
            if (!world.CanConsume(cell.X, cell.Y)) return GolemSummonResult.Protected;

        // Reserve a real NPC before spending any materials. A full NPC array preserves the whole T.
        if (!world.TrySpawn(center)) return GolemSummonResult.NoNpcSlot;
        world.Consume(cells);
        return GolemSummonResult.Summoned;
    }

    private sealed class ConsumedWorld(ICollisionWorld world, IReadOnlyList<StructureTile> cells) : ICollisionWorld
    {
        public int Width => world.Width;
        public int Height => world.Height;
        public WorldCell GetCell(int x, int y)
        {
            WorldCell cell = world.GetCell(x, y);
            foreach (StructureTile consumed in cells)
                if (consumed.X == x && consumed.Y == y) return cell with { Shape = BlockShape.Empty };
            return cell;
        }
    }
}
