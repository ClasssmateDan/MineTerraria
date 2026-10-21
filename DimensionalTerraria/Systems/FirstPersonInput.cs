using System;
using DimensionalTerraria.Configuration;
using DimensionalTerraria.Core;
using DimensionalTerraria.Rendering;
using DimensionalTerraria.World;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ModLoader;
using NVector3 = System.Numerics.Vector3;

namespace DimensionalTerraria.Systems;

public sealed partial class DimensionSystem
{
    private void UpdateFirstPersonInput(KeyboardState keyboard, MouseState mouse, bool togglePressed, bool anchorPressed, bool backpackPressed, bool typing, bool focused)
    {
        if (anchorPressed) { ReturnToDeparture(); return; }
        if (!focused)
        {
            view.Paused = true;
            SuspendMouse();
            return;
        }
        if (typing || Main.ingameOptionsWindow || Main.InGameUI.IsVisible) { SuspendMouse(); return; }

        // Native inventory/autopause can already have changed these flags before
        // PostUpdateInput. The 3D UI owns its own pause and backpack state.
        Main.playerInventory = false;
        if (!view.Paused) Main.gamePaused = false;
        Main.mapFullscreen = false;
        Main.LocalPlayer.mouseInterface = true;
        if (Pressed(Keys.Escape))
        {
            if (view.InventoryOpen) CloseBackpack();
            else view.Paused = !view.Paused;
            SuspendMouse();
            return;
        }
        if (backpackPressed && !view.Paused)
        {
            if (view.InventoryOpen) CloseBackpack(); else view.InventoryOpen = true;
            SuspendMouse();
            return;
        }

        bool clicked = mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released;
        ViewLayout layout = FirstPersonViewport.Layout;
        System.Numerics.Vector2 uiPoint = layout.ToUi(new(mouse.X, mouse.Y));
        Vector2 uiMouse = new(uiPoint.X, uiPoint.Y);
        if (view.Paused)
        {
            if (clicked)
            {
                switch (FirstPersonHud.ClickPause(uiMouse))
                {
                    case HudAction.Resume: view.Paused = false; view.NeedsMouseReset = true; break;
                    case HudAction.ReturnHere: RequestToggle("pause menu"); break;
                    case HudAction.ReturnToStart: ReturnToDeparture(); break;
                }
            }
            return;
        }
        for (int i = 0; i < HotbarSelection.SlotCount; i++)
        {
            Keys key = i == 9 ? Keys.D0 : (Keys)((int)Keys.D1 + i);
            if (Pressed(key)) Main.LocalPlayer.selectedItem = i;
        }
        int steps = (mouse.ScrollWheelValue - previousMouse.ScrollWheelValue) / 120;
        if (steps != 0) Main.LocalPlayer.selectedItem = HotbarSelection.Scroll(Main.LocalPlayer.selectedItem, steps);
        if (view.InventoryOpen)
        {
            int slot = FirstPersonHud.InventorySlot(uiMouse);
            if (slot >= 0) NativeBackpack.Click(slot, clicked,
                mouse.RightButton == ButtonState.Pressed && previousMouse.RightButton == ButtonState.Released);
            if (clicked) view.InventoryBlocked = false;
            return;
        }
        if (togglePressed) { RequestToggle("keybind"); return; }

        int centerX = (int)layout.PixelCenter.X, centerY = (int)layout.PixelCenter.Y;
        if (!view.CursorCaptured || view.NeedsMouseReset)
        {
            Mouse.SetPosition(centerX, centerY);
            view.CursorCaptured = true;
            view.NeedsMouseReset = false;
            return;
        }
        DimensionConfig config = ModContent.GetInstance<DimensionConfig>();
        float sensitivity = MathHelper.ToRadians(config.MouseSensitivity);
        float yaw = Math.Clamp(mouse.X - centerX, -300, 300) * sensitivity;
        float pitch = -Math.Clamp(mouse.Y - centerY, -300, 300) * sensitivity;
        yaw += (Down(Keys.Right) - Down(Keys.Left)) * 0.03f;
        pitch += (Down(Keys.Up) - Down(Keys.Down)) * 0.025f;
        Flight!.Look(yaw, pitch);
        Mouse.SetPosition(centerX, centerY);

        NVector3 input = new(Down(Keys.D) - Down(Keys.A),
            (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift) ? 1 : 0) - Down(Keys.Space),
            Down(Keys.W) - Down(Keys.S));
        view.Moving = input.LengthSquared() > 0;
        Flight.Update(world, 1f / 60f, input, keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl), config.FlightSpeed);
        Update3DInteractions(mouse);

        bool Pressed(Keys key) => keyboard.IsKeyDown(key) && previousKeyboard.IsKeyUp(key);
        int Down(Keys key) => keyboard.IsKeyDown(key) ? 1 : 0;
    }

    private void CloseBackpack()
    {
        if (!NativeBackpack.ReturnCursorItem()) { view.InventoryBlocked = true; return; }
        view.InventoryBlocked = false;
        view.InventoryOpen = false;
    }

    private void SuspendMouse()
    {
        SuspendInteractions();
        view.CursorCaptured = false;
        view.NeedsMouseReset = true;
        view.Moving = false;
    }

    private void ReleaseMouse()
    {
        if (Flight is not null && Main.instance.IsActive)
        {
            ViewLayout layout = FirstPersonViewport.Layout;
            Mouse.SetPosition(Math.Clamp(savedMousePosition.X, 0, layout.PixelWidth - 1), Math.Clamp(savedMousePosition.Y, 0, layout.PixelHeight - 1));
        }
        SuspendMouse();
    }

    private void DrawCursor(On_Main.orig_DrawCursor original, Vector2 bonus, bool smart)
    {
        if (Flight is not null && view.CursorCaptured) return;
        original(bonus, smart);
    }

    private Vector2 DrawThickCursor(On_Main.orig_DrawThickCursor original, bool smart) =>
        Flight is not null && view.CursorCaptured ? Vector2.Zero : original(smart);
}
