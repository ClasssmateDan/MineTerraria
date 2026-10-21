using DimensionalTerraria.Core;
using DimensionalTerraria.Systems;
using DimensionalTerraria.World;
using Microsoft.Xna.Framework;
using Terraria.ModLoader;

namespace DimensionalTerraria.Players;

public sealed class CrouchPlayer : ModPlayer
{
    private readonly TerrariaCollisionWorld world = new();
    public float Amount { get; private set; }
    internal bool IsCrouching => Player.active && !Player.dead && !Player.ghost && !Player.CCed &&
        Player.controlDown && Player.gravDir == 1 && Player.velocity.Y == 0 && Player.fullRotation == 0 &&
        !Player.mount.Active && !Player.pulley && !Player.sitting.isSitting && !Player.sleeping.isSleeping &&
        !DimensionSystem.IsDetached(Player) && Support is { Ground: true, DropThrough: false };
    private FootSupport Support => CrouchRules.Support(world, Player.Left.X / 16, Player.Bottom.Y / 16, Player.width / 16f);

    public override void PostUpdateRunSpeeds()
    {
        if (!IsCrouching) return;
        Player.maxRunSpeed *= .3f;
        Player.accRunSpeed *= .3f;
    }
    public override void PreUpdateMovement()
    {
        if (!IsCrouching) return;
        float x = MathHelper.Clamp(Player.velocity.X, -Player.maxRunSpeed, Player.maxRunSpeed);
        Player.velocity.X = CrouchRules.GuardStep(world, Player.Left.X / 16, Player.Bottom.Y / 16, Player.width / 16f, x / 16) * 16;
    }
    public override void PostUpdate() => Amount = IsCrouching ? 1 : 0;
    public override void UpdateDead() => Amount = 0;
    public override void OnEnterWorld() => Amount = 0;
}
