using System.Collections;
using System.Reflection;
using System.Reflection.Emit;

internal static class KingSlimeBoatChecks
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    internal static void Run(Assembly game, Assembly code, Func<object> createBoat)
    {
        Type main = game.GetType("Terraria.Main")!, npcType = game.GetType("Terraria.NPC")!;
        Type trial = code.GetType("DimensionalTerraria.Systems.KingSlimeBoatTrial")!;
        Type stateType = code.GetType("DimensionalTerraria.Systems.BoatCaptive")!;
        Type damageType = code.GetType("DimensionalTerraria.Systems.BoatDamageSystem")!;
        Array npcs = (Array)main.GetField("npc")!.GetValue(null)!;
        Array savedNpcs = (Array)npcs.Clone();
        object player = ((Array)main.GetField("player")!.GetValue(null)!).GetValue(0)!;
        Type pt = player.GetType(), vector = pt.GetField("position")!.FieldType;
        object Vec(float x, float y) => Activator.CreateInstance(vector, new object[] { x, y })!;
        object center = Vec(488, 779);
        object savedPosition = pt.GetField("position")!.GetValue(player)!;
        bool savedActive = (bool)pt.GetField("active")!.GetValue(player)!, savedDead = (bool)pt.GetField("dead")!.GetValue(player)!;
        bool savedGhost = (bool)pt.GetField("ghost")!.GetValue(player)!;
        FieldInfo mode = main.GetField("_currentGameModeInfo", All)!;
        object savedMode = mode.GetValue(null)!;
        var hooks = new List<(EventInfo Event, Delegate Callback)>();
        void SuppressVisual(string type, string name)
        {
            EventInfo e = Type.GetType(type + ", TerrariaHooks", true)!.GetEvent(name)!;
            MethodInfo invoke = e.EventHandlerType!.GetMethod("Invoke")!;
            var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("KingVisual" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
            var shim = assembly.DefineDynamicModule("Shim").DefineType("Shim", TypeAttributes.Public);
            var method = shim.DefineMethod("Skip", MethodAttributes.Public | MethodAttributes.Static, invoke.ReturnType,
                invoke.GetParameters().Select(p => p.ParameterType).ToArray());
            var il = method.GetILGenerator();
            if (invoke.ReturnType != typeof(void)) il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ret);
            Delegate callback = shim.CreateType()!.GetMethod("Skip")!.CreateDelegate(e.EventHandlerType);
            e.AddEventHandler(null, callback); hooks.Add((e, callback));
        }
        object? Call(object target, string name, params object?[] args) => target.GetType().GetMethod(name, All)!.Invoke(target, args);
        int Int(object npc, string field) => (int)npcType.GetField(field)!.GetValue(npc)!;
        float[] Ai(object npc) => (float[])npcType.GetField("ai")!.GetValue(npc)!;
        object State(object npc) => ((Array)npcType.GetField("_globals", All)!.GetValue(npc)!).Cast<object>().Single(g => g?.GetType() == stateType);
        bool Eligible(object npc) => (bool)stateType.GetMethod("Eligible", All)!.Invoke(null, new[] { npc })!;
        bool Participant(object npc) => (bool)trial.GetMethod("IsParticipant", All)!.Invoke(null, new[] { npc })!;
        MethodInfo defaults = npcType.GetMethods().Single(m => m.Name == "SetDefaults" && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(int));
        void Defaults(object npc) => defaults.Invoke(npc, new[] { (object)50, Activator.CreateInstance(defaults.GetParameters()[1].ParameterType)! });
        MethodInfo vanilla = npcType.GetMethod("VanillaAI")!;
        object damageSystem = Activator.CreateInstance(damageType)!;
        bool loaded = false;
        try
        {
            SuppressVisual("Terraria.On_Main", "NewText_string_byte_byte_byte");
            SuppressVisual("Terraria.On_CombatText", "NewText_Rectangle_Color_int_bool_bool");
            SuppressVisual("Terraria.On_NPC", "HitEffect_HitInfo");
            // Native NewNPC formats its boss announcement before calling NewText.
            Array npcNames = (Array)game.GetType("Terraria.Lang")!.GetField("_npcNameCache", All)!.GetValue(null)!;
            npcNames.SetValue(Activator.CreateInstance(game.GetType("Terraria.Localization.LocalizedText")!, All, null,
                new object[] { "NPCName.KingSlime", "King Slime" }, null), 50);
            Array dust = (Array)main.GetField("dust")!.GetValue(null)!;
            for (int i = 0; i < dust.Length; i++)
                if (dust.GetValue(i) is null) dust.SetValue(Activator.CreateInstance(dust.GetType().GetElementType()!), i);
            main.GetProperty("rand")!.SetValue(null, Activator.CreateInstance(game.GetType("Terraria.Utilities.UnifiedRandom")!, new object[] { 1 }));
            Call(damageSystem, "Load"); loaded = true;
            pt.GetField("active")!.SetValue(player, true); pt.GetField("dead")!.SetValue(player, false);
            pt.GetField("ghost")!.SetValue(player, false); pt.GetProperty("Center")!.SetValue(player, center);
            object map = main.GetField("tile")!.GetValue(null)!;
            Type tile = game.GetType("Terraria.Tile")!;
            for (int x = 20; x <= 42; x++)
                tile.GetMethod("ResetToType")!.Invoke(map.GetType().GetProperty("Item", new[] { typeof(int), typeof(int) })!.GetValue(map, new object[] { x, 50 }), new object[] { (ushort)1 });

            foreach (string difficulty in new[] { "NormalMode", "ExpertMode", "MasterMode" })
            {
                mode.SetValue(null, game.GetType("Terraria.DataStructures.GameModeData")!.GetField(difficulty)!.GetValue(null));
                for (int i = 0; i < npcs.Length; i++) npcs.SetValue(Activator.CreateInstance(npcType), i);
                string result = (string)trial.GetMethod("Spawn", All)!.Invoke(null, new object?[] { center, null })!;
                if (result != "KingSlimeTrialSpawned") throw new InvalidOperationException("Actual trial spawner failed: " + result);
                object king = npcs.Cast<object>().Single(n => (bool)npcType.GetField("active")!.GetValue(n)!);
                if (Int(king, "type") != 50 || !Participant(king) || Eligible(king))
                    throw new InvalidOperationException("Only the spawned King Slime is marked; capture must wait for native size initialization.");
                vanilla.Invoke(king, null);
                if (!Eligible(king) || Int(king, "width") < 120 || Int(king, "height") < 110)
                    throw new InvalidOperationException("A real initialized King Slime with native full size must pass the trial gate.");
                object natural = Activator.CreateInstance(npcType)!; Defaults(natural);
                npcType.GetField("active")!.SetValue(natural, true);
                npcType.GetField("target")!.SetValue(natural, 0);
                npcType.GetField("position")!.SetValue(natural, npcType.GetField("position")!.GetValue(king));
                ((float[])npcType.GetField("localAI")!.GetValue(natural)!)[3] = 1;
                if (!Participant(natural) || !Eligible(natural)) throw new InvalidOperationException("Natural/summoned King Slimes must now share the formal ground-boss capture rules.");
                Ai(king)[1] = 5; if (Eligible(king)) throw new InvalidOperationException("Do not freeze the teleport-out animation.");
                Ai(king)[1] = 6; if (Eligible(king)) throw new InvalidOperationException("Do not freeze the teleport-in animation.");
                Ai(king)[1] = 0;
                object boat = createBoat();
                if (!(bool)Call(boat, "TryCapture", king)! || !(bool)code.GetType("DimensionalTerraria.Systems.BoatCaptureSystem")!.GetMethod("HoldAI", All)!.Invoke(null, new[] { king })!)
                    throw new InvalidOperationException("The real trial King Slime must board and have its native jumping/teleport AI held.");
                pt.GetField("ghost")!.SetValue(player, true);
                if (!Eligible(king)) throw new InvalidOperationException("A living detached 3D player must not be treated as dead.");
                pt.GetField("ghost")!.SetValue(player, false);
                int life = Int(king, "life"), points = life / 5;
                float threshold = Ai(king)[3];
                Type hitType = game.GetType("Terraria.NPC+HitInfo")!;
                object hit = Array.CreateInstance(hitType, 1).GetValue(0)!;
                hitType.GetProperty("Damage")!.SetValue(hit, points);
                hitType.GetField("HideCombatText")!.SetValue(hit, true);
                npcType.GetMethod("StrikeNPC", new[] { hitType, typeof(bool), typeof(bool) })!.Invoke(king, new object[] { hit, false, true });
                if (Int(king, "life") != life - points || boat.GetType().GetField("Occupant", All)!.GetValue(boat) is not null ||
                    (bool)boat.GetType().GetProperty("Live", All)!.GetValue(boat)! || Ai(king)[3] != threshold || Ai(king)[1] != 0)
                    throw new InvalidOperationException("A native boss hit must break the boat once, retain damage and reset jump/teleport state without discarding slime-spawn thresholds.");
                int oldWidth = Int(king, "width"); vanilla.Invoke(king, null);
                if (Int(king, "width") >= oldWidth || Int(king, "life") != life - points ||
                    npcs.Cast<object>().Count(n => (bool)npcType.GetField("active")!.GetValue(n)!) < 2)
                    throw new InvalidOperationException("Released native King Slime must shrink and spawn slimes normally after crossing a damage threshold.");
                Console.WriteLine("PASS real King Slime " + difficulty + ": natural and debug bosses eligible, full-size boarding, teleport guards, 3D presence, pre-hit break and native shrink/slime spawning");

                object nextBoat = createBoat(), state = State(king);
                if ((bool)Call(nextBoat, "TryCapture", king)!) throw new InvalidOperationException("A just-released boss needs the normal recapture cooldown.");
                stateType.GetField("Cooldown", All)!.SetValue(state, 0);
                if (!(bool)Call(nextBoat, "TryCapture", king)!) throw new InvalidOperationException("Post-hit recapture setup failed.");
                pt.GetField("dead")!.SetValue(player, true); Call(nextBoat, "Reconcile");
                if (nextBoat.GetType().GetField("Occupant", All)!.GetValue(nextBoat) is not null) throw new InvalidOperationException("Real player death must release the boss for native retreat.");
                pt.GetField("dead")!.SetValue(player, false); stateType.GetField("Cooldown", All)!.SetValue(state, 0);
                if (!(bool)Call(nextBoat, "TryCapture", king)!) throw new InvalidOperationException("Distance-release setup failed.");
                pt.GetProperty("Center")!.SetValue(player, Vec(5000, 779)); Call(nextBoat, "Reconcile");
                if (nextBoat.GetType().GetField("Occupant", All)!.GetValue(nextBoat) is not null) throw new InvalidOperationException("Leaving the native 3000px engagement range in 2D must release the trial boss.");
                pt.GetProperty("Center")!.SetValue(player, center);
                Call(nextBoat, "Break"); Defaults(king);
                if (stateType.GetField("Host", All)!.GetValue(State(king)) is not null || (int)stateType.GetField("Cooldown", All)!.GetValue(State(king))! != 0)
                    throw new InvalidOperationException("NPC defaults/slot reuse must clear capture links and cooldown.");
            }
            Console.WriteLine("PASS King Slime lifecycle: release cooldown, real player death, 2D departure and NPC reset do not leave a permanently captive boss");
            GroundBossChecks.Run(game, code, createBoat);
        }
        finally
        {
            if (loaded) Call(damageSystem, "Unload");
            foreach (var hook in hooks.AsEnumerable().Reverse()) hook.Event.RemoveEventHandler(null, hook.Callback);
            Array.Copy(savedNpcs, npcs, npcs.Length); mode.SetValue(null, savedMode);
            pt.GetField("active")!.SetValue(player, savedActive); pt.GetField("dead")!.SetValue(player, savedDead);
            pt.GetField("ghost")!.SetValue(player, savedGhost); pt.GetField("position")!.SetValue(player, savedPosition);
        }
    }
}
