using DimensionalTerraria.Core;
using DimensionalTerraria.Systems;
using Terraria;
using Terraria.ModLoader;

namespace DimensionalTerraria.NPCs;

public sealed class DimensionEnemyState : GlobalNPC
{
    public override bool InstancePerEntity => true;
    internal bool Managed;
    internal int EngagedPlayer = TargetExclusionScope.NoPlayer;
    internal IdleWander? Idle;
    internal float DamageFlash;

    public override void SetDefaults(NPC npc)
    {
        Managed = false;
        EngagedPlayer = TargetExclusionScope.NoPlayer;
        Idle = null;
        DamageFlash = 0;
    }

    public override void HitEffect(NPC npc, NPC.HitInfo hit)
    {
        if (hit.Damage <= 0 || Main.dedServ || !DimensionSystem.IsDetached(Main.LocalPlayer)) return;
        DamageFlash = 0.20f;
        DimensionSystem.Instance.RecordHitFeedback(npc, hit);
    }
}
