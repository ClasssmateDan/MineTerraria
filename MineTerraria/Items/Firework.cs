using System;
using DimensionalTerraria.Core;
using DimensionalTerraria.Systems;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace DimensionalTerraria.Items;

public sealed class Firework : ModItem
{
    private bool launched;
    public override string Texture => "DimensionalTerraria/Textures/Minecraft/firework_rocket";

    public override void SetDefaults()
    {
        launched = false;
        Item.width = Item.height = 16;
        Item.maxStack = 9999;
        Item.consumable = true;
        Item.useStyle = ItemUseStyleID.HoldUp;
        Item.useTime = Item.useAnimation = 20;
        Item.autoReuse = true;
        Item.rare = ItemRarityID.Blue;
        Item.value = Item.buyPrice(copper: 20);
    }

    public override bool CanUseItem(Player player)
    {
        launched = false;
        return Main.netMode == NetmodeID.SinglePlayer && !DimensionSystem.IsDetached(player);
    }

    public override bool? UseItem(Player player) => launched || TryLaunch(player, null, Main.MouseWorld);
    public override bool ConsumeItem(Player player) => launched;

    internal bool TryLaunch(Player player, FlightState? flight, Vector2 aim)
    {
        launched = false;
        if (Main.netMode != NetmodeID.SinglePlayer || !player.active || player.dead || player.ghost || player.noItems || player.CCed || Item.stack <= 0) return false;
        if (flight is not null)
        {
            if (!flight.UseFirework()) return false;
        }
        else
        {
            Vector2 direction = aim - player.Center;
            if (!float.IsFinite(direction.X) || !float.IsFinite(direction.Y) || direction.LengthSquared() < .01f) return false;
            Vector2 velocity = player.velocity + Vector2.Normalize(direction) * 16f;
            player.velocity = velocity.LengthSquared() > 24f * 24f ? Vector2.Normalize(velocity) * 24f : velocity;
        }
        if (!Main.dedServ) SoundEngine.PlaySound(SoundID.Item13, player.Center);
        return launched = true;
    }

    public override void AddRecipes() => CreateRecipe(3).AddIngredient(ItemID.Wood).AddIngredient(ItemID.Gel).Register();
}
