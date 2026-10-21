using DimensionalTerraria.Core;
using DimensionalTerraria.Tiles;
using DimensionalTerraria.World;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using DimensionalTerraria.Items;

namespace DimensionalTerraria.Systems;

public sealed class OakBoatEntity : ModTileEntity
{
    internal NPC? Occupant;
    private BoatCaptive? captive;
    private readonly TerrariaCollisionWorld world = new();
    internal Vector2 SeatBottom { get; private set; }
    internal bool Ready { get; private set; }
    internal bool Overlay;
    internal float OverlaySurface;
    internal float DrawSurface => Overlay ? OverlaySurface :
        BoatRules.Support(world, Position.X, Position.Y + 2, out float surface) ? surface : Position.Y + 2;
    internal Rectangle Hull => new(Position.X * 16, (int)SeatBottom.Y - 20, 48, 32);
    public override bool IsTileValidForEntity(int x, int y)
    {
        if (!WorldGen.InWorld(x, y, 10) || !WorldGen.InWorld(x + 2, y + 1, 10)) return false;
        if (Overlay) return BoatCaptureSystem.OverlayLive(this);
        for (int dx = 0; dx < 3; dx++) for (int dy = 0; dy < 2; dy++)
        {
            Tile tile = Main.tile[x + dx, y + dy];
            if (!tile.HasUnactuatedTile || tile.TileType != ModContent.TileType<OakBoatTile>() || tile.TileFrameX != dx * 18 || tile.TileFrameY != dy * 18) return false;
        }
        return true;
    }
    internal bool Live => Overlay ? IsTileValidForEntity(Position.X, Position.Y) :
        TileEntity.ByPosition.TryGetValue(Position, out var entity) && ReferenceEquals(entity, this) && IsTileValidForEntity(Position.X, Position.Y);
    internal void BecomeOverlay()
    {
        if (Overlay || !Live) return;
        OverlaySurface = Ready ? (SeatBottom.Y + 12) / 16 : Position.Y + 2;
        for (int dx = 0; dx < 3; dx++) for (int dy = 0; dy < 2; dy++) Main.tile[Position.X + dx, Position.Y + dy].ClearTile();
        TileEntity.ByPosition.Remove(Position); TileEntity.ByID.Remove(ID);
        Overlay = true;
        BoatCaptureSystem.AddOverlay(this);
    }
    private void RestoreIfClear()
    {
        if (!Overlay || TileEntity.ByPosition.ContainsKey(Position) || !BoatRules.Support(world, Position.X, Position.Y + 2, out _)) return;
        for (int dx = 0; dx < 3; dx++) for (int dy = 0; dy < 2; dy++) if (Main.tile[Position.X + dx, Position.Y + dy].HasTile) return;
        for (int dx = 0; dx < 3; dx++) for (int dy = 0; dy < 2; dy++)
        {
            Tile tile = Main.tile[Position.X + dx, Position.Y + dy];
            tile.ResetToType((ushort)ModContent.TileType<OakBoatTile>());
            tile.TileFrameX = (short)(dx * 18); tile.TileFrameY = (short)(dy * 18);
        }
        BoatCaptureSystem.RemoveOverlay(this);
        Overlay = false; ID = TileEntity.AssignNewID();
        TileEntity.ByID[ID] = this; TileEntity.ByPosition[Position] = this;
    }
    internal void Reconcile()
    {
        Ready = false;
        RestoreIfClear();
        float surface = OverlaySurface;
        if (!Live || !Overlay && !BoatRules.Support(world, Position.X, Position.Y + 2, out surface))
        {
            if (Live) { Release(); Break(); }
            else
            {
                Release();
                if (TileEntity.ByPosition.TryGetValue(Position, out var entry) && ReferenceEquals(entry, this)) Kill(Position.X, Position.Y);
            }
            return;
        }
        Ready = true;
        SeatBottom = new((Position.X + 1.5f) * 16, surface * 16 - 12);
        if (Occupant is { } npc && (!npc.active || npc.life <= 0 || !ReferenceEquals(Main.npc[npc.whoAmI], npc) || !ReferenceEquals(npc.GetGlobalNPC<BoatCaptive>(), captive))) Release();
        if (Occupant is not null) Pin();
    }
    internal bool TryCapture(NPC npc) => Board(npc, false);
    internal bool TryRestore(NPC npc) => Board(npc, true);
    private bool Board(NPC npc, bool restoring)
    {
        if (Occupant is not null || !Ready || !Live || !BoatCaptive.Eligible(npc) || !Fits(npc)) return false;
        BoatCaptive state = npc.GetGlobalNPC<BoatCaptive>();
        if (state.Host is not null || !restoring && (state.Cooldown > 0 || state.PendingBoat.HasValue || !Hull.Intersects(npc.Hitbox))) return false;
        Occupant = npc; captive = state; state.Host = this;
        if (!restoring) state.ReleaseVelocity = npc.velocity;
        state.PendingBoat = null; state.Cooldown = 0;
        npc.netUpdate = true; Pin();
        return true;
    }
    private bool Fits(NPC npc)
    {
        Vector2 position = SeatBottom - new Vector2(npc.width / 2f, npc.height);
        return position.X >= 10 * 16 && position.Y >= 10 * 16 &&
            position.X + npc.width <= (Main.maxTilesX - 10) * 16 && SeatBottom.Y <= (Main.maxTilesY - 10) * 16 &&
            (Overlay ? OverlayFits(position, npc.width, npc.height) : !Collision.SolidCollision(position, npc.width, npc.height));
    }
    private bool OverlayFits(Vector2 position, int width, int height)
    {
        // Blocks passing through the boat may overlap its own small footprint;
        // the rest of a passenger's full body still needs open space.
        for (int x = (int)(position.X / 16); x <= (int)((position.X + width - 0.01f) / 16); x++)
        for (int y = (int)(position.Y / 16); y <= (int)((position.Y + height - 0.01f) / 16); y++)
        {
            if (x >= Position.X && x < Position.X + 3 && y >= Position.Y && y < Position.Y + 2) continue;
            if (Collision.SolidCollision(new Vector2(x * 16, y * 16), 16, 16) &&
                new Rectangle(x * 16, y * 16, 16, 16).Intersects(new Rectangle((int)position.X, (int)position.Y, width, height))) return false;
        }
        return true;
    }
    internal void Pin()
    {
        if (Occupant is not { } npc) return;
        if (!BoatCaptive.Eligible(npc) || !Fits(npc)) { Release(); return; }
        npc.Bottom = SeatBottom; npc.velocity = Vector2.Zero; npc.oldPosition = npc.position;
        npc.target = TargetExclusionScope.NoPlayer; npc.targetRect = Rectangle.Empty;
        npc.gfxOffY = 0;
    }
    internal void Release()
    {
        if (captive is not null && ReferenceEquals(captive.Host, this))
        {
            captive.Host = null; captive.Cooldown = 60;
            if (Occupant is { active: true } npc && ReferenceEquals(npc.GetGlobalNPC<BoatCaptive>(), captive))
            { npc.velocity = Vector2.Zero; GroundBossCapture.OnReleased(npc, captive.ReleaseVelocity); npc.netUpdate = true; }
        }
        Occupant = null; captive = null;
    }
    internal void Break()
    {
        if (Overlay && !BoatCaptureSystem.OverlayLive(this)) return;
        // Clear the link before tile drops or hit callbacks can re-enter damage.
        Ready = false;
        Release();
        if (Overlay)
        {
            BoatCaptureSystem.RemoveOverlay(this);
            Item.NewItem(new EntitySource_Misc("DimensionalTerraria_OverlayBoatBreak"), new Rectangle(Position.X * 16, Position.Y * 16, 48, 32), ModContent.ItemType<OakBoat>());
            return;
        }
        WorldGen.KillTile(Position.X, Position.Y);
    }
    public override void OnKill() { Ready = false; Release(); }
}

