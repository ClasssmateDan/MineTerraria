using System;
using System.Collections.Generic;
using DimensionalTerraria.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;

namespace DimensionalTerraria.Rendering;

internal sealed class FirstPersonView
{
    public bool Paused;
    public bool InventoryOpen;
    public bool CursorCaptured;
    public bool NeedsMouseReset = true;
    public bool Moving;
    public TileRayHit? Target;
    public bool TargetReady;
    public bool TargetIsShot;
    public bool TargetIsEgg;
    public string SpawnHint = "EggTarget";
    public bool InventoryBlocked;
    public readonly MiningSwing Swing = new();
    public CombatHit? Enemy;
    public string? CombatHint;
    public float CombatHintTime;
    public float HitFlash;
    public readonly CombatFeedback Feedback = new();
    public readonly List<SwordFlight> SwordFlights = new();

    public void Reset()
    {
        Paused = InventoryOpen = CursorCaptured = Moving = false;
        NeedsMouseReset = true;
        Target = null;
        TargetReady = false;
        TargetIsShot = false;
        TargetIsEgg = InventoryBlocked = false;
        Swing.Reset();
        Feedback.Clear();
        SwordFlights.Clear();
        Enemy = null; CombatHint = null; CombatHintTime = HitFlash = 0;
    }
}

internal enum HudAction { None, Resume, ReturnHere, ReturnToStart }

internal static class FirstPersonHud
{
    private const int Cell = 46;
    private const string Prefix = "Mods.DimensionalTerraria.FirstPerson.";
    private static float Width => FirstPersonViewport.Layout.UiWidth;
    private static float Height => FirstPersonViewport.Layout.UiHeight;
    private static Texture2D Pixel => TextureAssets.MagicPixel.Value;

    public static void Draw(FlightState flight, ReturnFailure status, FirstPersonView view)
    {
        if (!view.Paused && !view.InventoryOpen)
        {
            DrawCrosshair(view.HitFlash > 0 ? Color.OrangeRed : view.Enemy is not null ? Color.LightGreen : Color.White);
            if (view.HitFlash > 0) DrawHitMarker();
            if (view.CombatHint is { } hint)
                CenterText(Language.GetTextValue(Prefix + hint), new Vector2(Width / 2, Height / 2 + 24),
                    hint.StartsWith("Shot", StringComparison.Ordinal) ? Color.LightGreen : Color.Salmon, 0.65f);
            else if (view.Enemy is { } enemy && Main.npc[enemy.Id].active)
            {
                NPC npc = Main.npc[enemy.Id];
                CenterText($"{npc.FullName}  {npc.life}/{npc.lifeMax}", new Vector2(Width / 2, Height / 2 + 24), Color.White, 0.65f);
            }
            else if (view.Target is { } target)
                CenterText(Language.GetTextValue(Prefix + (target.Layer == RayLayer.Unexplored ? "TargetUnexplored" : view.TargetIsEgg ? view.TargetReady ? view.SpawnHint : "EggNoSpace" : view.TargetIsShot ? "ShotTarget" : view.TargetReady ? "MineTarget" : "NeedTool")),
                    new Vector2(Width / 2, Height / 2 + 24), view.TargetReady ? Color.White : Color.Salmon, 0.65f);
            else if (view.TargetIsEgg && view.SpawnHint == "PlaceTarget")
                CenterText(Language.GetTextValue(Prefix + "PlaceBlocked"), new Vector2(Width / 2, Height / 2 + 24), Color.Salmon, .65f);
        }
        DrawHotbar(status);
        if (view.InventoryOpen) DrawInventory(view.InventoryBlocked);
        if (view.Paused) DrawPause(status);
    }

    public static HudAction ClickPause(Vector2 mouse)
    {
        for (int i = 0; i < 3; i++)
            if (PauseButton(i).Contains(mouse.ToPoint())) return (HudAction)(i + 1);
        return HudAction.None;
    }

    public static int ClickHotbar(Vector2 mouse, bool inventoryOpen)
    {
        Rectangle bar = HotbarRect();
        if (bar.Contains(mouse.ToPoint())) return Math.Clamp(((int)mouse.X - bar.X) / Cell, 0, HotbarSelection.SlotCount - 1);
        if (inventoryOpen)
        {
            Rectangle panel = InventoryRect();
            Rectangle row = new(panel.X + 16, panel.Y + 44 + 4 * Cell, Cell * HotbarSelection.SlotCount, Cell);
            if (row.Contains(mouse.ToPoint())) return Math.Clamp(((int)mouse.X - row.X) / Cell, 0, HotbarSelection.SlotCount - 1);
        }
        return -1;
    }

    public static int InventorySlot(Vector2 mouse)
    {
        Rectangle panel = InventoryRect();
        Rectangle slots = new(panel.X + 16, panel.Y + 44, Cell * 10, Cell * 5);
        if (!slots.Contains(mouse.ToPoint())) return -1;
        int column = ((int)mouse.X - slots.X) / Cell, row = ((int)mouse.Y - slots.Y) / Cell;
        return row == 4 ? column : 10 + row * 10 + column;
    }

