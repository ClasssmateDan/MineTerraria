using System.Linq;
using System.Collections.Generic;
using DimensionalTerraria.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.GameContent;
using Microsoft.Xna.Framework.Graphics;
using DimensionalTerraria.Tiles;

namespace DimensionalTerraria.Systems;

[Autoload(Side = ModSide.Client)]
public sealed class BoatCaptureSystem : ModSystem
{
    public override void Load() => On_Main.DrawNPCs += BeforeNPCs;
    private static readonly Dictionary<Point16, OakBoatEntity> overlays = new();
    internal static IEnumerable<OakBoatEntity> Overlays => overlays.Values;
    internal static OakBoatEntity[] Boats() => TileEntity.ByID.Values.OfType<OakBoatEntity>().Concat(overlays.Values).OrderBy(b => b.Position.X).ThenBy(b => b.Position.Y).ToArray();
    internal static OakBoatEntity[] RenderBoats() => Boats().Where(b => b.Live).ToArray();
    internal static bool OverlayLive(OakBoatEntity boat) => overlays.TryGetValue(boat.Position, out var value) && ReferenceEquals(value, boat);
    internal static void AddOverlay(OakBoatEntity boat) => overlays[boat.Position] = boat;
    internal static void RemoveOverlay(OakBoatEntity boat) { if (OverlayLive(boat)) overlays.Remove(boat.Position); }
    internal static OakBoatEntity? OverlayAt(int x, int y)
    {
        foreach (OakBoatEntity boat in overlays.Values)
            if (x >= boat.Position.X && x < boat.Position.X + 3 && y >= boat.Position.Y && y < boat.Position.Y + 2) return boat;
        return null;
    }
    internal static OakBoatEntity? FindAt(int x, int y)
    {
        OakBoatEntity? overlay = OverlayAt(x, y);
        if (overlay is not null) return overlay;
        if (!WorldGen.InWorld(x, y)) return null;
        Tile tile = Main.tile[x, y];
        if (!tile.HasTile || tile.TileType != ModContent.TileType<OakBoatTile>()) return null;
        Point16 origin = new(x - tile.TileFrameX / 18, y - tile.TileFrameY / 18);
        return TileEntity.ByPosition.TryGetValue(origin, out var entity) && entity is OakBoatEntity boat && boat.Live ? boat : null;
    }
    public override void SaveWorldData(TagCompound tag) => tag["OverlayBoats"] = overlays.Values.Select(b => new TagCompound {
        ["X"] = (int)b.Position.X, ["Y"] = (int)b.Position.Y, ["Surface"] = b.OverlaySurface }).ToList();
    public override void LoadWorldData(TagCompound tag)
    {
        overlays.Clear();
        foreach (TagCompound data in tag.GetList<TagCompound>("OverlayBoats"))
        {
            int x = data.GetInt("X"), y = data.GetInt("Y");
            float surface = data.GetFloat("Surface");
            if (!WorldGen.InWorld(x, y, 10) || !WorldGen.InWorld(x + 2, y + 1, 10) || !float.IsFinite(surface) || surface < y + 2 || surface > y + 3) continue;
            var boat = (OakBoatEntity)ModTileEntity.ConstructFromBase(ModContent.GetInstance<OakBoatEntity>());
            boat.Position = new(x, y); boat.Overlay = true; boat.OverlaySurface = surface;
            AddOverlay(boat);
        }
    }
    private static void BeforeNPCs(On_Main.orig_DrawNPCs original, Main self, bool behindTiles)
    {
        DrawBoats(behindTiles);
        original(self, behindTiles);
    }
    internal static bool BehindTiles(OakBoatEntity boat) => boat.Occupant is { active: true, behindTiles: true };
    private static void DrawBoats(bool behindTiles)
    {
        if (Main.dedServ) return;
        OakBoatEntity[] boats = RenderBoats();
        if (boats.Length == 0) return;
        Texture2D texture = TextureAssets.Tile[ModContent.TileType<OakBoatTile>()].Value;
        // DrawNPCs is called inside the native world SpriteBatch. Submit the
        // hull before its passenger's pass, without ending/restarting that batch.
        // PostDrawTiles is after NPCs and would paint the hull over the rider.
        // Physical and overlapping boats share one live draw path. Returning
        // to tiles must not wait for the native cached tile target to refresh.
        foreach (OakBoatEntity boat in boats)
        {
            if (BehindTiles(boat) != behindTiles) continue;
            float top = boat.DrawSurface - 2;
            for (int dx = 0; dx < 3; dx++) for (int dy = 0; dy < 2; dy++)
            {
                Vector2 at = new Vector2((boat.Position.X + dx) * 16, (top + dy) * 16) - Main.screenPosition;
                Main.spriteBatch.Draw(texture, at, new Rectangle(dx * 18, dy * 18, 16, 16), Lighting.GetColor(boat.Position.X + dx, boat.Position.Y + dy));
            }
        }
    }
    public override void PreUpdateNPCs() => Capture();
    public override void PostUpdateNPCs()
    {
        for (int i = 0; i < Main.maxNPCs; i++)
            if (Main.npc[i].active && Main.npc[i].GetGlobalNPC<BoatCaptive>() is { Host: null, Cooldown: > 0 } state) state.Cooldown--;
        Capture();
    }
    private static void Capture()
    {
        if (Main.netMode != NetmodeID.SinglePlayer) return;
        OakBoatEntity[] boats = Boats();
        foreach (OakBoatEntity boat in boats) boat.Reconcile();
        // NPCs load before tile entities and overlay boats. Restore their exact
        // seats only once all boats exist, before free creatures can board.
        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC npc = Main.npc[i];
            if (!npc.active || !npc.TryGetGlobalNPC<BoatCaptive>(out var state) || state.PendingBoat is not { } position) continue;
            OakBoatEntity? savedBoat = boats.FirstOrDefault(b => b.Position == position);
            if (savedBoat is not null && savedBoat.TryRestore(npc)) continue;
            // Missing/blocked/occupied boats leave one living free NPC, never a
            // duplicate or a deferred summon when a boat is placed here later.
            state.PendingBoat = null;
            state.Cooldown = 60;
        }
        foreach (OakBoatEntity boat in boats)
        {
            if (!boat.Ready || !boat.Live || boat.Occupant is not null) continue;
            for (int i = 0; i < Main.maxNPCs; i++) if (boat.TryCapture(Main.npc[i])) break;
        }
    }
    internal static bool HoldAI(NPC npc)
    {
        BoatCaptive state = npc.GetGlobalNPC<BoatCaptive>();
        if (state.Host is not { } boat) return false;
        if (!boat.Live || !ReferenceEquals(boat.Occupant, npc)) { state.Host = null; return false; }
        boat.Pin();
        return state.Host is not null;
    }
    public override void PostUpdateProjectiles()
    {
        if (Main.netMode != NetmodeID.SinglePlayer) return;
        OakBoatEntity[] boats = Boats();
        if (boats.Length == 0) return;
        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            Projectile projectile = Main.projectile[i];
            if (!projectile.active || projectile.damage <= 0 || !projectile.friendly && !projectile.hostile) continue;
            foreach (OakBoatEntity boat in boats)
            {
                if (!boat.Ready || !boat.Live) continue;
                Rectangle hull = boat.Hull;
                if (BoatRules.SweptHit(new(projectile.Center.X - projectile.velocity.X, projectile.Center.Y - projectile.velocity.Y),
                    new(projectile.Center.X, projectile.Center.Y), new(projectile.width / 2f, projectile.height / 2f),
                    new(hull.Left, hull.Top), new(hull.Right, hull.Bottom))) boat.Break();
            }
        }
    }
    public override void OnWorldUnload() { foreach (OakBoatEntity boat in Boats()) boat.Release(); overlays.Clear(); }
    public override void Unload()
    {
        On_Main.DrawNPCs -= BeforeNPCs;
        OnWorldUnload();
    }
}

public sealed class OverlayBoatMelee : GlobalItem
{
    public override void MeleeEffects(Item item, Player player, Rectangle hitbox)
    {
        if (Main.netMode != NetmodeID.SinglePlayer || player.ghost || item.damage <= 0 && item.pick <= 0 && item.axe <= 0 && item.hammer <= 0) return;
        foreach (OakBoatEntity boat in BoatCaptureSystem.Overlays.ToArray()) if (boat.Live && boat.Hull.Intersects(hitbox)) boat.Break();
    }
}
