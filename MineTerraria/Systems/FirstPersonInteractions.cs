using DimensionalTerraria.Core;
using DimensionalTerraria.Players;
using DimensionalTerraria.World;
using DimensionalTerraria.Rendering;
using DimensionalTerraria.Configuration;
using DimensionalTerraria.Items;
using Terraria.Audio;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using NVector2 = System.Numerics.Vector2;
using NVector3 = System.Numerics.Vector3;

namespace DimensionalTerraria.Systems;

public sealed partial class DimensionSystem
{
    private readonly MiningInput miningInput = new();
    private readonly MiningInput placementInput = new();
    private readonly MiningInput fireworkInput = new();

    private InteractionContext InteractionState => new(Flight is not null, Main.netMode == NetmodeID.SinglePlayer,
        Main.LocalPlayer.active && !Main.LocalPlayer.dead && !Main.LocalPlayer.ghost, Main.instance.IsActive,
        view.Paused || Main.gamePaused || Main.drawingPlayerChat || Main.ingameOptionsWindow || Main.InGameUI.IsVisible,
        view.InventoryOpen);

    private bool CanInteract(DimensionAction action) => InteractionState.Allows(Main.LocalPlayer.GetModPlayer<DimensionPlayer>().CanInteractIn3D(action));

    private void Update3DInteractions(MouseState mouse)
    {
        if (Flight is null) return;
        if (Main.LocalPlayer.HeldItem.ModItem is Firework firework)
        {
            UpdateFirework(mouse, firework);
            Update3DPickup();
            return;
        }
        fireworkInput.Suspend();
        if (Main.LocalPlayer.HeldItem.ModItem is OakBoat boat)
        {
            UpdateBoatPlacement(mouse, boat);
            Update3DPickup();
            return;
        }
        if (Main.LocalPlayer.HeldItem.ModItem is GroundCreatureItem egg)
        {
            UpdateSpawnEgg(mouse, egg);
            Update3DPickup();
            return;
        }
        if (NativePlacement.Supported(Main.LocalPlayer.HeldItem))
        {
            UpdateBlockPlacement(mouse);
            Update3DPickup();
            return;
        }
        placementInput.Suspend();
        view.TargetIsEgg = false;
        bool mine = CanInteract(DimensionAction.Mine);
        view.Target = mine ? TileRaycast.Cast(world, Flight.Eye, Flight.Forward, InteractionRules.MiningReach, exploration) : null;
        bool tool = view.Target is { } target && NativeInteractions.HasTool(Main.LocalPlayer, target);
        view.TargetReady = tool;
        view.TargetIsShot = false;
        bool combat = CanInteract(DimensionAction.Attack);
        WeaponMode mode = NativeCombat.Mode(Main.LocalPlayer.HeldItem);
        float stop = 0;
        TileRayHit? obstruction = null;
        CombatHit? enemy = combat && mode != WeaponMode.None ? AimEnemy(mode, out stop, out obstruction) : null;
        view.Enemy = enemy;
        PrimaryAction action = CombatRules.Choose(mine, tool, view.Target?.Distance, combat, mode != WeaponMode.None, enemy?.Distance);
        if (action == PrimaryAction.None && combat && mode == WeaponMode.None && Main.LocalPlayer.HeldItem.damage > 0)
        {
            view.CombatHint = "UnsupportedWeapon";
            view.CombatHintTime = 0.12f;
        }
        if (action == PrimaryAction.Attack)
        {
            view.Target = enemy is null && mode == WeaponMode.Ranged ? obstruction : null;
            view.TargetIsShot = view.Target is not null;
            view.TargetReady = view.TargetIsShot && view.Target?.Layer != RayLayer.Unexplored;
        }
        if (miningInput.Tick(mine || combat, mouse.LeftButton == ButtonState.Pressed, action != PrimaryAction.None))
        {
            if (action == PrimaryAction.Attack) Attack(mode, enemy, stop, obstruction);
            else if (view.Target is { } hit) lock (targetingGate)
            {
                MiningSurface surface = MiningParticleRenderer.Capture(hit);
                bool overlayBoat = hit.Layer == RayLayer.Tile && BoatCaptureSystem.OverlayAt(hit.X, hit.Y) is not null;
                int type = hit.Layer == RayLayer.Wall ? Main.tile[hit.X, hit.Y].WallType : Main.tile[hit.X, hit.Y].TileType;
                int ticks = NativeInteractions.Hit(Main.LocalPlayer, hit);
                miningInput.Hit(ticks);
                view.Swing.Start(ticks);
                Tile after = Main.tile[hit.X, hit.Y];
                bool broken = overlayBoat ? BoatCaptureSystem.OverlayAt(hit.X, hit.Y) is null :
                    hit.Layer == RayLayer.Wall ? after.WallType != type : !after.HasTile || after.TileType != type;
                renderer?.EmitMiningParticles(surface, Flight.Eye + Flight.Forward * hit.Distance, broken);
                renderer?.InvalidateTile(hit.X, hit.Y);
            }
        }
        Update3DPickup();
    }

