using System;
using System.Collections.Generic;
using System.Diagnostics;
using DimensionalTerraria.Configuration;
using DimensionalTerraria.Core;
using DimensionalTerraria.Rendering;
using DimensionalTerraria.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;
using NVector2 = System.Numerics.Vector2;

namespace DimensionalTerraria.Systems;

[Autoload(Side = ModSide.Client)]
public sealed partial class DimensionSystem : ModSystem
{
    internal static DimensionSystem Instance => ModContent.GetInstance<DimensionSystem>();
    internal static bool IsDetached(Player player) => !Main.dedServ && Main.netMode == NetmodeID.SinglePlayer &&
        player.whoAmI == Main.myPlayer && Instance.Flight is not null;
    internal FlightState? Flight { get; private set; }
    private ExplorationMask? exploration;
    internal ReturnFailure ReturnStatus => Flight is null ? ReturnFailure.None :
        SpatialRules.ValidateReturn(world, new NVector2(Flight.Position.X, Flight.Position.Y), Flight.BodySize, exploration: exploration);
    private readonly TerrariaCollisionWorld world = new();
    private DimensionRenderer? renderer;
    internal void InvalidateStructureTile(int x, int y) => renderer?.InvalidateTile(x, y);
    private KeyboardState previousKeyboard;
    private MouseState previousMouse;
    private Vector2 departurePosition;
    private int messageCooldown;
    private bool renderFailed;
    private readonly ButtonEdge toggleEdge = new();
    private readonly ButtonEdge anchorEdge = new();
    private readonly ButtonEdge backpackEdge = new();
    private readonly FirstPersonView view = new();
    private Point savedMousePosition;

    public override void Load()
    {
        global::DimensionalTerraria.DimensionalTerraria.RegisterBindings(Mod);
        On_Main.DoUpdateInWorld += UpdateWorld;
        On_Player.Update += UpdatePlayer;
        On_Main.DrawCursor += DrawCursor;
        On_Main.DrawThickCursor += DrawThickCursor;
        LoadTargetingHooks();
    }

    public override void Unload()
    {
        On_Main.DoUpdateInWorld -= UpdateWorld;
        On_Player.Update -= UpdatePlayer;
        On_Main.DrawCursor -= DrawCursor;
        On_Main.DrawThickCursor -= DrawThickCursor;
        UnloadTargetingHooks();
        ReleaseMouse();
        ResetSwordEffects();
        Flight = null;
        exploration = null;
        Main.QueueMainThreadAction(() => { renderer?.Dispose(); renderer = null; PixelSpriteAtlas.Clear(); });
    }

    private void UpdateWorld(On_Main.orig_DoUpdateInWorld original, Main self, Stopwatch stopwatch)
    {
        if (Flight is null || Main.gameMenu || Main.netMode != NetmodeID.SinglePlayer)
        {
            original(self, stopwatch);
            return;
        }
        if (view.Paused) return;
        // Keep a ghost presence for vanilla spawn/activity ranges, without a 2D body.
        // Do not deactivate the only player: vanilla would immediately despawn nearby enemies.
        lock (targetingGate)
        {
            using (new DetachedBodyScope(new TerrariaPlayerBody(Main.LocalPlayer)))
                original(self, stopwatch);
        }
    }

    private void UpdatePlayer(On_Player.orig_Update original, Player player, int index)
    {
        if (!IsDetached(player)) original(player, index);
    }

    public override void PostUpdateInput()
    {
        KeyboardState keyboard = Keyboard.GetState();
        MouseState mouse = Mouse.GetState();
        if (messageCooldown > 0) messageCooldown--;
        bool togglePressed = toggleEdge.Sample(BindingHeld(global::DimensionalTerraria.DimensionalTerraria.ToggleDimension, keyboard, mouse));
        bool anchorPressed = anchorEdge.Sample(BindingHeld(global::DimensionalTerraria.DimensionalTerraria.ReturnToAnchor, keyboard, mouse));
        bool backpackPressed = backpackEdge.Sample(BindingHeld(global::DimensionalTerraria.DimensionalTerraria.OpenBackpack, keyboard, mouse));

        try
        {
            if (Main.gameMenu || Main.LocalPlayer.dead || Main.netMode != NetmodeID.SinglePlayer)
            {
                if (Flight is not null) ReturnToDeparture(forceExit: true);
                if (!Main.gameMenu && togglePressed)
                    Notify("SinglePlayerOnly");
                return;
            }

            bool typing = Main.drawingPlayerChat || Main.editSign || Main.editChest;
            bool focused = Main.instance.IsActive;
            if (Flight is null)
            {
                if (focused && !typing && togglePressed)
                    RequestToggle("keybind");
                return;
            }

            UpdateFirstPersonInput(keyboard, mouse, togglePressed, anchorPressed, backpackPressed, typing, focused);
        }
        finally
        {
            previousKeyboard = keyboard;
            previousMouse = mouse;
        }
    }

    private static bool BindingHeld(ModKeybind binding, KeyboardState keyboard, MouseState mouse)
    {
        if (binding is null) return false;
        if (global::DimensionalTerraria.DimensionalTerraria.BindingCurrent(binding)) return true;
        foreach (string name in global::DimensionalTerraria.DimensionalTerraria.AssignedKeys(binding))
        {
            if (Enum.TryParse(name, true, out Keys key) && keyboard.IsKeyDown(key)) return true;
            if (name == "MouseLeft" && mouse.LeftButton == ButtonState.Pressed ||
                name == "MouseRight" && mouse.RightButton == ButtonState.Pressed ||
                name == "MouseMiddle" && mouse.MiddleButton == ButtonState.Pressed ||
                name == "MouseXButton1" && mouse.XButton1 == ButtonState.Pressed ||
                name == "MouseXButton2" && mouse.XButton2 == ButtonState.Pressed) return true;
        }
        return false;
    }

