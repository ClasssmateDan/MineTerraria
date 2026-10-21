using System.Collections.Generic;
using System.Linq;
using DimensionalTerraria.Core;
using DimensionalTerraria.Systems;
using DimensionalTerraria.Tiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace DimensionalTerraria.World;

internal sealed class TerrariaPistonWorld : IPistonWorld
{
    public int Width => Main.maxTilesX;
    public int Height => Main.maxTilesY;
    public PistonCell At(GridCell p)
    {
        if (!PistonRules.Inside(this, p)) return PistonCell.Obstacle;
        Tile tile = Main.tile[p.X, p.Y];
        if (tile.HasTile && tile.TileType == ModContent.TileType<OakBoatTile>())
            return BoatCaptureSystem.FindAt(p.X, p.Y) is null ? PistonCell.Obstacle : PistonCell.Empty;
        if (TileEntity.ByPosition.TryGetValue(new Point16(p.X, p.Y), out var entity))
            return entity is PistonEntity piston && piston.IsTileValidForEntity(p.X, p.Y) && !piston.Extended ||
                entity is HopperEntity hopper && hopper.IsTileValidForEntity(p.X, p.Y) && WorldGen.CanKillTile(p.X, p.Y) ? PistonCell.Movable : PistonCell.Obstacle;
        if (!tile.HasTile) return PistonCell.Empty;
        // Frame-important includes platforms, torches and switches, not just
        // multi-tile furniture. Query this style/alternate's real footprint.
        // Inventory and tile-entity contents are not part of a foreground copy.
        if (Main.tileContainer[tile.TileType] || TileID.Sets.BasicChest[tile.TileType] || TileID.Sets.BasicDresser[tile.TileType])
            return PistonCell.Obstacle;
        if (Main.tileFrameImportant[tile.TileType])
        {
            TileObjectData? data = TileObjectData.GetTileData(tile);
            if (data is not null ? data.Width != 1 || data.Height != 1 :
                !TileID.Sets.Platforms[tile.TileType] && tile.TileType != TileID.PlanterBox)
                return PistonCell.Obstacle;
        }
        return WorldGen.CanKillTile(p.X, p.Y) ? PistonCell.Movable : PistonCell.Obstacle;
    }

    internal readonly record struct Foreground(bool Has, ushort Type, short X, short Y, bool Half, SlopeType Slope,
        byte Color, bool Invisible, bool Fullbright, bool Actuated, bool Actuator)
    {
        internal static Foreground Empty => default;
        internal static Foreground Read(Tile t) => new(t.HasTile, t.TileType, t.TileFrameX, t.TileFrameY, t.IsHalfBlock,
            t.Slope, t.TileColor, t.IsTileInvisible, t.IsTileFullbright, t.IsActuated, t.HasActuator);
        internal void Write(Tile t)
        {
            t.ClearTile(); t.HasTile = Has; t.TileType = Type; t.TileFrameX = X; t.TileFrameY = Y;
            t.IsHalfBlock = Half; t.Slope = Slope; t.TileColor = Color;
            t.IsTileInvisible = Invisible; t.IsTileFullbright = Fullbright; t.IsActuated = Actuated; t.HasActuator = Actuator;
        }
        internal BlockShape Shape => !Has || Actuated ? BlockShape.Empty : Main.tileSolidTop[Type] ? BlockShape.Platform :
            !Main.tileSolid[Type] ? BlockShape.Empty : Half ? BlockShape.HalfBlock : BlockShape.Solid;
    }

