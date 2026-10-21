using System;
using DimensionalTerraria.Core;
using DimensionalTerraria.Systems;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace DimensionalTerraria.World;

internal readonly record struct AttackStats(Item Weapon, int Damage, float Knockback, int Cooldown);

internal static class NativeCombat
{
    internal static bool IsDrill(Item item) => !item.IsAir && item.channel && item.damage > 0 &&
        (ItemID.Sets.IsDrill[item.type] || ItemID.Sets.IsChainsaw[item.type] ||
            item.pick > 0 && item.shoot > ProjectileID.None && item.DamageType.CountsAsClass(DamageClass.Melee));
    public static WeaponMode Mode(Item item)
    {
        if (IsDrill(item)) return WeaponMode.Melee;
        if (item.IsAir || item.damage <= 0 || item.channel || item.consumable || item.DamageType.CountsAsClass(DamageClass.Summon)) return WeaponMode.None;
        if (item.useAmmo > AmmoID.None || item.DamageType.CountsAsClass(DamageClass.Ranged) && item.shoot > ProjectileID.None) return WeaponMode.Ranged;
        if (item.DamageType.CountsAsClass(DamageClass.Magic) && item.shoot > ProjectileID.None) return WeaponMode.Magic;
        // Shortswords and spears use projectiles for their native thrust, but still
        // need to work as close-range weapons in this aimed-attack prototype.
        if (item.DamageType.CountsAsClass(DamageClass.Melee)) return WeaponMode.Melee;
        return !item.noMelee ? WeaponMode.Melee : WeaponMode.None;
    }

    public static bool Eligible(Player player, NPC npc) => npc.active && !npc.friendly && !npc.townNPC && !npc.dontTakeDamage &&
        npc.life > 0 && !npc.hide && player.CanNPCBeHitByPlayerOrPlayerProjectile(npc);

    public static bool Prepare(Player player, WeaponMode mode, out AttackStats stats, out string? failure)
    {
        Item weapon = player.HeldItem.Clone();
        stats = default;
        failure = null;
        if (mode == WeaponMode.None || player.CCed || player.noItems) { failure = "CannotAttack"; return false; }
        if (NativeSwordEffects.Effect(weapon) != SwordEffect.None && !CombinedHooks.CanShoot(player, weapon))
        { failure = "CannotAttack"; return false; }
        if (weapon.mana > 0 && !player.CheckMana(weapon, pay: false, blockQuickMana: true)) { failure = "NoMana"; return false; }
        int ticks = Math.Max(1, CombinedHooks.TotalUseTime(weapon.useTime, player, weapon));
        if (IsDrill(weapon)) ticks = Math.Clamp(ticks, 3, 6);
        int damage = player.GetWeaponDamage(weapon);
        float knockback = player.GetWeaponKnockback(weapon);
        if (weapon.useAmmo > AmmoID.None && !PickAmmo(player, weapon, ticks, out damage, out knockback))
        { failure = "NoAmmo"; return false; }
        if (weapon.mana > 0)
        {
            if (!player.CheckMana(weapon, pay: true, blockQuickMana: true)) { failure = "NoMana"; return false; }
            player.manaRegenDelay = Math.Max(player.manaRegenDelay, player.maxRegenDelay);
        }
        stats = new AttackStats(weapon, damage, knockback, ticks);
        if (weapon.UseSound is { } sound) SoundEngine.PlaySound(sound);
        return true;
    }

    private static bool PickAmmo(Player player, Item weapon, int ticks, out int damage, out float knockback)
    {
        int animation = player.itemAnimation, maximum = player.itemAnimationMax;
        try
        {
            // Each aimed attack is one complete use. Native ammo conservation
            // checks the first/last animation frame; leaving the detached body's
            // animation at zero would grant infinite ammo to first-shot weapons.
            player.itemAnimation = player.itemAnimationMax = ticks;
            return player.PickAmmo(weapon, out _, out _, out damage, out knockback, out _);
        }
        finally
        {
            player.itemAnimation = animation;
            player.itemAnimationMax = maximum;
        }
    }

    public static bool Strike(Player player, NPC npc, AttackStats attack, float observerX, out NPC.HitInfo outcome)
    {
        outcome = default;
        if (!Eligible(player, npc) || npc.immune[player.whoAmI] > 0 || CombinedHooks.CanPlayerHitNPCWithItem(player, attack.Weapon, npc) == false) return false;
        // This is a confirmed aimed attack, not a damage preview. Break the
        // boss passenger's boat before our 3D hit modifiers and damage are calculated.
        if (attack.Damage > 0) BoatDamageSystem.ReleaseBeforeDamage(npc);
        int direction = npc.Center.X >= observerX * 16 ? 1 : -1;
        bool crit = attack.Weapon.DamageType.UseStandardCritCalcs && Main.rand.Next(100) < player.GetWeaponCrit(attack.Weapon);
        NPC.HitModifiers modifiers = npc.GetIncomingStrikeModifiers(attack.Weapon.DamageType, direction);
        player.ApplyBannerOffenseBuff(npc, ref modifiers);
        modifiers.ArmorPenetration += player.GetWeaponArmorPenetration(attack.Weapon);
        CombinedHooks.ModifyPlayerHitNPCWithItem(player, attack.Weapon, npc, ref modifiers);
        NPC.HitInfo hit = modifiers.ToHitInfo(attack.Damage, crit, attack.Knockback, damageVariation: true, player.luck);
        outcome = hit;
        NPCKillAttempt attempt = new(npc);
        player.StatusToNPC(attack.Weapon.type, npc.whoAmI);
        if (npc.life > 5) player.OnHit(npc.Center.X, npc.Center.Y, npc);
        int done = npc.StrikeNPC(hit);
        CombinedHooks.OnPlayerHitNPCWithItem(player, attack.Weapon, npc, in hit, done);
        npc.immune[player.whoAmI] = attack.Cooldown;
        if (attempt.DidNPCDie()) player.OnKillNPC(ref attempt, attack.Weapon);
        return done > 0;
    }
}
