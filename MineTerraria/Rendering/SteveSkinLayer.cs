using DimensionalTerraria.Players;
using DimensionalTerraria.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using ReLogic.Content;

namespace DimensionalTerraria.Rendering;

public sealed class SteveSkinLayer : PlayerDrawLayer
{
    public override Position GetDefaultPosition() => new BeforeParent(PlayerDrawLayers.HeldItem);

    internal static bool CanDraw(PlayerDrawSet drawInfo) => !Main.dedServ &&
        drawInfo.drawPlayer.GetModPlayer<SteveCharacter>().IsSteve && !DimensionSystem.IsDetached(drawInfo.drawPlayer) &&
        !drawInfo.drawPlayer.invis && !drawInfo.headOnlyRender && Atlas.Ready;

    private static PixelSpriteAtlas Atlas => PixelSpriteAtlas.Request("DimensionalTerraria/Textures/Steve");

    public override bool GetDefaultVisibility(PlayerDrawSet drawInfo) => CanDraw(drawInfo);

    protected override void Draw(ref PlayerDrawSet drawInfo)
    {
        Player player = drawInfo.drawPlayer;
        PixelSpriteAtlas atlas = Atlas;
        int frame = player.itemAnimation > 0 ? (player.itemAnimation > player.itemAnimationMax / 2 ? 6 : 7) :
            player.velocity.Y != 0 ? 5 : System.MathF.Abs(player.velocity.X) > 0.15f ? 1 + (int)(Main.GameUpdateCount / 7 % 4) : 0;
        if (!atlas.TryFrame(frame, out var crop)) return;
        Vector2 position = drawInfo.Position + new Vector2(player.width / 2f, player.height + player.gfxOffY) - Main.screenPosition;
        Vector2 origin = PixelSpriteAtlas.Origin(crop);
        SpriteEffects effects = SpriteEffects.None;
        if (player.direction == -1) { effects |= SpriteEffects.FlipHorizontally; origin.X = crop.Width - origin.X; }
        if (player.gravDir == -1) { effects |= SpriteEffects.FlipVertically; origin.Y = 0; position.Y -= player.height; }
        // Reuse the renderer's light/immune/shadow color. Menu preview characters
        // have no live world position from which to sample tile lighting.
        Color tint = drawInfo.colorArmorBody;
        drawInfo.DrawDataCache.Add(new DrawData(atlas.Texture, position, PixelSpriteAtlas.Source(crop), tint,
            player.fullRotation, origin, 42f / atlas.RestHeight, effects, 0));
        // All overlays share Steve's source frame, origin and scale. Equipping
        // armor must never replace his body with the native Terraria skin.
        if (ProjectionArmorAppearance.LegsVisible(player)) drawInfo.DrawDataCache.Add(Armor("Legs", drawInfo.colorArmorLegs, drawInfo.cLegs));
        if (ProjectionArmorAppearance.ChestVisible(player)) drawInfo.DrawDataCache.Add(Armor("Body", drawInfo.colorArmorBody, drawInfo.cBody));
        if (ProjectionArmorAppearance.HeadVisible(player)) drawInfo.DrawDataCache.Add(Armor("Head", drawInfo.colorArmorHead, drawInfo.cHead));

        DrawData Armor(string part, Color color, int shader) => new(
            StorageTextures.Sprite("DimensionalTerraria/Textures/Equipment/SteveArmor_" + part).Value,
            position, PixelSpriteAtlas.Source(crop), color, player.fullRotation, origin, 42f / atlas.RestHeight, effects, 0) { shader = shader };
    }
}
