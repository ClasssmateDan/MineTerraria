using System.Numerics;
using DimensionalTerraria.Core;

internal static class VillagerCoreChecks
{
    internal static void Run(Action<string, Action> run)
    {
        void Check(bool value) { if (!value) throw new InvalidOperationException("Villager memory check failed"); }
        run("Villager attempts use simulation time even when the day clock is frozen", () => {
            VillagerMemory memory = new();
            for (int i = 0; i < 299; i++) Check(!memory.Tick());
            Check(memory.Tick());
            for (int i = 0; i < 299; i++) Check(!memory.Tick());
            Check(memory.Tick());
        });
        run("Villager fear expires three seconds after the last threat", () => {
            VillagerMemory memory = new(); memory.Frighten(120);
            for (int i = 0; i < 179; i++) memory.Tick();
            Check(memory.Panicking); memory.Tick(); Check(!memory.Panicking);
            memory.Frighten(80); memory.Tick(); memory.Frighten(90);
            Check(memory.PanicTicks == 180 && memory.ThreatX == 90);
        });
        run("Recent golem memory blocks participation for a full thirty seconds", () => {
            VillagerMemory memory = new(); memory.ObserveGuard();
            for (int i = 0; i < 1799; i++) memory.Tick();
            Check(!memory.Eligible); memory.Tick(); Check(memory.Eligible);
            memory.ObserveGuard(); memory.Tick(); memory.ObserveGuard(); Check(memory.GuardTicks == 1800);
        });
        run("Village grouping uses independent horizontal and vertical tile limits", () => {
            Check(VillagerMemory.Within(Vector2.Zero, new(160, -160), 10));
            Check(!VillagerMemory.Within(Vector2.Zero, new(160.01f, 0), 10));
            Check(!VillagerMemory.Within(Vector2.Zero, new(0, -160.01f), 10));
            Check(VillagerMemory.Within(Vector2.Zero, new(-256, 256), 16));
            Check(!VillagerMemory.Within(Vector2.Zero, new(float.NaN, 0), 16));
        });
        run("Villager reload preserves remaining cooldown rather than restarting it", () => {
            VillagerMemory original = new(); original.Frighten(44); original.ObserveGuard();
            for (int i = 0; i < 80; i++) original.Tick();
            VillagerMemory restored = new(); restored.Restore(original.PanicTicks, original.GuardTicks, original.AttemptTicks, original.ThreatX);
            for (int i = 0; i < 2000; i++) {
                Check(original.Tick() == restored.Tick());
                Check(original.Panicking == restored.Panicking && original.Eligible == restored.Eligible);
            }
        });
        run("Corrupt saved villager timers are bounded and invalid threats are ignored", () => {
            VillagerMemory memory = new(); memory.Restore(-1, int.MaxValue, 0, float.NaN);
            Check(!memory.Panicking && !memory.Eligible && memory.AttemptTicks == 1 && float.IsFinite(memory.ThreatX));
            memory.Frighten(float.PositiveInfinity); Check(!memory.Panicking);
        });
    }
}
