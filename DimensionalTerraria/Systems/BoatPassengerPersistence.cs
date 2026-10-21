using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace DimensionalTerraria.Systems;

public sealed partial class BoatCaptive
{
    internal Point16? PendingBoat;

    private Point16? SaveAnchor(NPC npc)
    {
        if (!npc.active || npc.life <= 0) return null;
        if (Host is { } boat && boat.Live && ReferenceEquals(boat.Occupant, npc) &&
            npc.whoAmI >= 0 && npc.whoAmI < Main.maxNPCs && ReferenceEquals(Main.npc[npc.whoAmI], npc)) return boat.Position;
        return PendingBoat;
    }

    public override bool NeedSaving(NPC npc) => SaveAnchor(npc).HasValue;

    public override void SaveData(NPC npc, TagCompound tag)
    {
        if (SaveAnchor(npc) is not { } anchor) return;
        tag["Version"] = 1; tag["BoatX"] = (int)anchor.X; tag["BoatY"] = (int)anchor.Y;
        tag["Life"] = npc.life; tag["LifeMax"] = npc.lifeMax;
        tag["Width"] = npc.width; tag["Height"] = npc.height; tag["Scale"] = npc.scale;
        tag["AI"] = npc.ai.ToList(); tag["LocalAI"] = npc.localAI.ToList();
        // WorldIO already saves NPC identity by stable vanilla/mod names. NetID
        // and appearance/stats additionally preserve vanilla negative-ID variants.
        if (npc.ModNPC is null) tag["NetID"] = npc.netID;
        tag["Damage"] = npc.damage; tag["DefDamage"] = npc.defDamage;
        tag["Defense"] = npc.defense; tag["DefDefense"] = npc.defDefense;
        tag["Value"] = npc.value; tag["Alpha"] = npc.alpha;
        tag["Tint"] = unchecked((int)npc.color.PackedValue);
        tag["Direction"] = npc.direction; tag["SpriteDirection"] = npc.spriteDirection;
        tag["NoGravity"] = npc.noGravity; tag["NoTileCollide"] = npc.noTileCollide;
        tag["TimeLeft"] = npc.timeLeft; tag["Name"] = npc.GivenName;
        tag["ReleaseX"] = ReleaseVelocity.X; tag["ReleaseY"] = ReleaseVelocity.Y;
        List<TagCompound> buffs = new();
        for (int i = 0; i < npc.buffType.Length; i++)
        {
            int type = npc.buffType[i];
            if (type <= 0 || type >= BuffLoader.BuffCount || npc.buffTime[i] <= 0) continue;
            TagCompound buff = new() { ["Time"] = npc.buffTime[i] };
            if (type < BuffID.Count) buff["Type"] = type;
            else if (ModContent.GetModBuff(type) is { } modBuff) buff["ModBuff"] = modBuff.FullName;
            else continue;
            buffs.Add(buff);
        }
        tag["Buffs"] = buffs;
    }

    public override void LoadData(NPC npc, TagCompound tag)
    {
        if (tag.GetInt("Version") != 1) return;
        int x = tag.GetInt("BoatX"), y = tag.GetInt("BoatY");
        if (!WorldGen.InWorld(x, y, 10) || !WorldGen.InWorld(x + 2, y + 1, 10)) return;
        npc.lifeMax = Math.Max(1, tag.GetInt("LifeMax"));
        npc.life = Math.Clamp(tag.GetInt("Life"), 1, npc.lifeMax);
        npc.width = Math.Clamp(tag.GetInt("Width"), 1, Main.maxTilesX * 16);
        npc.height = Math.Clamp(tag.GetInt("Height"), 1, Main.maxTilesY * 16);
        float scale = tag.GetFloat("Scale");
        if (float.IsFinite(scale) && scale > 0) npc.scale = scale;
        RestoreAI(npc.ai, tag.GetList<float>("AI")); RestoreAI(npc.localAI, tag.GetList<float>("LocalAI"));
        // Do not call SetDefaults here: it would erase earlier GlobalNPC loads.
        if (npc.ModNPC is null && tag.ContainsKey("NetID")) npc.netID = tag.GetInt("NetID");
        npc.damage = tag.GetInt("Damage"); npc.defDamage = tag.GetInt("DefDamage");
        npc.defense = tag.GetInt("Defense"); npc.defDefense = tag.GetInt("DefDefense");
        float value = tag.GetFloat("Value");
        if (float.IsFinite(value)) npc.value = value;
        npc.alpha = Math.Clamp(tag.GetInt("Alpha"), 0, 255);
        npc.color = new Color { PackedValue = unchecked((uint)tag.GetInt("Tint")) };
        npc.direction = tag.GetInt("Direction") < 0 ? -1 : 1;
        npc.spriteDirection = tag.GetInt("SpriteDirection") < 0 ? -1 : 1;
        npc.noGravity = tag.GetBool("NoGravity"); npc.noTileCollide = tag.GetBool("NoTileCollide");
        npc.timeLeft = Math.Max(11, tag.GetInt("TimeLeft")); npc.GivenName = tag.GetString("Name");
        float vx = tag.GetFloat("ReleaseX"), vy = tag.GetFloat("ReleaseY");
        ReleaseVelocity = float.IsFinite(vx) && float.IsFinite(vy) ? new(vx, vy) : Vector2.Zero;
        Array.Clear(npc.buffType); Array.Clear(npc.buffTime);
        int slot = 0;
        foreach (TagCompound buff in tag.GetList<TagCompound>("Buffs"))
        {
            if (slot >= npc.buffType.Length) break;
            int type = buff.GetInt("Type"), time = buff.GetInt("Time");
            if (buff.ContainsKey("ModBuff")) type = ModContent.TryFind<ModBuff>(buff.GetString("ModBuff"), out var modBuff) ? modBuff.Type : 0;
            else if (type >= BuffID.Count) continue;
            if (type <= 0 || type >= BuffLoader.BuffCount || time <= 0) continue;
            npc.buffType[slot] = type; npc.buffTime[slot++] = time;
        }
        Host = null; Cooldown = 0; PendingBoat = new(x, y);
    }

    private static void RestoreAI(float[] target, IList<float> saved)
    {
        for (int i = 0; i < target.Length; i++) target[i] = i < saved.Count && float.IsFinite(saved[i]) ? saved[i] : 0;
    }
}
