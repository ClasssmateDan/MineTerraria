using DimensionalTerraria.Systems;
using DimensionalTerraria.Configuration;
using DimensionalTerraria.Core;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using DimensionalTerraria.Rendering;

namespace DimensionalTerraria.Players;

public sealed class DimensionPlayer : ModPlayer
{
    // Central permission entry point: later equipment, buffs and progression
    // requirements belong here, independently of aiming and native item handling.
    public bool CanInteractIn3D(DimensionAction action)
    {
        DimensionConfig config = ModContent.GetInstance<DimensionConfig>();
        return action switch { DimensionAction.Mine => config.Enable3DMining, DimensionAction.Pickup => config.Enable3DPickup,
            DimensionAction.Attack => config.Enable3DCombat, DimensionAction.Place => config.Enable3DPlacement, DimensionAction.UseItem => true, _ => false };
    }

    public override bool ImmuneTo(PlayerDeathReason damageSource, int cooldownCounter, bool dodgeable) => DimensionSystem.IsDetached(Player);

    public override bool CanBeHitByNPC(NPC npc, ref int cooldownSlot) => !DimensionSystem.IsDetached(Player);

    public override bool CanBeHitByProjectile(Projectile projectile) => !DimensionSystem.IsDetached(Player);

    public override bool CanUseItem(Item item) => !DimensionSystem.IsDetached(Player);

    public override void HideDrawLayers(PlayerDrawSet drawInfo)
    {
        bool detached = DimensionSystem.IsDetached(Player);
        if (!detached && !SteveSkinLayer.CanDraw(drawInfo)) return;
        foreach (PlayerDrawLayer layer in PlayerDrawLayerLoader.Layers)
            if (detached || layer is not SteveSkinLayer && layer is not CrouchPoseLayer && layer != PlayerDrawLayers.HeldItem) layer.Hide();
    }
}
