using System;
using System.Collections.Generic;
using DimensionalTerraria.Core;
using DimensionalTerraria.NPCs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using NVector3 = System.Numerics.Vector3;
using NMatrix = System.Numerics.Matrix4x4;

namespace DimensionalTerraria.Rendering;

internal static class CreatureStatusHud
{
    public static void Draw(IReadOnlyList<CreatureAnchor> anchors, FirstPersonView view, FlightState flight,
        IInteractionWorld world, IExplorationMap? exploration, Matrix viewProjection)
    {
        if (view.Paused || view.InventoryOpen) return;
        NMatrix transform = Numeric(viewProjection);
        ViewLayout layout = FirstPersonViewport.Layout;
        foreach (CreatureAnchor anchor in anchors)
        {
            NPC npc = anchor.Npc;
            if (!npc.active || npc.life <= 0 || npc.lifeMax <= 1 || npc.HideStrikeDamage ||
                npc.life >= npc.lifeMax && npc.ModNPC is not DebugTargetDummy && view.Enemy?.Id != npc.whoAmI) continue;
            if (!Project(anchor.Top, out Vector2 screen) || !Visible(anchor.Center)) continue;
            float fraction = CreatureOverlayRules.HealthFraction(npc.life, npc.lifeMax);
            int width = 80, height = 7;
            Rectangle bar = new((int)screen.X - width / 2, (int)screen.Y, width, height);
            Fill(new(bar.X - 2, bar.Y - 2, bar.Width + 4, bar.Height + 4), new Color(15, 18, 22, 220));
            Fill(bar, new Color(75, 26, 29, 230));
            Fill(new(bar.X, bar.Y, Math.Max(1, (int)(width * fraction)), height), Color.Lerp(Color.OrangeRed, Color.LightGreen, fraction));
            CenterText($"{npc.life}/{npc.lifeMax}", new(screen.X, screen.Y - 17), Color.White, 0.55f);
        }
        // Popups keep world positions instead of NPC slots, so a killing blow
        // remains visible and a reused slot cannot inherit another NPC's damage.
        foreach (DamagePopup popup in view.Feedback.Popups)
        {
            if (!Project(popup.Position, out Vector2 screen) || !Visible(popup.VisibilityOrigin)) continue;
            Color color = (popup.Critical ? Color.Gold : new Color(255, 210, 160)) * popup.Opacity;
            CenterText(popup.Critical ? $"{popup.Damage}!" : popup.Damage.ToString(), screen, color, popup.Critical ? 0.95f : 0.78f);
        }

        bool Visible(NVector3 point)
        {
            NVector3 delta = point - flight.Eye;
            float distance = delta.Length();
            if (!float.IsFinite(distance) || distance > 128) return false;
            return distance < 0.04f || TileRaycast.Cast(world, flight.Eye, delta, distance - 0.03f, exploration) is null;
        }

        bool Project(NVector3 point, out Vector2 screen)
        {
            bool visible = CreatureOverlayRules.Project(new(point.X, -point.Y, point.Z), transform,
                layout.UiWidth, layout.UiHeight, out System.Numerics.Vector2 ui);
            screen = new(ui.X, ui.Y);
            return visible;
        }
    }

    private static void Fill(Rectangle rectangle, Color color) => Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, rectangle, color);
    private static void CenterText(string text, Vector2 position, Color color, float scale) =>
        Utils.DrawBorderString(Main.spriteBatch, text, position, color, scale, 0.5f);
    private static NMatrix Numeric(Matrix m) => new(m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24,
        m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44);
}