public sealed partial class BoatCaptive : GlobalNPC
{
    public override bool InstancePerEntity => true;
    internal OakBoatEntity? Host;
    internal int Cooldown;
    internal Vector2 ReleaseVelocity;
    public override void SetDefaults(NPC npc) { Host = null; PendingBoat = null; Cooldown = 0; ReleaseVelocity = Vector2.Zero; }
    internal static bool Eligible(NPC npc)
    {
        if (npc.type <= NPCID.None || npc.type >= NPCID.Sets.ProjectileNPC.Length) return false;
        bool boss = GroundBossCapture.IsBoss(npc);
        return BoatRules.FitsPassenger(npc.active && npc.life > 0,
            boss && !GroundBossCapture.Allowed(npc) || BossPart(npc.aiStyle),
            npc.realLife >= 0 || NPCID.Sets.ProjectileNPC[npc.type] || npc.aiStyle is NPCAIStyleID.Worm or NPCAIStyleID.TheDestroyer,
            npc.dontTakeDamage || npc.immortal || npc.hide, new(npc.width, npc.height)) && !HasLinkedParts(npc);
    }
    private static bool HasLinkedParts(NPC npc)
    {
        // A head may retain realLife == -1 while its segments refer back to it.
        for (int i = 0; i < Main.maxNPCs; i++)
            if (Main.npc[i] is { active: true } part && part != npc && part.realLife == npc.whoAmI) return true;
        return false;
    }
    private static bool BossPart(int style) => style is NPCAIStyleID.SkeletronHand or
        NPCAIStyleID.PrimeSaw or NPCAIStyleID.PrimeVice or NPCAIStyleID.PrimeCannon or NPCAIStyleID.PrimeLaser or
        NPCAIStyleID.GolemHead or NPCAIStyleID.GolemFist or NPCAIStyleID.PlanteraHook or NPCAIStyleID.PlanteraTentacle or
        NPCAIStyleID.MoonLordHand or NPCAIStyleID.MoonLordHead;
    public override bool CanHitPlayer(NPC npc, Player target, ref int cooldownSlot) => Host is null;
    public override bool CanHitNPC(NPC npc, NPC target) => Host is null;
    public override bool CheckActive(NPC npc) => Host is null;
}
