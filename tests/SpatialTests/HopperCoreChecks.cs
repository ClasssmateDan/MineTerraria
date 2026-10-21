using System.Numerics;
using DimensionalTerraria.Core;

internal static class HopperCoreChecks
{
    internal static void Run(Action<string, Action> run)
    {
        GridCell hopper = new(10, 20);
        void Check(bool value) { if (!value) throw new InvalidOperationException("Hopper collection boundary failed"); }
        run("Hopper accepts items on its own top and through the above-block upper surface", () => {
            Check(HopperRules.CanCollect(hopper, new(10.5f, 19.5f), new(1, 1)));
            Check(HopperRules.CanCollect(hopper, new(10.5f, 18.5f), new(1, 1)));
            Check(HopperRules.CanCollect(hopper, new(10.5f, 19), new(.5f, .5f)));
        });
        run("Hopper measures item feet so tall sprites on the upper surface still collect", () => {
            Check(HopperRules.CanCollect(hopper, new(10.5f, 17), new(.75f, 4)));
            Check(!HopperRules.CanCollect(hopper, new(10.5f, 18.45f), Vector2.One));
            Check(!HopperRules.CanCollect(hopper, new(10.5f, 19.6f), Vector2.One));
        });
        run("Hopper does not extend pickup to either neighboring column", () => {
            Check(HopperRules.CanCollect(hopper, new(10, 19), Vector2.One));
            Check(!HopperRules.CanCollect(hopper, new(9.99f, 19), new(2, 1)));
            Check(!HopperRules.CanCollect(hopper, new(11, 19), new(2, 1)));
        });
        run("Hopper rejects invalid body sizes and nonfinite item positions", () => {
            Check(!HopperRules.CanCollect(hopper, new(float.NaN, 19), Vector2.One));
            Check(!HopperRules.CanCollect(hopper, new(10.5f, float.PositiveInfinity), Vector2.One));
            Check(!HopperRules.CanCollect(hopper, new(10.5f, 19), new(0, 1)));
            Check(!HopperRules.CanCollect(hopper, new(10.5f, 19), new(1, float.NaN)));
        });
    }
}
