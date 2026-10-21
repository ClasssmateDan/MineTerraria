using System.Linq;
using DimensionalTerraria.Core;
using DimensionalTerraria.Tiles;
using DimensionalTerraria.World;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace DimensionalTerraria.Systems;

public sealed class PistonEntity : ModTileEntity
{
    private int cooldown;
    internal int Direction => Main.tile[Position.X, Position.Y].TileFrameX / 18 & 3;
    internal bool Extended => Main.tile[Position.X, Position.Y].TileFrameY == 18;
    internal bool Sticky => Main.tile[Position.X, Position.Y].TileType == ModContent.TileType<StickyPistonTile>();
    internal GridCell Cell => new(Position.X, Position.Y);
    public override bool IsTileValidForEntity(int x, int y) => WorldGen.InWorld(x, y, 10) &&
        Main.tile[x, y].HasTile && TileLoader.GetTile(Main.tile[x, y].TileType) is PistonBaseTile;
    public int AfterPlacement(int i, int j, int type, int style, int direction, int alternate)
    {
        if (Main.netMode != NetmodeID.SinglePlayer) return -1;
        if (TileEntity.ByPosition.TryGetValue(new Point16(i, j), out var existing)) return existing.ID;
        int id = Place(i, j);
        Main.tile[i, j].TileFrameX = (short)((Main.LocalPlayer.direction < 0 ? 3 : 1) * 18);
        return id;
    }
    internal bool Rotate()
    {
        if (Extended) return false;
        Main.tile[Position.X, Position.Y].TileFrameX = (short)(((Direction + 1) & 3) * 18);
        cooldown = 6; return true;
    }
    internal void Tick()
    {
        if (!IsTileValidForEntity(Position.X, Position.Y)) { Destroy(Position.X, Position.Y); return; }
        if (cooldown > 0) { cooldown--; return; }
        bool powered = RedstoneSystem.Charged(Position.X, Position.Y);
        if (powered == Extended) return;
        var world = new TerrariaPistonWorld();
        if (!PistonRules.TryPlan(world, Cell, Direction, powered, Sticky, out var plan)) return;
        if (!world.Apply(plan) && (powered || plan.Sources.Count == 0 ||
            !world.Apply(plan with { Sources = System.Array.Empty<GridCell>() }))) return;
        Main.tile[Position.X, Position.Y].TileFrameY = (short)(powered ? 18 : 0);
        cooldown = 6;
    }
    private void ClearHead(int direction)
    {
        GridCell head = Cell + PistonRules.Direction(direction);
        if (!WorldGen.InWorld(head.X, head.Y)) return;
        Tile tile = Main.tile[head.X, head.Y];
        if (tile.HasTile && tile.TileType == ModContent.TileType<PistonHeadTile>() && tile.TileFrameX / 18 == direction)
            TerrariaPistonWorld.Foreground.Empty.Write(tile);
    }
    internal static void Destroy(int x, int y, int? direction = null)
    {
        if (!TileEntity.ByPosition.TryGetValue(new Point16(x, y), out var entity) || entity is not PistonEntity piston) return;
        piston.ClearHead(direction ?? piston.Direction);
        ModContent.GetInstance<PistonEntity>().Kill(x, y);
    }
    internal static void HeadBroken(int x, int y, int direction)
    {
        GridCell origin = new GridCell(x, y) - PistonRules.Direction(direction);
        if (!WorldGen.InWorld(origin.X, origin.Y, 10) || !TileEntity.ByPosition.TryGetValue(new Point16(origin.X, origin.Y), out var entity) ||
            entity is not PistonEntity piston || piston.Direction != direction || !piston.Extended) return;
        WorldGen.KillTile(origin.X, origin.Y);
    }
}

public sealed class RedstoneSystem : ModSystem
{
    internal const int PulsePeriod = 60, PulsePoweredFrames = 30;
    // A shared world clock avoids one tile entity or map-wide scan per source.
    // All pulsers stay in phase even when pushed or loaded in another chunk.
    private static int pulseFrame = PulsePoweredFrames;
    internal static bool PulseOn => pulseFrame < PulsePoweredFrames;
    internal static bool PulserOn(Tile tile) => PulseOn ^ (tile.TileFrameX == 18);
    private static void SetPulseVisual() => Main.tileFrame[ModContent.TileType<RedstonePulserTile>()] = PulseOn ? 1 : 0;
    private static void ResetPulse() { pulseFrame = PulsePoweredFrames; SetPulseVisual(); }
    public override void OnWorldLoad() => ResetPulse();
    public override void OnWorldUnload() => ResetPulse();
    public override void Load() => On_WorldGen.KillTile += AfterTileKill;
    public override void Unload() => On_WorldGen.KillTile -= AfterTileKill;
    private static void AfterTileKill(On_WorldGen.orig_KillTile original, int x, int y, bool fail, bool effectOnly, bool noItem)
    {
        if (!WorldGen.InWorld(x, y)) { original(x, y, fail, effectOnly, noItem); return; }
        Tile tile = Main.tile[x, y];
        bool body = tile.HasTile && TileLoader.GetTile(tile.TileType) is PistonBaseTile;
        bool head = tile.HasTile && TileLoader.GetTile(tile.TileType) is PistonHeadTile;
        var saved = TerrariaPistonWorld.Foreground.Read(tile);
        original(x, y, fail, effectOnly, noItem);
        // Observe the final native result, after every protection/KillTile hook.
        // Never delete a machine because a damage attempt was vetoed.
        if ((!body && !head) || Main.tile[x, y].HasTile && Main.tile[x, y].TileType == saved.Type) return;
        int direction = saved.X / 18 & 3;
        if (body) PistonEntity.Destroy(x, y, direction);
        else
        {
            PistonEntity.HeadBroken(x, y, direction);
            GridCell origin = new GridCell(x, y) - PistonRules.Direction(direction);
            if (!Main.tile[x, y].HasTile && TileEntity.ByPosition.TryGetValue(new Point16(origin.X, origin.Y), out var entity) &&
                entity is PistonEntity piston && piston.Extended && piston.Direction == direction)
                saved.Write(Main.tile[x, y]); // A protected base could not break; restore its head.
        }
    }
    internal static bool Charged(int x, int y)
    {
        for (int d = 0; d < 4; d++)
        {
            GridCell p = new GridCell(x, y) + PistonRules.Direction(d);
            if (!WorldGen.InWorld(p.X, p.Y)) continue;
            Tile tile = Main.tile[p.X, p.Y];
            if (tile.HasUnactuatedTile && (tile.TileType == ModContent.TileType<RedstoneBlockTile>() ||
                tile.TileType == ModContent.TileType<RedstonePulserTile>() && PulserOn(tile))) return true;
        }
        return false;
    }
    public override void PostUpdateWorld()
    {
        if (Main.netMode != NetmodeID.SinglePlayer || Main.gameMenu || Main.gamePaused) return;
        pulseFrame = (pulseFrame + 1) % PulsePeriod;
        SetPulseVisual();
        // Mutate only after native tile-entity iteration has finished. Snapshot
        // because a push may relocate another piston or detach a boat overlay.
        foreach (PistonEntity piston in TileEntity.ByID.Values.OfType<PistonEntity>().OrderBy(p => p.Position.X).ThenBy(p => p.Position.Y).ToArray())
            piston.Tick();
    }
}
