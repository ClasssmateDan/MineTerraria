using System.Collections;
using System.Reflection;

internal static class BoatPersistenceChecks
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    internal static void Run(Assembly game, Assembly code, object boatTile, object boatTemplate, object captiveTemplate,
        object sheepTemplate, object villagerTemplate, object golemTemplate)
    {
        Type main = game.GetType("Terraria.Main")!, npcType = game.GetType("Terraria.NPC")!;
        Type tile = game.GetType("Terraria.Tile")!, tileEntity = game.GetType("Terraria.DataStructures.TileEntity")!;
        Type npcLoader = game.GetType("Terraria.ModLoader.NPCLoader")!, globalType = game.GetType("Terraria.ModLoader.GlobalNPC")!;
        Type worldIO = game.GetType("Terraria.ModLoader.IO.WorldIO")!, tileIO = game.GetType("Terraria.ModLoader.IO.TileIO")!;
        Type tagType = game.GetType("Terraria.ModLoader.IO.TagCompound")!, tagIO = game.GetType("Terraria.ModLoader.IO.TagIO")!;
        Type boatSystem = code.GetType("DimensionalTerraria.Systems.BoatCaptureSystem")!;
        object system = Activator.CreateInstance(boatSystem)!;
        object? Call(object obj, string method, params object?[] args) => obj.GetType().GetMethod(method, All)!.Invoke(obj, args);
        object? Get(object obj, string field) => obj.GetType().GetField(field, All)!.GetValue(obj);
        void Set(object obj, string field, object value) => obj.GetType().GetField(field, All)!.SetValue(obj, value);
        int Id(object content) => Convert.ToInt32(content.GetType().GetProperty("Type")!.GetValue(content));
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        Array npcs = (Array)main.GetField("npc")!.GetValue(null)!;
        object map = main.GetField("tile")!.GetValue(null)!;
        IDictionary entities = (IDictionary)tileEntity.GetField("ByID")!.GetValue(null)!, positions = (IDictionary)tileEntity.GetField("ByPosition")!.GetValue(null)!;
        object Cell(int x, int y) => map.GetType().GetProperty("Item", new[] { typeof(int), typeof(int) })!.GetValue(map, new object[] { x, y })!;
        void Put(int x, int y) => tile.GetMethod("ResetToType")!.Invoke(Cell(x, y), new object[] { (ushort)1 });
        MethodInfo defaults = npcType.GetMethods().Single(m => m.Name == "SetDefaults" && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(int));
        object spawnParams = Activator.CreateInstance(defaults.GetParameters()[1].ParameterType)!;
        // Native content loading builds these before updating global hook lists.
        // Prior fixtures only used direct NPC hooks; WorldIO now needs the real
        // per-type NeedSaving/SaveData lookup tables as well.
        object prototype = Activator.CreateInstance(npcType)!;
        Action<int> initialize = type => defaults.Invoke(prototype, new[] { (object)type, spawnParams });
        game.GetType("Terraria.ModLoader.Core.GlobalLoaderUtils`2")!.MakeGenericType(globalType, npcType)
            .GetMethod("BuildTypeLookups")!.Invoke(null, new object[] { initialize });
        npcLoader.GetMethod("UpdateHookLists", All)!.Invoke(null, null);
        Call(sheepTemplate, "SetStaticDefaults");
        object player = ((Array)main.GetField("player")!.GetValue(null)!).GetValue((int)main.GetField("myPlayer")!.GetValue(null)!)!;
        Set(player, "active", true); Set(player, "dead", false); Set(player, "ghost", false);
        Type vector = npcType.GetField("position")!.FieldType;
        Set(player, "position", Activator.CreateInstance(vector, new object[] { 700f, 700f })!);
        main.GetField("gameMenu")!.SetValue(null, false); main.GetField("netMode")!.SetValue(null, 0); main.GetField("dedServ")!.SetValue(null, true);
        void EmptyNpcs()
        {
            for (int i = 0; i < npcs.Length; i++) { object npc = Activator.CreateInstance(npcType)!; Set(npc, "whoAmI", i); npcs.SetValue(npc, i); }
        }
        void Reset()
        {
            Call(system, "OnWorldUnload"); entities.Clear(); positions.Clear(); EmptyNpcs();
            map.GetType().GetMethod("ClearEverything")!.Invoke(map, null);
            for (int x = 12; x < 88; x++) Put(x, 50);
        }
        object NewBoat(int left)
        {
            Check((bool)game.GetType("Terraria.WorldGen")!.GetMethod("PlaceObject")!.Invoke(null,
                new object[] { left + 1, 49, Id(boatTile), true, 0, 0, -1, -1 })!, "Persistence boat native placement failed");
            int id = (int)Call(boatTemplate, "Place", left, 48)!;
            object boat = entities[id]!; Call(boat, "Reconcile"); return boat;
        }
        object Npc(int slot, int type)
        {
            object npc = npcs.GetValue(slot)!; defaults.Invoke(npc, new[] { (object)type, spawnParams }); Set(npc, "active", true); return npc;
        }
        object State(object npc) => ((Array)Get(npc, "_globals")!).Cast<object?>().Single(g => g?.GetType() == captiveTemplate.GetType())!;
        void Board(object boat, object npc)
        {
            npcType.GetProperty("Bottom")!.SetValue(npc, boat.GetType().GetProperty("SeatBottom", All)!.GetValue(boat));
            Check((bool)Call(boat, "TryCapture", npc)!, "Persistence passenger failed to board");
        }
        object Tag() => Activator.CreateInstance(tagType)!;
        void TagSet(object tag, string key, object value) => tagType.GetProperty("Item")!.SetValue(tag, value, new object[] { key });
        object List(object tag, string key) => tagType.GetMethod("GetList")!.MakeGenericMethod(tagType).Invoke(tag, new object[] { key })!;
        object Binary(object root)
        {
            using var stream = new MemoryStream();
            tagIO.GetMethod("ToStream", new[] { tagType, typeof(Stream), typeof(bool) })!.Invoke(null, new object[] { root, stream, false });
            using var input = new MemoryStream(stream.ToArray());
            return tagIO.GetMethod("FromStream", new[] { typeof(Stream), typeof(bool) })!.Invoke(null, new object[] { input, false })!;
        }
        object Save()
        {
            object root = Tag(), boats = Tag();
            TagSet(root, "NPCs", worldIO.GetMethod("SaveNPCs", All)!.Invoke(null, null)!);
            TagSet(root, "Entities", tileIO.GetMethod("SaveTileEntities", All)!.Invoke(null, null)!);
            Call(system, "SaveWorldData", boats); TagSet(root, "Boats", boats);
            return Binary(root);
        }
        void Load(object saved, bool priorCreature = false, bool bind = true)
        {
            Call(system, "OnWorldUnload"); entities.Clear(); positions.Clear(); EmptyNpcs();
            if (priorCreature) Npc(0, 1); // Force every saved passenger into another slot.
            worldIO.GetMethod("LoadNPCs", All)!.Invoke(null, new[] { List(saved, "NPCs") });
            tileIO.GetMethod("LoadTileEntities", All)!.Invoke(null, new[] { List(saved, "Entities") });
            Call(system, "LoadWorldData", tagType.GetMethod("GetCompound")!.Invoke(saved, new object[] { "Boats" }));
            if (bind) Call(system, "PreUpdateNPCs");
        }
        object[] Active() => npcs.Cast<object>().Where(n => (bool)Get(n, "active")!).ToArray();
        object[] Boats() => ((IEnumerable)boatSystem.GetMethod("Boats", All)!.Invoke(null, null)!).Cast<object>().ToArray();
        object Origin(object boat) => boat.GetType().GetField("Position")!.GetValue(boat)!;
        int Left(object boat) => Convert.ToInt32(Origin(boat).GetType().GetField("X")!.GetValue(Origin(boat)));
        Reset();
        int[] types = { 1, Id(sheepTemplate), Id(golemTemplate), Id(villagerTemplate), 1, 50 };
        int[] lefts = { 20, 25, 30, 40, 50, 60 };
        int[] health = { 13, 37, 123, 29, 9, 1500 };
        for (int i = 0; i < types.Length; i++)
        {
            object boat = NewBoat(lefts[i]), npc = Npc(i, types[i]);
            if (i == 4) defaults.Invoke(npc, new[] { (object)(-3), spawnParams });
            if (i == 5) { ((float[])Get(npc, "localAI")!)[3] = 1; ((float[])Get(npc, "ai")!)[3] = 1700; }
            Set(npc, "life", health[i]); Board(boat, npc);
            if (i == 2) { Call(boat, "BecomeOverlay"); Put(lefts[i] + 1, 49); }
            Check((bool)Call(State(npc), "NeedSaving", npc)!, "Every valid passenger must request native world saving");
        }
        object blue = npcs.GetValue(0)!;
        ((int[])Get(blue, "buffType")!)[0] = 20; ((int[])Get(blue, "buffTime")!)[0] = 450;
        object saved = Save();
        Check(((IList)List(saved, "NPCs")).Count == 6, "Exactly six captured creatures should save, including vanilla non-town NPCs");
        Load(saved, priorCreature: true);
        Check(Active().Length == 7 && Boats().Length == 6, "Loading with shifted slots must preserve six passengers plus one existing creature");
        foreach (object boat in Boats())
        {
            int index = Array.IndexOf(lefts, Left(boat));
            object passenger = Get(boat, "Occupant") ?? throw new InvalidOperationException("Restored boat lost its passenger");
            Check(index >= 0 && (int)Get(passenger, "type")! == types[index] && (int)Get(passenger, "life")! == health[index], "Passenger identity, health or exact boat assignment changed");
            Check(ReferenceEquals(Get(State(passenger), "Host"), boat), "Restored passenger must have both sides of the seat link");
            Check((int)Get(passenger, "whoAmI")! != index, "Seat restore must not depend on the original NPC slot");
        }
        object green = Get(Boats().Single(b => Left(b) == 50), "Occupant")!;
        Check((int)Get(green, "netID")! == -3, "Negative-ID slime variant must survive native WorldIO");
        object restoredBlue = Get(Boats().Single(b => Left(b) == 20), "Occupant")!;
        Check(((int[])Get(restoredBlue, "buffType")!)[0] == 20 && ((int[])Get(restoredBlue, "buffTime")!)[0] == 450, "Passenger debuffs must retain type and duration");
        object kingBoat = Boats().Single(b => Left(b) == 60), king = Get(kingBoat, "Occupant")!;
        Check(((float[])Get(king, "localAI")!)[3] == 1 && ((float[])Get(king, "ai")!)[3] == 1700, "King Slime initialization and damage threshold must survive");
        Console.WriteLine("PASS native passenger binary round-trip: slime, sheep, manual golem, villager, slime variant and King Slime retain health, buffs, AI and exact seats after slot changes");
        object overlayBoat = Boats().Single(b => Left(b) == 30);
        Check((bool)Get(overlayBoat, "Overlay")! && Get(overlayBoat, "Occupant") is not null, "Overlap boat must restore its passenger while retaining the overlapping block");
        object again = Save(); Load(again); Call(system, "PreUpdateNPCs"); Call(system, "PostUpdateNPCs");
        Check(Active().Length == 6 && Boats().Count(b => Get(b, "Occupant") is not null) == 6, "Repeated save/reload must retain six passengers; the unrelated free slime keeps its normal non-saving policy");
        Console.WriteLine("PASS native boat persistence: physical and overlapping boats survive repeated saves without duplicate passengers");

        Load(saved, bind: false); object pendingSave = Save(); Load(pendingSave);
        Check(Active().Length == 6 && Boats().Count(b => Get(b, "Occupant") is not null) == 6, "Saving pending passengers before binding must not lose their boat association");
        Console.WriteLine("PASS native pending-seat persistence: saving before the first capture pass preserves all six passengers");

        // Restore pending seats before a free NPC already overlapping the hull.
        Load(saved, priorCreature: true, bind: false);
        object targetBoat = Boats().Single(b => Left(b) == 20), intruder = npcs.GetValue(0)!;
        Call(targetBoat, "Reconcile"); npcType.GetProperty("Bottom")!.SetValue(intruder, targetBoat.GetType().GetProperty("SeatBottom", All)!.GetValue(targetBoat));
        Call(system, "PreUpdateNPCs");
        Check(!ReferenceEquals(Get(targetBoat, "Occupant"), intruder) && (int)Get(Get(targetBoat, "Occupant")!, "life")! == 13,
            "Nearby free creature cannot steal a saved passenger's seat");
        Console.WriteLine("PASS native restoration priority: saved passenger claims its exact boat before ordinary capture");

        Load(saved, bind: false); Put(31, 45); Call(system, "PreUpdateNPCs");
        object blockedGolem = Active().Single(n => (int)Get(n, "type")! == Id(golemTemplate));
        Check(Active().Length == 6 && Get(State(blockedGolem), "Host") is null && Get(State(blockedGolem), "PendingBoat") is null && (int)Get(blockedGolem, "life")! == 123,
            "Blocked seat must leave one free living passenger, with no stale link or heal");
        tile.GetMethod("ClearTile")!.Invoke(Cell(31, 45), null);
        Console.WriteLine("PASS native blocked-seat recovery: terrain obstruction releases the saved creature without deletion, duplication or healing");

        // If the recorded boat is gone, do not recreate either it or an NPC.
        Load(saved, bind: false); object missingBoat = Boats().Single(b => Left(b) == 25);
        object missingPosition = Origin(missingBoat);
        int missingId = (int)Get(missingBoat, "ID")!; entities.Remove(missingId); positions.Remove(missingPosition);
        for (int x = 25; x < 28; x++) for (int y = 48; y < 50; y++) tile.GetMethod("ClearTile")!.Invoke(Cell(x, y), null);
        int before = Active().Length; Call(system, "PreUpdateNPCs");
        object freeSheep = Active().Single(n => (int)Get(n, "type")! == Id(sheepTemplate));
        Check(Active().Length == before && Get(State(freeSheep), "Host") is null && Get(State(freeSheep), "PendingBoat") is null && (int)Get(State(freeSheep), "Cooldown")! == 60,
            "Missing boat must release exactly one live creature and clear stale pending state");
        Check(!(bool)Call(State(freeSheep), "NeedSaving", freeSheep)!, "Released sheep must return to its original save policy");
        Console.WriteLine("PASS native missing-boat recovery: one live released passenger, pending link cleared, original save policy restored");

        kingBoat = Boats().Single(b => Left(b) == 60); king = Get(kingBoat, "Occupant")!;
        code.GetType("DimensionalTerraria.Systems.BoatDamageSystem")!.GetMethod("ReleaseBeforeDamage", All)!.Invoke(null, new[] { king });
        Check(Get(State(king), "Host") is null && Get(kingBoat, "Occupant") is null && (bool)Get(king, "active")! && (int)Get(king, "life")! == 1500 && ((float[])Get(king, "ai")!)[3] == 1700,
            "Reloaded boss must retain the break-before-damage rule and its native damage threshold");
        Check(!(bool)Call(State(king), "NeedSaving", king)!, "Released boss must no longer request passenger-only saving");
        Console.WriteLine("PASS native reloaded boss release: boat breaks before damage, boss stays alive, damage threshold and normal save policy retained");
        Call(system, "OnWorldUnload"); entities.Clear(); positions.Clear(); EmptyNpcs();
    }
}
