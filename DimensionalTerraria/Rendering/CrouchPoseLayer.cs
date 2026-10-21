using System;
using System.Collections.Generic;
using DimensionalTerraria.Core;
using DimensionalTerraria.Players;
using DimensionalTerraria.Systems;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace DimensionalTerraria.Rendering;

public sealed class CrouchPoseLayer : PlayerDrawLayer
{
    public override Position GetDefaultPosition() => PlayerDrawLayers.AfterLastVanillaLayer;
    public override bool GetDefaultVisibility(PlayerDrawSet info) => !info.headOnlyRender && !info.drawPlayer.invis &&
        info.drawPlayer.gravDir == 1 && !DimensionSystem.IsDetached(info.drawPlayer) && info.drawPlayer.GetModPlayer<CrouchPlayer>().Amount > 0;

    protected override void Draw(ref PlayerDrawSet info)
    {
        Player player = info.drawPlayer;
        float amount = player.GetModPlayer<CrouchPlayer>().Amount;
        float feet = info.Position.Y + player.height + player.gfxOffY - Main.screenPosition.Y;
        var output = new List<DrawData>(info.DrawDataCache.Count * 20);
        int sourceIndex = 0, projectileIndex = info.projectileDrawPosition;
        foreach (DrawData draw in info.DrawDataCache)
        {
            if (sourceIndex++ == projectileIndex) info.projectileDrawPosition = output.Count;
            DrawData data = draw;
            if (MathF.Abs(data.rotation) > .001f || data.scale.Y <= 0 || data.effect.HasFlag(Microsoft.Xna.Framework.Graphics.SpriteEffects.FlipVertically))
            {
                var offset = CrouchRules.Offset(feet - data.position.Y, amount, player.direction);
                data.position += new Vector2(offset.X, offset.Y);
                output.Add(data); continue;
            }
            Rectangle source = data.sourceRect ?? data.texture.Bounds;
            int step = Math.Max(1, (int)MathF.Round(1 / data.scale.Y));
            for (int row = 0; row < source.Height; row += step)
            {
                DrawData strip = data;
                int height = Math.Min(step, source.Height - row);
                float screenY = data.position.Y + (row + height * .5f - data.origin.Y) * data.scale.Y;
                var offset = CrouchRules.Offset(feet - screenY, amount, player.direction);
                strip.sourceRect = new Rectangle(source.X, source.Y + row, source.Width, height);
                strip.origin.Y -= row;
                strip.position += new Vector2(offset.X, offset.Y);
                output.Add(strip);
            }
        }
        if (projectileIndex == sourceIndex) info.projectileDrawPosition = output.Count;
        info.DrawDataCache.Clear();
        info.DrawDataCache.AddRange(output);
    }
}