    internal bool Apply(PistonPlan plan)
    {
        if (!plan.Extend)
        {
            Tile head = Main.tile[plan.Head.X, plan.Head.Y];
            int direction = Enumerable.Range(0, 4).Single(d => PistonRules.Direction(d) == plan.Direction);
            if (!head.HasTile || head.TileType != ModContent.TileType<PistonHeadTile>() || head.TileFrameX / 18 != direction)
                return true; // A replaced/missing head cannot authorize deleting its replacement.
        }
        Dictionary<GridCell, Foreground> changes = new() { [plan.Head] = Foreground.Empty };
        List<(TileEntity Entity, GridCell To)> entities = new();
        var snapshots = plan.Sources.Select(p => (From: p, Data: Foreground.Read(Main.tile[p.X, p.Y]))).ToArray();
        foreach (var entry in snapshots)
        {
            if (At(entry.From) != PistonCell.Movable) return false;
            changes[entry.From] = Foreground.Empty;
            if (TileEntity.ByPosition.TryGetValue(new Point16(entry.From.X, entry.From.Y), out var entity)) entities.Add((entity, entry.From + plan.Delta));
        }
        foreach (var entry in snapshots) changes[entry.From + plan.Delta] = entry.Data;
        foreach (GridCell p in changes.Keys)
            if (!plan.Sources.Contains(p) && !(p == plan.Head && !plan.Extend) && At(p) != PistonCell.Empty) return false;
        if (plan.Extend)
        {
            int direction = Enumerable.Range(0, 4).Single(d => PistonRules.Direction(d) == plan.Direction);
            bool sticky = Main.tile[plan.Base.X, plan.Base.Y].TileType == ModContent.TileType<StickyPistonTile>();
            changes[plan.Head] = new(true, (ushort)ModContent.TileType<PistonHeadTile>(), (short)(direction * 18),
                (short)(sticky ? 18 : 0), false, SlopeType.Solid, 0, false, false, false, false);
        }
        // Check the entire resulting map before moving tiles or entities.
        var projected = new ProjectedWorld(changes);
        List<(Entity Entity, Vector2 Position)> actors = new();
        foreach (Entity actor in Main.player.Where(p => p is { active: true, dead: false, ghost: false }).Cast<Entity>()
            .Concat(Main.npc.Where(n => n is { active: true, noTileCollide: false } &&
                (!n.TryGetGlobalNPC<BoatCaptive>(out var captive) || captive.Host is null))))
        {
            if (!changes.Any(pair => pair.Value.Shape != BlockShape.Empty && actor.Hitbox.Intersects(new Rectangle(pair.Key.X * 16, pair.Key.Y * 16, 16, 16)))) continue;
            Vector2 position = actor.position + new Vector2(plan.Delta.X, plan.Delta.Y) * 16;
            if (SpatialRules.ValidateReturn(projected, new((position.X + actor.width / 2f) / 16, (position.Y + actor.height / 2f) / 16),
                new(actor.width / 16f, actor.height / 16f)) != ReturnFailure.None) return false;
            actors.Add((actor, position));
        }
        foreach (GridCell p in changes.Keys)
            if (!PistonRules.Inside(this, p)) return false;
        foreach (OakBoatEntity boat in changes.Keys.Select(p => BoatCaptureSystem.FindAt(p.X, p.Y)).OfType<OakBoatEntity>().Distinct().ToArray())
            boat.BecomeOverlay();
        foreach (var entity in entities) TileEntity.ByPosition.Remove(entity.Entity.Position);
        foreach (var pair in changes) pair.Value.Write(Main.tile[pair.Key.X, pair.Key.Y]);
        foreach (var entity in entities)
        {
            entity.Entity.Position = new Point16(entity.To.X, entity.To.Y);
            TileEntity.ByPosition[entity.Entity.Position] = entity.Entity;
        }
        foreach (var actor in actors) actor.Entity.position = actor.Position;
        foreach (GridCell p in changes.Keys) WorldGen.SquareTileFrame(p.X, p.Y);
        return true;
    }
    private sealed class ProjectedWorld(Dictionary<GridCell, Foreground> changes) : ICollisionWorld
    {
        public int Width => Main.maxTilesX;
        public int Height => Main.maxTilesY;
        public WorldCell GetCell(int x, int y) => new(changes.TryGetValue(new(x, y), out var block) ? block.Shape : TerrariaCollisionWorld.GetShape(Main.tile[x, y]));
    }
}