    private void UpdateBlockPlacement(MouseState mouse)
    {
        if (Flight is null) return;
        view.Enemy = null; view.TargetIsEgg = true; view.TargetIsShot = false; view.SpawnHint = "PlaceTarget";
        bool allowed = CanInteract(DimensionAction.Place);
        PlacementPlan preview = default;
        bool ready = allowed && NativePlacement.Plan(Main.LocalPlayer, Flight.Eye, Flight.Forward, out preview, exploration, Flight);
        view.Target = ready ? new TileRayHit(preview.X, preview.Y, preview.Wall ? RayLayer.Wall : RayLayer.Tile, 0,
            new(preview.Left, preview.Top, preview.Wall ? -1.5f : -.5f),
            new(preview.Left + preview.Width, preview.Top + preview.Height, preview.Wall ? -.5f : .5f)) : null;
        view.TargetReady = ready;
        if (!placementInput.Tick(allowed, mouse.RightButton == ButtonState.Pressed, ready)) return;
        lock (targetingGate)
        {
            int ticks = System.Math.Max(6, Main.LocalPlayer.HeldItem.useTime);
            if (!NativePlacement.TryPlace(Main.LocalPlayer, Flight.Eye, Flight.Forward, out PlacementPlan placed, exploration, Flight)) return;
            for (int x = placed.Left - 1; x <= placed.Left + placed.Width; x++)
            for (int y = placed.Top - 1; y <= placed.Top + placed.Height; y++) renderer?.InvalidateTile(x, y);
            placementInput.Hit(ticks); view.Swing.Start(ticks);
            if (!Main.dedServ) SoundEngine.PlaySound(SoundID.Dig);
        }
    }

    private void UpdateBoatPlacement(MouseState mouse, OakBoat boat)
    {
        if (Flight is null) return;
        view.Enemy = null; view.TargetIsEgg = true; view.TargetIsShot = false; view.SpawnHint = "BoatTarget";
        view.Target = null; view.TargetReady = false;
        if (!CanInteract(DimensionAction.UseItem) || !BoatRules.TryAim(world, Flight.Eye, Flight.Forward,
            InteractionRules.MiningReach, out BoatPlacement placement, exploration)) return;
        view.Target = new TileRayHit(placement.X, placement.Y, RayLayer.Tile, 0,
            new(placement.X, placement.Y, 0.51f), new(placement.X + 3, placement.Y + 2, 0.61f));
        view.TargetReady = true;
        if (mouse.RightButton != ButtonState.Pressed || previousMouse.RightButton != ButtonState.Released) return;
        lock (targetingGate)
        {
            if (!boat.TryPlace(Main.LocalPlayer, placement, exploration)) return;
            Item item = Main.LocalPlayer.HeldItem;
            if (ItemLoader.ConsumeItem(item, Main.LocalPlayer) && --item.stack <= 0) item.TurnToAir();
            renderer?.InvalidateTile(placement.X, placement.Y);
            renderer?.InvalidateTile(placement.X + 2, placement.Y + 1);
            view.Swing.Start(24); SoundEngine.PlaySound(SoundID.Dig);
        }
    }

