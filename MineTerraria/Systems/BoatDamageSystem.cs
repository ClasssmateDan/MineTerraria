using System;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DimensionalTerraria.Systems;

[Autoload(Side = ModSide.Client)]
public sealed class BoatDamageSystem : ModSystem
{
    public override void Load()
    {
        On_NPC.StrikeNPC_HitInfo_bool_bool += BeforeHit;
        IL_NPC.UpdateNPC_BuffApplyDOTs += BeforeDotDamage;
    }
    public override void Unload()
    {
        On_NPC.StrikeNPC_HitInfo_bool_bool -= BeforeHit;
        IL_NPC.UpdateNPC_BuffApplyDOTs -= BeforeDotDamage;
    }

    internal static void ReleaseBeforeDamage(NPC npc)
    {
        bool boss = GroundBossCapture.IsBoss(npc);
        if (!boss || !npc.active || npc.life <= 0 || npc.immortal || !npc.TryGetGlobalNPC<BoatCaptive>(out var state)) return;
        if (state.Host is { } boat && ReferenceEquals(boat.Occupant, npc)) boat.Break();
    }
    private static int BeforeHit(On_NPC.orig_StrikeNPC_HitInfo_bool_bool original, NPC npc, NPC.HitInfo hit, bool fromNet, bool noPlayerInteraction)
    {
        if (hit.Damage > 0 || hit.InstantKill) ReleaseBeforeDamage(npc);
        return original(npc, hit, fromNet, noPlayerInteraction);
    }
    private static void BeforeDotDamage(ILContext context)
    {
        // DOT subtracts life directly, bypassing StrikeNPC for non-lethal ticks.
        // Insert before each negative life write, leaving the native calculation,
        // regen accumulator, combat text and death pipeline unchanged.
        var cursor = new ILCursor(context);
        int sites = 0;
        while (cursor.TryGotoNext(MoveType.Before, i => i.MatchSub(), i => i.MatchStfld<NPC>(nameof(NPC.life))))
        {
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Action<NPC>>(ReleaseBeforeDamage);
            cursor.Index += 2;
            sites++;
        }
        if (sites < 2) throw new InvalidOperationException("Boat damage: the engine DOT damage points could not be located.");
    }
}
