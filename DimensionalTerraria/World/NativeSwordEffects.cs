using DimensionalTerraria.Core;
using Terraria;
using Terraria.ID;

namespace DimensionalTerraria.World;

internal static class NativeSwordEffects
{
    internal static SwordEffect Effect(Item item) => item.type switch
    {
        ItemID.Zenith => SwordEffect.Zenith,
        ItemID.DD2SquireBetsySword => SwordEffect.FlyingDragon,
        _ => SwordEffect.None
    };

    internal static float Reach(Item item, WeaponMode mode) => Effect(item) == SwordEffect.Zenith ? CombatRules.ZenithReach :
        mode != WeaponMode.Melee || Effect(item) != SwordEffect.None ? CombatRules.ShotReach : CombatRules.MeleeReach;

    // A projectile's flight already has its own range. The shorter direct
    // melee strike is the part whose depth budget needs extra reach.
    internal static float DepthWeight(Item item, WeaponMode mode) => mode == WeaponMode.Melee && Effect(item) == SwordEffect.None
        ? CombatRules.MeleeDepthWeight : 1;
}
