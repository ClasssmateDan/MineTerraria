using System.Collections;
using System.Reflection;
using System.Reflection.Emit;

internal static class BoatDamageChecks
{
    const BindingFlags Hidden = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    internal static void Run(Assembly game, Assembly code, Func<(object Boat, object Npc, int Id)> create)
    {
        Type npcType = game.GetType("Terraria.NPC")!;
        Type damage = code.GetType("DimensionalTerraria.Systems.BoatDamageSystem")!;
        object system = Activator.CreateInstance(damage)!;
        var shims = new List<(EventInfo Event, Delegate Hook)>();
        void SuppressVisual(string type, string name)
        {
            EventInfo e = Type.GetType(type + ", TerrariaHooks", throwOnError: true)!.GetEvent(name)!;
            MethodInfo invoke = e.EventHandlerType!.GetMethod("Invoke")!;
            var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("VisualShim" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
            var typeBuilder = assembly.DefineDynamicModule("Shim").DefineType("Shim", TypeAttributes.Public);
            var method = typeBuilder.DefineMethod("NoVisual", MethodAttributes.Public | MethodAttributes.Static, invoke.ReturnType,
                invoke.GetParameters().Select(p => p.ParameterType).ToArray());
            var il = method.GetILGenerator();
            if (invoke.ReturnType != typeof(void)) il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ret);
            Delegate callback = typeBuilder.CreateType()!.GetMethod("NoVisual")!.CreateDelegate(e.EventHandlerType);
            e.AddEventHandler(null, callback); shims.Add((e, callback));
        }
        IDictionary entities = (IDictionary)game.GetType("Terraria.DataStructures.TileEntity")!.GetField("ByID")!.GetValue(null)!;
        int Life(object npc) => (int)npcType.GetField("life")!.GetValue(npc)!;
        void Int(object npc, string field, int value) => npcType.GetField(field)!.SetValue(npc, value);
        bool Free(object boat, int id) => !entities.Contains(id) && boat.GetType().GetField("Occupant", Hidden)!.GetValue(boat) is null;
        void Break(object boat) => boat.GetType().GetMethod("Break", Hidden)!.Invoke(boat, null);
        Type hitType = game.GetType("Terraria.NPC+HitInfo")!;
        object Hit(int points)
        {
            object hit = Array.CreateInstance(hitType, 1).GetValue(0)!;
            if (points > 0) hitType.GetProperty("Damage")!.SetValue(hit, points);
            hitType.GetField("HitDirection")!.SetValue(hit, 1);
            hitType.GetField("Knockback")!.SetValue(hit, 4f);
            hitType.GetField("HideCombatText")!.SetValue(hit, true);
            return hit;
        }
        MethodInfo strike = npcType.GetMethod("StrikeNPC", new[] { hitType, typeof(bool), typeof(bool) })!;
        MethodInfo dot = npcType.GetMethod("UpdateNPC_BuffApplyDOTs", Hidden)!;
        (object Boat, object Npc, int Id) CreateBoss()
        {
            // Exercise the future captured-boss damage policy without opening
            // the production eligibility gate: promote an already seated NPC.
            var entry = create(); npcType.GetField("boss")!.SetValue(entry.Npc, true); return entry;
        }
        bool loaded = false;
        try
        {
            // Only bypass font/dust rendering in this headless process. Native
            // damage arithmetic, regen counters, knockback and tile drops run.
            SuppressVisual("Terraria.On_CombatText", "NewText_Rectangle_Color_int_bool_bool");
            SuppressVisual("Terraria.On_NPC", "HitEffect_HitInfo");
            system.GetType().GetMethod("Load")!.Invoke(system, null); loaded = true;
            var ordinary = create();
            strike.Invoke(ordinary.Npc, new object[] { Hit(7), false, true });
            if (Free(ordinary.Boat, ordinary.Id) || Life(ordinary.Npc) != 43)
                throw new InvalidOperationException("Ordinary passengers must retain their boat when hurt.");
            Int(ordinary.Npc, "lifeRegen", -240); dot.Invoke(ordinary.Npc, null);
            if (Free(ordinary.Boat, ordinary.Id) || Life(ordinary.Npc) != 41)
                throw new InvalidOperationException("Ordinary passengers must retain their boat during DOT as well.");
            damage.GetMethod("ReleaseBeforeDamage", Hidden)!.Invoke(null, new[] { ordinary.Npc });
            if (Free(ordinary.Boat, ordinary.Id)) throw new InvalidOperationException("The 3D pre-hit helper must leave ordinary passengers seated.");
            Break(ordinary.Boat);
            Console.WriteLine("PASS ordinary passenger regression: direct hits, DOT and the 3D pre-hit helper do not break its boat");

            var direct = CreateBoss();
            int result = (int)strike.Invoke(direct.Npc, new object[] { Hit(7), false, true })!;
            if (!Free(direct.Boat, direct.Id) || Life(direct.Npc) != 43 || result != 7)
                throw new InvalidOperationException("One real native hit must release the passenger and apply exactly seven HP damage once.");
            object speed = npcType.GetField("velocity")!.GetValue(direct.Npc)!;
            if ((float)speed.GetType().GetField("X")!.GetValue(speed)! == 0)
                throw new InvalidOperationException("Boat release must happen before native knockback, without clearing the resulting impulse.");
            Console.WriteLine("PASS boss-only native hit: boat removed, exactly one damage application, native knockback retained");

            var blank = CreateBoss();
            strike.Invoke(blank.Npc, new object[] { Hit(0), false, true });
            if (Free(blank.Boat, blank.Id) || Life(blank.Npc) != 50) throw new InvalidOperationException("A zero-damage strike cannot break the passenger's boat.");
            Break(blank.Boat);
            Console.WriteLine("PASS zero-damage strike: no lost HP and no passenger-triggered boat break");

            var immune = CreateBoss(); npcType.GetField("immortal")!.SetValue(immune.Npc, true);
            strike.Invoke(immune.Npc, new object[] { Hit(7), false, true });
            if (Free(immune.Boat, immune.Id) || Life(immune.Npc) != 50) throw new InvalidOperationException("A strike that cannot reduce immortal HP must not trigger a passenger-damage break.");
            npcType.GetField("immortal")!.SetValue(immune.Npc, false); Break(immune.Boat);
            Console.WriteLine("PASS invulnerable damage attempt: native HP protection does not trigger the damage-release path");

            var poison = CreateBoss();
            npcType.GetField("poisoned")!.SetValue(poison.Npc, true);
            Int(poison.Npc, "lifeRegenCount", -96);
            dot.Invoke(poison.Npc, null);
            if (Free(poison.Boat, poison.Id) || Life(poison.Npc) != 50) throw new InvalidOperationException("A pending poison tick must not break the boat before damage is due.");
            Int(poison.Npc, "lifeRegen", 0); // Native ResetEffects clears this for the next tick.
            dot.Invoke(poison.Npc, null);
            if (!Free(poison.Boat, poison.Id) || Life(poison.Npc) != 49 || (int)npcType.GetField("lifeRegenCount")!.GetValue(poison.Npc)! != 0)
                throw new InvalidOperationException("The due poison tick must break the boat and preserve its one HP loss and regen accumulator.");
            Console.WriteLine("PASS native poison: ship retained before the tick, released on the first actual HP loss without changing damage");

            var fast = CreateBoss(); Int(fast.Npc, "lifeRegen", -240);
            dot.Invoke(fast.Npc, null);
            if (!Free(fast.Boat, fast.Id) || Life(fast.Npc) != 48) throw new InvalidOperationException("Batched DOT must retain its original two-HP amount.");
            Console.WriteLine("PASS native batched DOT: the second life-write path releases and preserves original HP loss");

            var preview = CreateBoss();
            npcType.GetMethod("CalculateHitInfo")!.Invoke(preview.Npc, new object?[] { 7, 1, false, 0f, null, false, 0f });
            if (Free(preview.Boat, preview.Id) || Life(preview.Npc) != 50) throw new InvalidOperationException("Calculating damage alone is not an attack and must not break a boat.");
            damage.GetMethod("ReleaseBeforeDamage", Hidden)!.Invoke(null, new[] { preview.Npc });
            damage.GetMethod("ReleaseBeforeDamage", Hidden)!.Invoke(null, new[] { preview.Npc });
            if (!Free(preview.Boat, preview.Id) || Life(preview.Npc) != 50) throw new InvalidOperationException("Pre-damage release must be idempotent and cannot change passenger health itself.");
            Console.WriteLine("PASS pre-damage release: damage previews harmless, repeated release safe, helper never deducts HP");
        }
        finally
        {
            if (loaded) system.GetType().GetMethod("Unload")!.Invoke(system, null);
            foreach (var shim in shims.AsEnumerable().Reverse()) shim.Event.RemoveEventHandler(null, shim.Hook);
        }
    }
}