    private void UpdateFirework(MouseState mouse, Firework firework)
    {
        miningInput.Suspend(); placementInput.Suspend();
        view.Target = null; view.Enemy = null;
        view.TargetIsEgg = view.TargetIsShot = view.TargetReady = false;
        if (!fireworkInput.Tick(CanInteract(DimensionAction.UseItem), mouse.RightButton == ButtonState.Pressed, Flight is { Departing: false })) return;
        Player player = Main.LocalPlayer;
        Item item = player.HeldItem;
        if (!firework.TryLaunch(player, Flight, default)) return;
        if (ItemLoader.ConsumeItem(item, player) && --item.stack <= 0) item.TurnToAir();
        fireworkInput.Hit(20);
        view.Swing.Start(20);
    }

    private void UpdateSpawnEgg(MouseState mouse, GroundCreatureItem egg)
    {
        if (Flight is null) return;
        view.Enemy = null;
        view.TargetIsEgg = true;
        view.SpawnHint = egg is PrefabIronGolem ? "GolemTarget" : egg is SlimeSpawnEgg ? "SlimeEggTarget" :
            egg is VillagerSpawnEgg ? "VillagerEggTarget" : "EggTarget";
        view.TargetIsShot = false;
        view.Target = CanInteract(DimensionAction.UseItem) ? TileRaycast.Cast(world, Flight.Eye, Flight.Forward, InteractionRules.MiningReach, exploration) : null;
        view.TargetReady = view.Target is { Layer: RayLayer.Tile } floor && egg.SpawnPosition(world, floor.X, floor.Y, out _, exploration);
        if (!view.TargetReady || view.Target is not { } target || mouse.RightButton != ButtonState.Pressed || previousMouse.RightButton != ButtonState.Released) return;
        lock (targetingGate)
        {
            if (!egg.TrySpawn(Main.LocalPlayer, target.X, target.Y, exploration)) return;
            Item item = Main.LocalPlayer.HeldItem;
            if (ItemLoader.ConsumeItem(item, Main.LocalPlayer) && --item.stack <= 0) item.TurnToAir();
            view.Swing.Start(24);
            SoundEngine.PlaySound(SoundID.Item8);
        }
    }

    private void Update3DPickup()
    {
        if (Flight is null || !CanInteract(DimensionAction.Pickup)) return;
        float pickupRadius = ModContent.GetInstance<DimensionConfig>().PickupRadius;
        for (int i = 0; i < Main.maxItems; i++)
        {
            Item item = Main.item[i];
            if (!item.active || item.IsAir) continue;
            NVector2 center = new(item.Center.X / 16, item.Center.Y / 16);
            if (!InteractionRules.CanReachDrop(Flight.Position, center, new(item.width / 16f, item.height / 16f),
                    pickupRadius, exploration)) continue;
            NVector3 delta = new NVector3(center, InteractionRules.ItemDepth) - Flight.Eye;
            float distance = delta.Length();
            if (distance > 0.0001f && TileRaycast.Cast(world, Flight.Eye, delta, distance, exploration) is not null) continue;
            lock (targetingGate) NativeInteractions.TryPickup(Main.LocalPlayer, i);
        }
    }

    private void SuspendInteractions()
    {
        fireworkInput.Suspend();
        miningInput.Suspend();
        placementInput.Suspend();
        view.Target = null;
        view.TargetReady = false;
        view.TargetIsShot = false;
        view.TargetIsEgg = false;
        view.Swing.Reset();
        view.Enemy = null;
        view.CombatHint = null;
        view.CombatHintTime = 0;
        view.HitFlash = 0;
    }

    public override void PostUpdateEverything()
    {
        if (Flight is null || view.Paused) return;
        // Normal Player.Update remains detached. Only advance the native mana
        // resource here, so casting does not require returning to 2D to recover.
        Main.LocalPlayer.UpdateManaRegen();
        UpdateSwordEffects();
        view.Swing.Update(1f / 60);
        view.Feedback.Update(1f / 60);
        renderer?.UpdateMiningParticles(1f / 60);
        renderer?.UpdateCombatTraces(1f / 60);
        if (view.HitFlash > 0) view.HitFlash -= 1f / 60;
        if (view.CombatHintTime > 0)
        {
            view.CombatHintTime -= 1f / 60;
            if (view.CombatHintTime <= 0) view.CombatHint = null;
        }
    }
}