    private static Rectangle HotbarRect() => new((int)(Width / 2 - Cell * HotbarSelection.SlotCount / 2f), (int)Height - Cell - 14, Cell * HotbarSelection.SlotCount, Cell);
    private static Rectangle InventoryRect() => new((int)(Width / 2 - (Cell * 10 + 32) / 2f), (int)(Height / 2 - (Cell * 5 + 84) / 2f), Cell * 10 + 32, Cell * 5 + 84);
    private static Rectangle PauseButton(int index) => new((int)(Width / 2 - 145), (int)(Height / 2 - 44 + index * 56), 290, 44);

    private static void DrawHotbar(ReturnFailure status)
    {
        Player player = Main.LocalPlayer;
        Rectangle bar = HotbarRect();
        Fill(new Rectangle(bar.X - 4, bar.Y - 4, bar.Width + 8, bar.Height + 8), new Color(15, 16, 20, 220));
        for (int i = 0; i < HotbarSelection.SlotCount; i++) DrawSlot(new Rectangle(bar.X + i * Cell, bar.Y, Cell, Cell), player.inventory[i], player.selectedItem == i, i);

        float hp = player.statLife / (float)Math.Max(1, player.statLifeMax2);
        Texture2D heart = TextureAssets.Heart.Value;
        for (int i = 0; i < 10; i++)
        {
            Rectangle destination = new(bar.X + i * 17, bar.Y - 24, 16, 16);
            Main.spriteBatch.Draw(heart, destination, new Color(55, 30, 30));
            float fill = Math.Clamp(hp * 10 - i, 0, 1);
            if (fill > 0)
            {
                Rectangle source = new(0, 0, Math.Max(1, (int)(heart.Width * fill)), heart.Height);
                Main.spriteBatch.Draw(heart, new Rectangle(destination.X, destination.Y, Math.Max(1, (int)(16 * fill)), 16), source, Color.White);
            }
        }
        float mana = player.statMana / (float)Math.Max(1, player.statManaMax2);
        for (int i = 0; i < 10; i++)
        {
            Rectangle cell = new(bar.Right - 170 + i * 17, bar.Y - 21, 14, 10);
            Fill(cell, new Color(22, 33, 54));
            int fill = (int)(Math.Clamp(mana * 10 - i, 0, 1) * cell.Width);
            if (fill > 0) Fill(new Rectangle(cell.X, cell.Y, fill, cell.Height), new Color(82, 156, 241));
        }
        Item held = player.inventory[player.selectedItem];
        if (!held.IsAir) CenterText(held.Name, new Vector2(Width / 2, bar.Y - 55), Color.White, 0.8f);
        string key = string.Join(" / ", global::DimensionalTerraria.DimensionalTerraria.AssignedKeys(global::DimensionalTerraria.DimensionalTerraria.ToggleDimension));
        string returnText = status switch { ReturnFailure.None => "ReturnReady", ReturnFailure.Unexplored => "ReturnUnexplored", _ => "ReturnBlocked" };
        CenterText(Language.GetTextValue(Prefix + returnText, key),
            new Vector2(Width / 2, bar.Y - 82), status == ReturnFailure.None ? new Color(175, 235, 177) : new Color(255, 154, 136), 0.65f);
    }

    private static void DrawSlot(Rectangle cell, Item item, bool selected, int index = -1)
    {
        Fill(cell, new Color(75, 77, 82, 240));
        Fill(new Rectangle(cell.X + 3, cell.Y + 3, cell.Width - 6, cell.Height - 6), new Color(30, 32, 37, 240));
        Outline(cell, selected ? new Color(239, 239, 224) : new Color(100, 104, 112), selected ? 3 : 1);
        if (index >= 0) Text(index == 9 ? "0" : (index + 1).ToString(), new Vector2(cell.X + 4, cell.Y + 2), new Color(177, 180, 187), 0.45f);
        if (item.IsAir) return;
        Main.instance.LoadItem(item.type);
        Texture2D texture = TextureAssets.Item[item.type].Value;
        Rectangle frame = Main.itemAnimations[item.type]?.GetFrame(texture) ?? new Rectangle(0, 0, texture.Width, texture.Height);
        float scale = Math.Min(1, 32f / Math.Max(frame.Width, frame.Height));
        Main.spriteBatch.Draw(texture, new Vector2(cell.Center.X, cell.Center.Y), frame, Color.White, 0, frame.Size() / 2, scale, SpriteEffects.None, 0);
        if (item.stack > 1)
        {
            string count = item.stack.ToString();
            Vector2 size = FontAssets.MouseText.Value.MeasureString(count) * 0.65f;
            Text(count, new Vector2(cell.Right - size.X - 3, cell.Bottom - size.Y + 1), Color.White, 0.65f);
        }
    }

