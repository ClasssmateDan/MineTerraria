using System.Collections;
using System.Reflection;

internal static class VillagerChecks
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    internal static void Run(Assembly game, Assembly code, object villagerTemplate, object golemTemplate, object boatTile, object boatTemplate, object slimeEgg)
    {
        Type main = game.GetType("Terraria.Main")!, npcType = game.GetType("Terraria.NPC")!;
        Type npcLoader = game.GetType("Terraria.ModLoader.NPCLoader")!, tile = game.GetType("Terraria.Tile")!;
        object? Call(object obj, string method, params object?[] values) => obj.GetType().GetMethod(method, All)!.Invoke(obj, values);
        object? Get(object obj, string field) => obj.GetType().GetField(field, All)!.GetValue(obj);
        void Set(object obj, string field, object value) => obj.GetType().GetField(field, All)!.SetValue(obj, value);
        int Id(object content) => Convert.ToInt32(content.GetType().GetProperty("Type")!.GetValue(content));
        object Vector(float x, float y) => Activator.CreateInstance(npcType.GetField("position")!.FieldType, new object[] { x, y })!;
        // Boat fixtures already finalized global slots. Resize the NPC tables
        // without requiring the desktop's shop/personality initialization.
        game.GetType("Terraria.ModLoader.Core.LoaderUtils")!.GetMethod("ResetStaticMembers", All)!.Invoke(null, new object[] { game.GetType("Terraria.ID.NPCID")!, true });
        int count = (int)npcLoader.GetProperty("NPCCount")!.GetValue(null)!;
        foreach (var pair in new[] { (main, "npcFrameCount"), (npcType, "killCount"), (npcType, "ShimmeredTownNPCs"), (main, "npcCatchable") })
        {
            FieldInfo field = pair.Item1.GetField(pair.Item2)!;
            Array old = (Array)field.GetValue(null)!;
            Array enlarged = Array.CreateInstance(old.GetType().GetElementType()!, count);
            Array.Copy(old, enlarged, old.Length); field.SetValue(null, enlarged);
        }
        Call(villagerTemplate, "SetStaticDefaults"); Call(golemTemplate, "SetStaticDefaults");
        Array npcs = (Array)main.GetField("npc")!.GetValue(null)!;
        object map = main.GetField("tile")!.GetValue(null)!;
        object Cell(int x, int y) => map.GetType().GetProperty("Item", new[] { typeof(int), typeof(int) })!.GetValue(map, new object[] { x, y })!;
        object system = Activator.CreateInstance(code.GetType("DimensionalTerraria.Systems.VillagerDefenseSystem")!)!;
        main.GetField("gameMenu")!.SetValue(null, false);
        main.GetField("netMode")!.SetValue(null, 0);
        main.GetField("dedServ")!.SetValue(null, true);
        void Reset(bool floor = true)
        {
            Call(system, "OnWorldUnload");
            map.GetType().GetMethod("ClearEverything")!.Invoke(map, null);
            for (int i = 0; i < npcs.Length; i++) { object npc = Activator.CreateInstance(npcType)!; Set(npc, "whoAmI", i); npcs.SetValue(npc, i); }
            ((IDictionary)game.GetType("Terraria.DataStructures.TileEntity")!.GetField("ByID")!.GetValue(null)!).Clear();
            ((IDictionary)game.GetType("Terraria.DataStructures.TileEntity")!.GetField("ByPosition")!.GetValue(null)!).Clear();
            if (floor) for (int x = 12; x < 100; x++) tile.GetMethod("ResetToType")!.Invoke(Cell(x, 60), new object[] { (ushort)1 });
            ((bool[])main.GetField("tileSolid")!.GetValue(null)!)[1] = true;
        }
        object Npc(int slot, int type, float x, float bottom = 60 * 16)
        {
            object npc = npcs.GetValue(slot)!;
            npcType.GetMethod("SetDefaults", new[] { typeof(int), game.GetType("Terraria.NPCSpawnParams")! })!
                .Invoke(npc, new[] { (object)type, Activator.CreateInstance(game.GetType("Terraria.NPCSpawnParams")!) });
            npcType.GetProperty("Bottom")!.SetValue(npc, Vector(x, bottom));
            Set(npc, "active", true); return npc;
        }
        object Villager(int slot, float x) => Npc(slot, Id(villagerTemplate), x);
        object ModNpc(object npc) => npcType.GetProperty("ModNPC")!.GetValue(npc)!;
        object Memory(object npc) => Get(ModNpc(npc), "Memory")!;
        void Tick(int frames) { for (int i = 0; i < frames; i++) Call(system, "PostUpdateNPCs"); }
        int Golems() => npcs.Cast<object>().Count(n => (bool)Get(n, "active")! && (int)Get(n, "type")! == Id(golemTemplate));
        object Enemy(int slot, float x)
        {
            object npc = npcs.GetValue(slot)!;
            Set(npc, "active", true); Set(npc, "life", 100); Set(npc, "lifeMax", 100); Set(npc, "damage", 12);
            Set(npc, "type", 1); Set(npc, "width", 24); Set(npc, "height", 24); Set(npc, "realLife", -1);
            npcType.GetProperty("Bottom")!.SetValue(npc, Vector(x, 60 * 16)); return npc;
        }
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        Reset();
        object a = Villager(0, 48 * 16), b = Villager(1, 49 * 16);
        object eggUser = Activator.CreateInstance(game.GetType("Terraria.Player")!)!;
        Call(slimeEgg, "SetDefaults");
        Check(!(bool)Call(slimeEgg, "TrySpawn", eggUser, 1, 1, null)! && !(bool)Call(slimeEgg, "ConsumeItem", eggUser)!, "Invalid ground must preserve the slime egg");
        Check((bool)Call(slimeEgg, "TrySpawn", eggUser, 51, 60, null)! && (bool)Call(slimeEgg, "ConsumeItem", eggUser)!, "Valid dry ground must spawn and consume one slime egg");
        int slimeType = Convert.ToInt32(game.GetType("Terraria.ID.NPCID")!.GetField("BlueSlime")!.GetValue(null));
        object enemy = npcs.Cast<object>().Single(n => (bool)Get(n, "active")! && (int)Get(n, "type")! == slimeType);
        Check((int)Get(enemy, "damage")! > 0 && (int)Get(enemy, "aiStyle")! == 1 && !(bool)Get(enemy, "friendly")!, "Egg must create a real hostile vanilla Blue Slime");
        Check((int)Get(enemy, "width")! == 24 && (int)Get(enemy, "height")! == 18, "Slime ground clearance must match the native body");
        Check((bool)code.GetType("DimensionalTerraria.Systems.CatalogItems")!.GetMethod("CanGrant", All)!.Invoke(null,
            new[] { slimeEgg.GetType().GetProperty("Mod")!.GetValue(slimeEgg), Id(slimeEgg), (object)false })!, "Slime egg must be available in the mod item catalog");
        Console.WriteLine("PASS native slime egg: real Blue Slime, native body/AI/hostility, success-only consumption and catalog eligibility");
        Check((int)Get(a, "damage")! == 0 && (int)Get(a, "lifeMax")! == 100 && !(bool)Get(a, "friendly")!, "Villager defaults must stay passive and damageable");
        object[] hitPlayer = { Activator.CreateInstance(game.GetType("Terraria.Player")!)!, 0 };
        Check(!(bool)Call(ModNpc(a), "CanHitPlayer", hitPlayer)! && !(bool)Call(ModNpc(a), "CanHitNPC", enemy)!, "Villagers must never attack either faction");
        Check(!(bool)code.GetType("DimensionalTerraria.Systems.DimensionSystem")!.GetMethod("IsHostile", All)!.Invoke(null, new[] { a })!, "Villagers must not enter detached pursuit");
        Console.WriteLine("PASS native villager: 100 HP, zero contact attacks against player/NPC, no detached pursuit");
        Tick(300); Check(Golems() == 0, "Two villagers cannot summon a guard");
        object c = Villager(3, 50 * 16); Tick(300);
        Check(Golems() == 1, "Three frightened villagers must summon exactly one guard, including simultaneous attempts");
        object guardian = npcs.Cast<object>().Single(n => (bool)Get(n, "active")! && (int)Get(n, "type")! == Id(golemTemplate));
        Check((bool)Get(ModNpc(guardian), "VillageGuardian")! && (bool)Call(ModNpc(guardian), "NeedSaving")!, "Summoned guardian must persist");
        Tick(2100); Check(Golems() == 1, "A nearby living guard must continue suppressing duplicates");
        Console.WriteLine("PASS native summoning: two rejected, three summon one, same-frame and nearby-guard suppression");
        Set(guardian, "active", false); Tick(1499); Check(Golems() == 0, "Recent-guard memory must survive guard removal");
        Tick(601); Check(Golems() == 1, "After memory expires, continuing panic may summon again");
        Console.WriteLine("PASS native replacement: removing the guard retains cooldown and permits later replacement");

        Reset(); a = Villager(0, 48 * 16); b = Villager(1, 49 * 16); c = Villager(2, 50 * 16); enemy = Enemy(10, 54 * 16);
        for (int y = 48; y < 60; y++) tile.GetMethod("ResetToType")!.Invoke(Cell(52, y), new object[] { (ushort)1 });
        Tick(600); Check(Golems() == 0, "An opaque wall must block threat perception");
        object hit = Activator.CreateInstance(game.GetType("Terraria.NPC+HitInfo")!)!;
        hit.GetType().GetProperty("Damage")!.SetValue(hit, 1); hit.GetType().GetField("HitDirection")!.SetValue(hit, 1);
        Call(ModNpc(a), "HitEffect", hit); Tick(1);
        Check((bool)Memory(a).GetType().GetProperty("Panicking")!.GetValue(Memory(a))!, "Actual damage must frighten a villager behind walls");
        Call(ModNpc(a), "AI");
        Check((float)Get(Get(a, "velocity")!, "X")! > 0, "Hurt villager must flee away from the incoming hit");
        Console.WriteLine("PASS native perception: terrain blocks sight; damage independently triggers flight without retaliation");

        Reset(false); a = Villager(0, 48 * 16); Villager(1, 49 * 16); Villager(2, 50 * 16); Enemy(10, 51 * 16);
        Tick(900); Check(Golems() == 0, "No valid floor must prevent autonomous spawns");
        Check((bool)Memory(a).GetType().GetProperty("Eligible")!.GetValue(Memory(a))!, "Failed spawn cannot consume guard cooldown");
        Console.WriteLine("PASS native spawn safety: no floor means no guard and no success cooldown");

        Reset(); a = Villager(0, 48 * 16); Villager(1, 49 * 16); Villager(2, 50 * 16); Enemy(10, 51 * 16);
        object manual = Npc(15, Id(golemTemplate), 48 * 16 + 256);
        Check(!(bool)Call(ModNpc(manual), "NeedSaving")!, "Manual guards must retain their existing save policy");
        Tick(600); Check(Golems() == 1, "Manual golem on the 16-tile boundary must block summoning");
        Console.WriteLine("PASS native existing-guard rule: manually spawned golem blocks summoning at the radius boundary");

        void VillageOnTestTerrain()
        {
            Reset(false); Villager(0, 48 * 16); Villager(1, 49 * 16); Villager(2, 50 * 16); Enemy(10, 51 * 16);
        }
        void Put(int x, int y) => tile.GetMethod("ResetToType")!.Invoke(Cell(x, y), new object[] { (ushort)1 });
        void Floor(int x) { for (int column = x - 1; column <= x + 1; column++) Put(column, 60); }
        object OnlyGuard() => npcs.Cast<object>().Single(n => (bool)Get(n, "active")! && (int)Get(n, "type")! == Id(golemTemplate));
        float GuardX() => (float)Get(npcType.GetProperty("Center")!.GetValue(OnlyGuard())!, "X")! / 16;
        VillageOnTestTerrain(); Put(48, 60); Tick(300); Check(Golems() == 0, "One support must fail");
        Put(47, 60); Tick(300); Check(Golems() == 0, "Two supports must fail");
        Put(49, 60); Tick(300); Check(Golems() == 1 && GuardX() == 48.5f, "Three supports must summon on the central column");
        Console.WriteLine("PASS native village footing: one/two blocks reject, three blocks summon exactly on the central column");
        VillageOnTestTerrain(); Floor(45); Floor(51); Tick(300);
        Check(Golems() == 1 && GuardX() == 45.5f, "Equal-distance floors must choose the left column first");
        VillageOnTestTerrain(); Floor(56); Tick(300);
        Check(Golems() == 1 && GuardX() == 56.5f, "Search must reach the final rightmost column without random omissions");
        Console.WriteLine("PASS native village search: equal-distance left priority and deterministic rightmost eight-tile coverage");

        Type liquidType = game.GetType("Terraria.LiquidData")!;
        Array liquids = (Array)map.GetType().GetMethod("GetData")!.MakeGenericMethod(liquidType).Invoke(map, null)!;
        int stride = (int)main.GetField("maxTilesY")!.GetValue(null)!;
        void Liquid(int x, int y, byte amount, byte kind = 0)
        {
            object data = liquids.GetValue(x * stride + y)!;
            liquidType.GetField("Amount")!.SetValue(data, amount);
            liquidType.GetField("typeAndFlags", All)!.SetValue(data, kind);
            liquids.SetValue(data, x * stride + y);
        }
        object collision = Activator.CreateInstance(code.GetType("DimensionalTerraria.World.TerrariaCollisionWorld")!)!;
        MethodInfo villagePosition = code.GetType("DimensionalTerraria.Core.VillageGolemSpawnRules")!.GetMethod("Position")!;
        bool Fits() => (bool)villagePosition.Invoke(null, new object?[] { collision, 48, 60, null })!;
        foreach ((byte amount, byte kind) in new (byte, byte)[] { (128, 0), (255, 0), (255, 1), (255, 2), (255, 3) })
        {
            VillageOnTestTerrain(); Floor(48);
            for (int column = 47; column <= 49; column++) Liquid(column, 59, amount, kind);
            Check(Fits(), "Native half/full-height liquid must be allowed for each liquid kind");
            Tick(300); Check(Golems() == 1 && GuardX() == 48.5f, "Actual village update must summon into shallow liquid");
        }
        Console.WriteLine("PASS native shallow-liquid village: half/full water and full lava/honey/shimmer use measured liquid depth and summon");
        VillageOnTestTerrain(); Floor(48);
        for (int column = 47; column <= 49; column++) Liquid(column, 59, 255);
        Liquid(49, 58, 1); Tick(300);
        Check(!Fits() && Golems() == 0, "One byte in the second layer on a side column must block spawning");
        Liquid(49, 58, 0); Tick(300); Check(Golems() == 1, "Clearing excess water must permit the next spawn attempt");
        Console.WriteLine("PASS native liquid-depth boundary: even one byte above the allowed depth blocks, clearing permits the next attempt");
        VillageOnTestTerrain(); Floor(48); Liquid(48, 59, 128); Put(48, 55); Tick(300);
        Check(!Fits() && Golems() == 0, "Shallow-liquid allowance must not disable headroom collision");
        Console.WriteLine("PASS native wet headroom: shallow liquid cannot allow a guard to intersect solid ceiling blocks");

        Type foreground = code.GetType("DimensionalTerraria.World.TerrariaPistonWorld+Foreground")!;
        void Surface(int x, bool half, int slope = 0, bool actuated = false, ushort type = 1)
        {
            object data = Activator.CreateInstance(foreground, All, null, new object[] { true, type, (short)0, (short)0, half,
                Enum.ToObject(game.GetType("Terraria.ID.SlopeType")!, slope), (byte)0, false, false, actuated, false }, null)!;
            foreground.GetMethod("Write", All)!.Invoke(data, new[] { Cell(x, 60) });
        }
        VillageOnTestTerrain();
        for (int column = 47; column <= 49; column++) { Surface(column, true); Liquid(column, 60, 255); Liquid(column, 59, 128); }
        Check(Fits(), "Native half blocks must measure liquid from their half-height feet");
        Liquid(49, 59, 129); Check(!Fits(), "Half-block liquid above one tile must fail");
        VillageOnTestTerrain(); Floor(48); Surface(49, true); Check(!Fits(), "Uneven native support surfaces must fail");
        Surface(49, false, slope: 1); Check(!Fits(), "Native slope cannot substitute for a flat support");
        Surface(49, false, actuated: true); Check(!Fits(), "Actuated block provides no support");
        ((bool[])main.GetField("tileSolidTop")!.GetValue(null)!)[19] = true;
        for (int column = 47; column <= 49; column++) Surface(column, false, type: 19);
        Check(Fits(), "Three level native platforms must remain valid supports");
        Console.WriteLine("PASS native support adapter: half blocks/liquid rounding, uneven surfaces, slopes, actuation and level platforms");

        Reset(); a = Villager(0, 45.5f * 16); Villager(1, 48 * 16); Villager(2, 50 * 16); enemy = Enemy(10, 54.5f * 16);
        object Boat(int left, object passenger)
        {
            int boatId = Id(boatTile);
            Check((bool)game.GetType("Terraria.WorldGen")!.GetMethod("PlaceObject")!.Invoke(null,
                new object[] { left + 1, 59, boatId, true, 0, 0, -1, -1 })!, "Boat fixture requires native placement");
            int id = (int)Call(boatTemplate, "Place", left, 58)!;
            object boat = ((IDictionary)game.GetType("Terraria.DataStructures.TileEntity")!.GetField("ByID")!.GetValue(null)!)[id]!;
            Type captive = code.GetType("DimensionalTerraria.Systems.BoatCaptive")!;
            object template = game.GetType("Terraria.ModLoader.ModContent")!.GetMethods().Single(m => m.Name == "GetInstance" && m.IsGenericMethodDefinition)
                .MakeGenericMethod(captive).Invoke(null, null)!;
            int index = Convert.ToInt32(template.GetType().GetProperty("PerEntityIndex", All)!.GetValue(template));
            Array globals = Array.CreateInstance(game.GetType("Terraria.ModLoader.GlobalNPC")!, index + 1);
            globals.SetValue(Activator.CreateInstance(captive), index);
            Set(passenger, "_globals", globals);
            Call(boat, "Reconcile");
            Check((bool)Call(boat, "TryCapture", passenger)!, "Boat must capture the villager/hostile fixture");
            return boat;
        }
        object villageBoat = Boat(44, a), enemyBoat = Boat(53, enemy);
        Check((bool)code.GetType("DimensionalTerraria.Systems.BoatCaptureSystem")!.GetMethod("HoldAI", All)!.Invoke(null, new[] { a })!, "Boat must suspend passenger AI");
        Tick(300); Check(Golems() == 1, "Boat passengers must still sense captive hostiles and summon without their NPC AI");
        Console.WriteLine("PASS native boat village: villager AI held, captured hostile still frightens, independent system summons guard");

        // Return to the ordinary fixture for serialization tests.
        Reset(); a = Villager(0, 48 * 16); Villager(1, 49 * 16); Villager(2, 50 * 16);
        manual = Npc(15, Id(golemTemplate), 48 * 16 + 256); Tick(1);

        // Test native per-NPC save hooks with real TagCompounds and damaged HP.
        Type tagType = game.GetType("Terraria.ModLoader.IO.TagCompound")!;
        Type serializerType = game.GetType("Terraria.ModLoader.IO.TagSerializer")!;
        Type boolSerializer = game.GetType("Terraria.ModLoader.IO.BoolTagSerializer")!;
        object?[] serializerLookup = { typeof(bool), null };
        if (!(bool)serializerType.GetMethod("TryGetSerializer")!.Invoke(null, serializerLookup)!)
            serializerType.GetMethod("AddSerializer", All)!.Invoke(null, new[] { Activator.CreateInstance(boolSerializer, true) });
        Call(Memory(a), "Frighten", 900f); Set(a, "life", 37); Tick(17);
        object tag = Activator.CreateInstance(tagType)!; Call(ModNpc(a), "SaveData", tag);
        object restored = Villager(3, 47 * 16); Call(ModNpc(restored), "LoadData", tag);
        Check((int)Get(restored, "life")! == 37 && (bool)Call(ModNpc(restored), "NeedSaving")!, "Villager must save damaged health and request persistence");
        foreach (string prop in new[] { "PanicTicks", "GuardTicks", "AttemptTicks", "ThreatX" })
            Check(Equals(Memory(a).GetType().GetProperty(prop)!.GetValue(Memory(a)), Memory(restored).GetType().GetProperty(prop)!.GetValue(Memory(restored))), "Saved memory changed on load: " + prop);
        Set(ModNpc(manual), "VillageGuardian", true); Set(manual, "life", 123);
        object golemTag = Activator.CreateInstance(tagType)!; Call(ModNpc(manual), "SaveData", golemTag);
        object restoredGolem = Npc(16, Id(golemTemplate), 40 * 16); Call(ModNpc(restoredGolem), "LoadData", golemTag);
        Check((bool)Call(ModNpc(restoredGolem), "NeedSaving")! && (int)Get(restoredGolem, "life")! == 123, "Guardian provenance and damaged health must reload");
        Console.WriteLine("PASS native persistence hooks: villager health and all timer remainders, summoned guard provenance/health");

        Type worldIO = game.GetType("Terraria.ModLoader.IO.WorldIO")!;
        object saved = worldIO.GetMethod("SaveNPCs", All)!.Invoke(null, null)!;
        Check(((IList)saved).Count == 6, "World save must include four villagers and both marked guardians");
        for (int i = 0; i < npcs.Length; i++) { object npc = Activator.CreateInstance(npcType)!; Set(npc, "whoAmI", i); npcs.SetValue(npc, i); }
        worldIO.GetMethod("LoadNPCs", All)!.Invoke(null, new[] { saved });
        Check(npcs.Cast<object>().Count(n => (bool)Get(n, "active")! && (int)Get(n, "type")! == Id(villagerTemplate)) == 4 && Golems() == 2,
            "Actual WorldIO reload must retain villagers and guardians");
        Check(npcs.Cast<object>().Any(n => (bool)Get(n, "active")! && (int)Get(n, "life")! == 37), "Damaged villager health must survive real WorldIO");
        Console.WriteLine("PASS real WorldIO round-trip: all villagers and summoned guards restored, damaged health retained");

    }
}
