using System;
using System.Collections.Generic;
using System.Linq;
using DimensionalTerraria.Core;
using DimensionalTerraria.Rendering;
using DimensionalTerraria.World;
using Terraria;
using Terraria.Audio;
using Terraria.Graphics;
using Terraria.ID;
using NVector3 = System.Numerics.Vector3;

namespace DimensionalTerraria.Systems;

public sealed partial class DimensionSystem
{
    private readonly Dictionary<SwordFlight, AttackStats> swordAttacks = new();
    private int swordSequence;

    private bool LaunchSwordEffect(AttackStats stats, float stop, CombatHit? target)
    {
        SwordEffect effect = NativeSwordEffects.Effect(stats.Weapon);
        if (effect == SwordEffect.None || Flight is null) return false;
        int sequence = swordSequence++;
        int skin = effect == SwordEffect.Zenith
            ? sequence % 3 == 0 ? ItemID.Zenith : FinalFractalHelper.GetRandomProfileIndex()
            : ProjectileID.DD2SquireSonicBoom;
        NVector3 start = Flight.Eye + Flight.Forward * 0.12f;
        float distance = Math.Max(0.2f, effect == SwordEffect.Zenith ? target?.Distance ?? stop : stop);
        NVector3 end = Flight.Eye + Flight.Forward * distance;
        // Aimed weapons can be used even when no enemy is under the crosshair.
        var blade = new SwordFlight(effect, skin, start, end,
            new(MathF.Cos(Flight.Yaw), 0, MathF.Sin(Flight.Yaw)), Math.Max(12, stats.Weapon.shootSpeed * 60 / 16), sequence);
        if (view.SwordFlights.Count >= 64)
        {
            swordAttacks.Remove(view.SwordFlights[0]);
            view.SwordFlights.RemoveAt(0);
        }
        view.SwordFlights.Add(blade);
        swordAttacks[blade] = stats;
        if (effect == SwordEffect.Zenith) SoundEngine.PlaySound(SoundID.Item169);
        return true;
    }

    private void ResetSwordEffects()
    {
        swordAttacks.Clear();
        view.SwordFlights.Clear();
        swordSequence = 0;
    }

    private void UpdateSwordEffects()
    {
        if (Flight is null) return;
        if (!Main.LocalPlayer.GetModPlayer<Players.DimensionPlayer>().CanInteractIn3D(DimensionAction.Attack))
        { ResetSwordEffects(); return; }
        lock (targetingGate)
        for (int i = view.SwordFlights.Count - 1; i >= 0; i--)
        {
            SwordFlight blade = view.SwordFlights[i];
            blade.Update(1f / 60);
            NVector3 delta = blade.Position - blade.Previous;
            float length = delta.Length();
            TileRayHit? wall = length > 0.000001f ? TileRaycast.Cast(world, blade.Previous, delta, length, exploration) : null;
            float limit = wall?.Distance ?? length;
            foreach (CombatHit hit in blade.Sweep(CombatBodies(), limit, exploration, world).OrderBy(hit => hit.Distance))
            {
                NPC npc = Main.npc[hit.Id];
                long before = view.Feedback.Events;
                if (!NativeCombat.Strike(Main.LocalPlayer, npc, swordAttacks[blade], Flight.Position.X, out var outcome)) continue;
                blade.MarkHit(hit.Id);
                State(npc).DamageFlash = 0.2f;
                if (view.Feedback.Events == before) RecordHitFeedback(npc, outcome);
                view.HitFlash = 0.24f;
                renderer?.EmitCombatTrace(hit.Point, hit.Point, WeaponMode.Melee, true);
                if (!blade.CanHitMore) break;
            }
            if (wall is { } blocked)
            {
                if (blocked.Layer != RayLayer.Unexplored)
                {
                    NVector3 position = blade.Previous + NVector3.Normalize(delta) * blocked.Distance;
                    MiningSurface surface = MiningParticleRenderer.Capture(blocked);
                    bool boat = NativeInteractions.IsBoat(blocked);
                    if (boat) { NativeInteractions.BreakBoat(blocked); renderer?.InvalidateTile(blocked.X, blocked.Y); }
                    renderer?.EmitMiningParticles(surface, position, boat);
                }
                blade.Stop();
            }
            if (!blade.Active)
            {
                swordAttacks.Remove(blade);
                view.SwordFlights.RemoveAt(i);
            }
        }
    }
}