    private static void DrawCrosshair(Color color)
    {
        int x = (int)(Width / 2), y = (int)(Height / 2);
        Fill(new Rectangle(x - 7, y - 2, 15, 5), new Color(0, 0, 0, 100));
        Fill(new Rectangle(x - 2, y - 7, 5, 15), new Color(0, 0, 0, 100));
        Fill(new Rectangle(x - 6, y, 13, 1), color);
        Fill(new Rectangle(x, y - 6, 1, 13), color);
    }

    private static void DrawHitMarker()
    {
        Vector2 center = new(Width / 2, Height / 2);
        for (int x = -1; x <= 1; x += 2)
        for (int y = -1; y <= 1; y += 2)
        {
            float angle = MathF.Atan2(y, x);
            Main.spriteBatch.Draw(Pixel, center + new Vector2(x, y) * 5, new Rectangle(0, 0, 1, 1), Color.Gold, angle,
                new Vector2(0, 0.5f), new Vector2(8, 2), SpriteEffects.None, 0);
        }
    }

    private static void DrawInventory(bool blocked)
    {
        Fill(new Rectangle(0, 0, (int)Width, (int)Height), new Color(0, 0, 0, 145));
        Rectangle panel = InventoryRect();
        Fill(panel, new Color(184, 186, 190)); Outline(panel, new Color(231, 232, 235), 3);
        Text(Language.GetTextValue(Prefix + "Inventory"), new Vector2(panel.X + 16, panel.Y + 10), new Color(38, 40, 44), 0.9f);
        for (int row = 0; row < 5; row++)
        for (int column = 0; column < 10; column++)
        {
            int index = row == 4 ? column : 10 + row * 10 + column;
            DrawSlot(new Rectangle(panel.X + 16 + column * Cell, panel.Y + 44 + row * Cell, Cell, Cell), Main.LocalPlayer.inventory[index],
                index == Main.LocalPlayer.selectedItem, row == 4 ? column : -1);
        }
        Text(Language.GetTextValue(Prefix + (blocked ? "InventoryFull" : "InventoryHint")), new Vector2(panel.X + 16, panel.Bottom - 29), new Color(45, 47, 52), 0.55f);
        MouseState raw = Mouse.GetState();
        Vector2 mouse = new(raw.X / Main.UIScale, raw.Y / Main.UIScale);
        int hovered = InventorySlot(mouse);
        if (hovered >= 0 && !Main.LocalPlayer.inventory[hovered].IsAir)
            Text(Main.LocalPlayer.inventory[hovered].Name, new(panel.X + 16, panel.Bottom + 8), Color.White, 0.7f);
        if (!Main.mouseItem.IsAir) DrawSlot(new Rectangle((int)mouse.X + 10, (int)mouse.Y + 10, Cell, Cell), Main.mouseItem, false, -1);
        Main.spriteBatch.Draw(TextureAssets.Cursors[0].Value, mouse, Color.White);
    }

    private static void DrawPause(ReturnFailure status)
    {
        Fill(new Rectangle(0, 0, (int)Width, (int)Height), new Color(0, 0, 0, 150));
        CenterText(Language.GetTextValue(Prefix + "Pause"), new Vector2(Width / 2, Height / 2 - 110), Color.White, 1.1f);
        string[] names = { "Resume", "ReturnHere", "ReturnStart" };
        MouseState rawMouse = Mouse.GetState();
        Point mouse = new((int)(rawMouse.X / Main.UIScale), (int)(rawMouse.Y / Main.UIScale));
        for (int i = 0; i < names.Length; i++)
        {
            Rectangle button = PauseButton(i);
            bool disabled = i == 1 && status != ReturnFailure.None;
            Fill(button, disabled ? new Color(47, 48, 53) : button.Contains(mouse) ? new Color(105, 116, 144) : new Color(78, 82, 92));
            Outline(button, new Color(170, 173, 184), 1);
            CenterText(Language.GetTextValue(Prefix + names[i]), new Vector2(button.Center.X, button.Y + 10), disabled ? Color.Gray : Color.White, 0.8f);
        }
    }

    private static void Fill(Rectangle rectangle, Color color) => Main.spriteBatch.Draw(Pixel, rectangle, color);
    private static void Outline(Rectangle rectangle, Color color, int thickness)
    {
        Fill(new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, thickness), color);
        Fill(new Rectangle(rectangle.X, rectangle.Bottom - thickness, rectangle.Width, thickness), color);
        Fill(new Rectangle(rectangle.X, rectangle.Y, thickness, rectangle.Height), color);
        Fill(new Rectangle(rectangle.Right - thickness, rectangle.Y, thickness, rectangle.Height), color);
    }
    private static void Text(string text, Vector2 position, Color color, float scale) => Utils.DrawBorderString(Main.spriteBatch, text, position, color, scale);
    private static void CenterText(string text, Vector2 center, Color color, float scale) => Text(text, new Vector2(center.X - FontAssets.MouseText.Value.MeasureString(text).X * scale / 2, center.Y), color, scale);
}
