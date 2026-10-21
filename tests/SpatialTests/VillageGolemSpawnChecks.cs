using System.Numerics;
using DimensionalTerraria.Core;

internal static class VillageGolemSpawnChecks
{
    internal static void Run(Action<string, Action> run)
    {
        void Check(bool value) { if (!value) throw new InvalidOperationException("Village golem footing/search check failed"); }
        run("Village golem requires three continuous supports instead of a one- or two-tile ledge", () => {
            FloorWorld world = new(); world.Floor(30, 50);
            Check(VillageGolemSpawnRules.Position(world, 30, 50, out var at) && at.X == 30.5f && at.Y + 76f / 32 == 50);
            world.Cells.Remove((29, 50)); Check(!VillageGolemSpawnRules.Position(world, 30, 50, out _));
            world.Cells.Remove((31, 50)); Check(!VillageGolemSpawnRules.Position(world, 30, 50, out _));
            world.Cells[(29, 50)] = new(BlockShape.Solid); world.Cells[(31, 50)] = new(BlockShape.Solid);
            world.Cells.Remove((30, 50)); Check(!VillageGolemSpawnRules.Position(world, 30, 50, out _));
        });
        run("Village support surfaces must align; level platforms and half blocks work, slopes and steps do not", () => {
            FloorWorld world = new(); world.Floor(30, 50, BlockShape.HalfBlock);
            Check(VillageGolemSpawnRules.Position(world, 30, 50, out var at) && at.Y + 76f / 32 == 50.5f);
            world.Cells[(29, 50)] = new(BlockShape.Solid); Check(!VillageGolemSpawnRules.Position(world, 30, 50, out _));
            world.Floor(30, 50, BlockShape.Platform); Check(VillageGolemSpawnRules.Position(world, 30, 50, out _));
            world.Cells[(29, 50)] = new(BlockShape.Solid); Check(VillageGolemSpawnRules.Position(world, 30, 50, out _));
            world.Cells[(31, 50)] = new(BlockShape.Solid, Sloped: true); Check(!VillageGolemSpawnRules.Position(world, 30, 50, out _));
        });
        run("Village guards allow half/full tile liquid but reject any second-layer liquid across their full width", () => {
            foreach (byte amount in new byte[] { 1, 128, 255 }) {
                FloorWorld world = new(); world.Floor(30, 50);
                for (int x = 29; x <= 31; x++) world.Wet(x, 49, amount);
                Check(VillageGolemSpawnRules.Position(world, 30, 50, out _));
                foreach (int x in new[] { 29, 30, 31 }) {
                    world.Wet(x, 48, 1); Check(!VillageGolemSpawnRules.Position(world, 30, 50, out _));
                    world.Wet(x, 48, 0);
                }
            }
        });
        run("Half-block liquid depth is measured from actual feet with half-byte rounding", () => {
            FloorWorld world = new(); world.Floor(30, 50, BlockShape.HalfBlock);
            for (int x = 29; x <= 31; x++) { world.Wet(x, 50, 255); world.Wet(x, 49, 128); }
            Check(VillageGolemSpawnRules.Position(world, 30, 50, out _));
            world.Wet(31, 49, 129); Check(!VillageGolemSpawnRules.Position(world, 30, 50, out _));
        });
        run("Shallow liquid does not bypass golem body geometry, support or world bounds", () => {
            FloorWorld world = new(); world.Floor(30, 50); world.Wet(30, 49, 128);
            world.Cells[(31, 45)] = new(BlockShape.Solid);
            Check(!VillageGolemSpawnRules.Position(world, 30, 50, out _));
            world.Cells.Remove((31, 45)); Check(VillageGolemSpawnRules.Position(world, 30, 50, out _));
            Check(!VillageGolemSpawnRules.Position(world, 1, 50, out _));
            FloorWorld water = new(); water.Wet(30, 50, 255); Check(!VillageGolemSpawnRules.Position(water, 30, 50, out _));
            // Other spawning and returning paths still require dry bodies.
            Check(!GroundSpawnRules.IronGolemPosition(world, 30, 50, out _));
            Check(SpatialRules.ValidateReturn(world, new(30.5f, 47.625f), new(2.75f, 4.75f)) == ReturnFailure.Liquid);
        });
        run("Village search prefers center, then nearest columns and left before right on ties", () => {
            FloorWorld world = new(); world.Floor(30, 50); world.Floor(25, 50); world.Floor(35, 50);
            Check(VillageGolemSpawnRules.TryFind(world, 30, 48, out var at) && at.X == 30.5f);
            for (int x = 29; x <= 31; x++) world.Cells.Remove((x, 50));
            Check(VillageGolemSpawnRules.TryFind(world, 30, 48, out at) && at.X == 25.5f);
            world.Floor(32, 50); Check(VillageGolemSpawnRules.TryFind(world, 30, 48, out at) && at.X == 32.5f);
        });
        run("Village search exhausts both eight-tile edges, keeps vertical bounds and bottom-up priority", () => {
            foreach (int dx in new[] { -8, 8 }) {
                FloorWorld world = new(); world.Floor(30 + dx, 54);
                Check(VillageGolemSpawnRules.TryFind(world, 30, 48, out var at) && at.X == 30 + dx + .5f);
            }
            FloorWorld outside = new(); outside.Floor(39, 50);
            Check(!VillageGolemSpawnRules.TryFind(outside, 30, 48, out _));
            outside = new(); outside.Floor(30, 55); Check(!VillageGolemSpawnRules.TryFind(outside, 30, 48, out _));
            FloorWorld layers = new(); layers.Floor(30, 46); layers.Floor(30, 52);
            Check(VillageGolemSpawnRules.TryFind(layers, 30, 48, out var lower) && lower.Y + 76f / 32 == 52);
        });
    }

    private sealed class FloorWorld : IGolemSpawnWorld
    {
        public int Width => 100;
        public int Height => 100;
        internal readonly Dictionary<(int, int), GolemSpawnCell> Cells = new();
        internal void Floor(int center, int row, BlockShape shape = BlockShape.Solid)
        { for (int x = center - 1; x <= center + 1; x++) Cells[(x, row)] = new(shape); }
        internal void Wet(int x, int y, byte amount) => Cells[(x, y)] = GolemCellAt(x, y) with { LiquidAmount = amount };
        public GolemSpawnCell GolemCellAt(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) throw new IndexOutOfRangeException("Spawn read outside the world");
            return Cells.GetValueOrDefault((x, y));
        }
        public WorldCell GetCell(int x, int y) { var cell = GolemCellAt(x, y); return new(cell.Shape, HasLiquid: cell.LiquidAmount > 0); }
    }
}
