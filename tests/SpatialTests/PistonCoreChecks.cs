using DimensionalTerraria.Core;

internal static class PistonCoreChecks
{
    internal static void Run(Action<string, Action> run)
    {
        void Check(bool value) { if (!value) throw new InvalidOperationException("Piston rule assertion failed"); }
        run("Pistons extend through empty space in each cardinal direction", () => {
            for (int d = 0; d < 4; d++) {
                Check(PistonRules.TryPlan(new World(), new(40, 40), d, true, false, out var p));
                Check(p.Head == new GridCell(40, 40) + PistonRules.Direction(d) && p.Sources.Count == 0);
            }
        });
        run("The Minecraft twelve-block push limit works in all four directions", () => {
            for (int d = 0; d < 4; d++) {
                var world = new World(); GridCell p = new(40, 40), delta = PistonRules.Direction(d);
                for (int i = 1; i <= 12; i++) { p += delta; world.Cells[p] = PistonCell.Movable; }
                Check(PistonRules.TryPlan(world, new(40, 40), d, true, false, out var plan) && plan.Sources.Count == 12);
                world.Cells[p + delta] = PistonCell.Movable;
                Check(!PistonRules.TryPlan(world, new(40, 40), d, true, false, out _));
            }
        });
        run("A furniture obstruction rejects the complete push plan", () => {
            var world = new World(); world.Cells[new(41, 40)] = PistonCell.Movable; world.Cells[new(42, 40)] = PistonCell.Obstacle;
            Check(!PistonRules.TryPlan(world, new(40, 40), 1, true, false, out _));
            Check(world.Cells[new(41, 40)] == PistonCell.Movable && world.Cells[new(42, 40)] == PistonCell.Obstacle);
        });
        run("Ordinary pistons retract without pulling while sticky pistons pull exactly one", () => {
            var world = new World(); world.Cells[new(42, 40)] = world.Cells[new(43, 40)] = PistonCell.Movable;
            Check(PistonRules.TryPlan(world, new(40, 40), 1, false, false, out var p) && p.Sources.Count == 0);
            Check(PistonRules.TryPlan(world, new(40, 40), 1, false, true, out p) && p.Sources.SequenceEqual(new[] { new GridCell(42, 40) }) && p.Delta == new GridCell(-1, 0));
            world.Cells[new(42, 40)] = PistonCell.Obstacle;
            Check(PistonRules.TryPlan(world, new(40, 40), 1, false, true, out p) && p.Sources.Count == 0);
        });
        run("Piston plans reject world borders before indexing outside the map", () => {
            Check(!PistonRules.TryPlan(new World(), new(10, 40), 3, true, false, out _));
            Check(!PistonRules.TryPlan(new World(), new(int.MaxValue, 40), 1, true, false, out _));
            var world = new World(); for (int x = 80; x < 90; x++) world.Cells[new(x, 40)] = PistonCell.Movable;
            Check(!PistonRules.TryPlan(world, new(79, 40), 1, true, false, out _));
        });
        run("Piston atlases preserve cardinal rotations, native pixels and transparent head gaps", () => {
            byte[] side = new byte[1024], inner = new byte[1024], top = new byte[1024], sticky = new byte[1024];
            for (int i = 0; i < 256; i++) {
                side[i * 4] = (byte)i; side[i * 4 + 3] = inner[i * 4 + 3] = top[i * 4 + 3] = sticky[i * 4 + 3] = 255;
                top[i * 4] = 200; sticky[i * 4 + 1] = 200;
            }
            byte[] body = ContentPixelArt.PistonAtlas(side, inner, top, sticky, false, false);
            byte[] head = ContentPixelArt.PistonAtlas(side, inner, top, sticky, false, true);
            Check(body.Length == 72 * 36 * 4 && head.Length == body.Length);
            Check(body[(18 + 15 + 0 * 72) * 4] == side[0]);
            Check(head[(0 + 8 * 72) * 4 + 3] == 0 && head[(7 + 8 * 72) * 4 + 3] == 255);
            Check(head[0] == 200 && head[(0 + 18 * 72) * 4 + 1] == 200);
            for (int y = 0; y < 36; y++) for (int x = 0; x < 72; x++)
                if (x % 18 >= 16 || y % 18 >= 16) Check(body[(x + y * 72) * 4 + 3] == 0);
        });
    }
    private sealed class World : IPistonWorld
    {
        public readonly Dictionary<GridCell, PistonCell> Cells = new();
        public int Width => 100;
        public int Height => 100;
        public PistonCell At(GridCell p) => PistonRules.Inside(this, p) ? Cells.GetValueOrDefault(p) : throw new InvalidOperationException("Unsafe piston read");
    }
}