    internal void RequestToggle(string source)
    {
        if (Main.gameMenu || Main.LocalPlayer.dead) return;
        if (Main.netMode != NetmodeID.SinglePlayer) { Notify("SinglePlayerOnly"); return; }
        if (Flight is not null)
        {
            if (ReturnStatus == ReturnFailure.None) ReturnAt(Flight.Position.X, Flight.Position.Y);
            else Notify("Return" + ReturnStatus);
            return;
        }
        if (!ModContent.GetInstance<DimensionConfig>().EnableSwitching) { Notify("Disabled"); return; }
        if (Main.ingameOptionsWindow || Main.InGameUI.IsVisible) { Notify("CloseMenus"); return; }
        Main.playerInventory = false;
        Main.mapFullscreen = false;
        EnterFlight();
    }

    internal void RecoverToDeparture()
    {
        if (Flight is not null) ReturnToDeparture();
    }

    private void EnterFlight()
    {
        Player player = Main.LocalPlayer;
        if (player.mount.Active || player.pulley || player.grapCount > 0 || player.ghost || player.sleeping.isSleeping)
        {
            Notify("CannotEnter");
            return;
        }
        NVector2 center = new(player.Center.X / 16, player.Center.Y / 16);
        NVector2 size = new(player.width / 16f, player.height / 16f);
        if (SpatialRules.ValidateReturn(world, center, size) != ReturnFailure.None)
        {
            Notify("CannotEnter");
            return;
        }
        departurePosition = player.position;
        savedMousePosition = new Point(previousMouse.X, previousMouse.Y);
        player.channel = false;
        player.itemAnimation = player.itemTime = 0;
        player.heldProj = -1;
        view.Reset();
        ResetSwordEffects();
        SuspendInteractions();
        DimensionConfig config = ModContent.GetInstance<DimensionConfig>();
        exploration = config.EnableExplorationFog
            ? new ExplorationMask(Main.maxTilesX, Main.maxTilesY, (x, y) =>
                Main.Map is not null && x < Main.Map.MaxWidth && y < Main.Map.MaxHeight && Main.Map.IsRevealed(x, y))
            : null;
        Flight = new FlightState(center, size, config.EntryDistance);
        ForgetDetachedTargets();
        renderFailed = false;
        renderer?.Reset();
    }

    private void ReturnAt(float centerX, float centerY)
    {
        Player player = Main.LocalPlayer;
        player.position = new Vector2(centerX * 16 - player.width / 2f, centerY * 16 - player.height / 2f);
        player.oldPosition = player.position;
        player.velocity = Vector2.Zero;
        player.fallStart = player.fallStart2 = (int)(player.position.Y / 16);
        ReleaseMouse();
        view.Reset();
        ResetSwordEffects();
        Flight = null;
        exploration = null;
        ViewLayout layout = FirstPersonViewport.Layout;
        Main.screenPosition = player.Center - new Vector2(layout.PixelWidth, layout.PixelHeight) / 2;
        renderer?.Reset();
        Mod.Logger.Info($"Dimension return complete: center={player.Center / 16}; active={player.active}; dead={player.dead}; ghost={player.ghost}");
    }

    private bool ReturnToDeparture(bool forceExit = false)
    {
        if (Flight is null) return true;
        Player player = Main.LocalPlayer;
        NVector2 anchor = new((departurePosition.X + player.width / 2f) / 16, (departurePosition.Y + player.height / 2f) / 16);
        if (forceExit)
        {
            // Leaving the world: no further body simulation runs here.
            ReturnAt(anchor.X, anchor.Y);
            return true;
        }
        if (ReturnSearch.TryNear(world, anchor, Flight.BodySize, exploration, out NVector2 safe))
        {
            ReturnAt(safe.X, safe.Y);
            return true;
        }
        Notify("RecoveryBlocked");
        return false;
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        if (Flight is not null)
        {
            // The list is a per-frame copy, while its layer objects are shared with 2D mode.
            layers.RemoveAll(layer => !layer.Name.Contains("Chat", StringComparison.OrdinalIgnoreCase));
            layers.Insert(0, new LegacyGameInterfaceLayer("DimensionalTerraria:3D", DrawFlight, InterfaceScaleType.None));
        }
    }

    private bool DrawFlight()
    {
        if (Flight is null) return true;
        try
        {
            renderer ??= new DimensionRenderer();
            renderer.Draw(Flight, ReturnStatus, view, exploration);
        }
        catch (Exception exception)
        {
            if (!renderFailed)
            {
                renderFailed = true;
                Mod.Logger.Error("3D rendering failed; restoring the departure position.", exception);
            }
            if (ReturnToDeparture()) Notify("RenderFailed");
        }
        return true;
    }

    public override void PreSaveAndQuit()
    {
        if (Flight is not null) ReturnToDeparture(forceExit: true);
    }

    public override void OnWorldUnload()
    {
        ReleaseMouse();
        view.Reset();
        ResetSwordEffects();
        Flight = null;
        exploration = null;
        renderer?.Reset();
    }

    private void Notify(string key)
    {
        if (messageCooldown > 0) return;
        Main.NewText(Language.GetTextValue("Mods.DimensionalTerraria.Messages." + key), Color.Orange);
        messageCooldown = 30;
    }
}
