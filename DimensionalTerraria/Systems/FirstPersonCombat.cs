using System.Collections.Generic;
using DimensionalTerraria.Core;
using DimensionalTerraria.World;
using DimensionalTerraria.Rendering;
using Terraria;
using NVector3 = System.Numerics.Vector3;

namespace DimensionalTerraria.Systems;

public sealed partial class DimensionSystem
{
    internal void RecordHitFeedback(NPC npc, NPC.HitInfo hit)
    {
        if (Flight is null || npc.hide || npc.HideStrikeDamage ||
            exploration is not null && !ExplorationMask.Covers(exploration,
                new(npc.Center.X / 16, npc.Center.Y / 16), new(npc.width / 16f, npc.height / 16f))) return;
        float depth = CreatureRenderer.SpriteDepth(npc);
        view.Feedback.Add(new(npc.Center.X / 16, npc.Top.Y / 16 - 0.15f, depth == 0 ? 0.04f : 0.63f), hit.Damage, hit.Crit,
            new NVector3(npc.Center.X / 16, npc.Center.Y / 16, depth));
    }

    private IEnumerable<CombatBody> CombatBodies()
    {
        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC npc = Main.npc[i];
            if (!NativeCombat.Eligible(Main.LocalPlayer, npc)) continue;
            yield return new CombatBody(i, new(npc.Center.X / 16, npc.Center.Y / 16),
                new(npc.width / 16f, npc.height / 16f), true, CreatureRenderer.SpriteDepth(npc) == 0);
        }
    }

    private CombatHit? AimEnemy(WeaponMode mode, out float stop, out TileRayHit? obstruction)
    {
        float reach = NativeSwordEffects.Reach(Main.LocalPlayer.HeldItem, mode);
        float weight = NativeSwordEffects.DepthWeight(Main.LocalPlayer.HeldItem, mode);
        float rayReach = CombatRules.RayReach(Flight!.Forward, reach, weight);
        obstruction = TileRaycast.Cast(world, Flight.Eye, Flight.Forward, rayReach, exploration);
        stop = obstruction?.Distance ?? rayReach;
        return CombatRules.Target(CombatBodies(), Flight.Eye, Flight.Forward, reach, stop, exploration, weight);
    }

    private void Attack(WeaponMode mode, CombatHit? target, float stop, TileRayHit? obstruction)
    {
        if (Flight is null) return;
        lock (targetingGate)
        {
            if (!NativeCombat.Prepare(Main.LocalPlayer, mode, out AttackStats stats, out string? failure))
            {
                view.CombatHint = failure;
                view.CombatHintTime = 0.8f;
                miningInput.Hit(10);
                return;
            }
            miningInput.Hit(stats.Cooldown);
            view.CombatHint = null;
            view.CombatHintTime = 0;
            view.Swing.Start(stats.Cooldown);
            if (LaunchSwordEffect(stats, stop, target)) return;
            long feedbackBefore = view.Feedback.Events;
            NPC.HitInfo outcome = default;
            bool hit = target is { } victim && NativeCombat.Strike(Main.LocalPlayer, Main.npc[victim.Id], stats, Flight.Position.X, out outcome);
            if (hit && target is { } struck)
            {
                State(Main.npc[struck.Id]).DamageFlash = 0.2f;
                if (view.Feedback.Events == feedbackBefore) RecordHitFeedback(Main.npc[struck.Id], outcome);
                view.HitFlash = 0.24f;
            }
            bool surfaceHit = target is null && obstruction is { Layer: not RayLayer.Unexplored } surfaceTarget &&
                (mode == WeaponMode.Ranged || NativeInteractions.IsBoat(surfaceTarget));
            if (surfaceHit && obstruction is { } tile)
            {
                MiningSurface surface = MiningParticleRenderer.Capture(tile);
                int type = Main.tile[tile.X, tile.Y].TileType;
                bool boatBroken = NativeInteractions.IsBoat(tile) && NativeInteractions.BreakBoat(tile);
                if (!boatBroken && CanInteract(DimensionAction.Mine)) NativeInteractions.ShootTile(Main.LocalPlayer, tile, stats.Damage);
                Tile after = Main.tile[tile.X, tile.Y];
                bool broken = boatBroken || tile.Layer == RayLayer.Tile && (!after.HasTile || after.TileType != type);
                renderer?.EmitMiningParticles(surface, Flight.Eye + Flight.Forward * tile.Distance, broken);
                renderer?.InvalidateTile(tile.X, tile.Y);
                view.HitFlash = 0.18f;
                view.CombatHint = broken ? "ShotBlockBroken" : "ShotBlockHit";
                view.CombatHintTime = 0.4f;
            }
            NVector3 end = target?.Point ?? Flight.Eye + Flight.Forward * stop;
            NVector3 right = new(System.MathF.Cos(Flight.Yaw), 0, System.MathF.Sin(Flight.Yaw));
            NVector3 start = Flight.Eye + right * 0.22f + NVector3.UnitY * 0.16f + Flight.Forward * 0.3f;
            renderer?.EmitCombatTrace(start, end, mode, hit || surfaceHit);
        }
    }
}
