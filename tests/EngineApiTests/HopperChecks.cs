using System.Collections;
using System.Reflection;

internal static class HopperChecks
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    internal static void Run(Assembly game, Assembly code, object hopperTile, object hopperTemplate, object pistonTile, object pistonTemplate, object redstoneTile)
    {
        Type main = game.GetType("Terraria.Main")!, tile = game.GetType("Terraria.Tile")!, chestType = game.GetType("Terraria.Chest")!;
        Type itemType = game.GetType("Terraria.Item")!, itemIds = game.GetType("Terraria.ID.ItemID")!;
        Type tileEntity = game.GetType("Terraria.DataStructures.TileEntity")!;
        IDictionary entities = (IDictionary)tileEntity.GetField("ByID")!.GetValue(null)!, positions = (IDictionary)tileEntity.GetField("ByPosition")!.GetValue(null)!;
        Array items = (Array)main.GetField("item")!.GetValue(null)!, chests = (Array)main.GetField("chest")!.GetValue(null)!;
        Array oldItems = (Array)items.Clone(), oldChests = (Array)chests.Clone();
        object map = main.GetField("tile")!.GetValue(null)!;
        Type foreground = code.GetType("DimensionalTerraria.World.TerrariaPistonWorld+Foreground")!;
        var settings = new[] { "gameMenu", "gamePaused", "netMode" }.Select(n => main.GetField(n)!).ToArray();
        object?[] oldSettings = settings.Select(f => f.GetValue(null)).ToArray();
        var actors = ((Array)main.GetField("player")!.GetValue(null)!).Cast<object>().Concat(((Array)main.GetField("npc")!.GetValue(null)!).Cast<object>())
            .Where(a => a is not null).Select(a => (Actor: a, Active: a.GetType().GetField("active")!.GetValue(a))).ToArray();
        foreach (var a in actors) a.Actor.GetType().GetField("active")!.SetValue(a.Actor, false);
        object system = Activator.CreateInstance(code.GetType("DimensionalTerraria.Systems.HopperSystem")!)!;
        int Id(object content) => Convert.ToInt32(content.GetType().GetProperty("Type")!.GetValue(content));
        int hopperId = Id(hopperTile), pistonId = Id(pistonTile), redstoneId = Id(redstoneTile), nextDrop = 0;
        int ItemId(string name) => Convert.ToInt32(itemIds.GetField(name)!.GetRawConstantValue());
        object? Call(object target, string name, params object?[] args) => target.GetType().GetMethod(name, All)!.Invoke(target, args);
        object Tile(int x, int y) => map.GetType().GetProperty("Item", new[] { typeof(int), typeof(int) })!.GetValue(map, new object[] { x, y })!;
        void Put(int x, int y, int kind) => tile.GetMethod("ResetToType")!.Invoke(Tile(x, y), new object[] { (ushort)kind });
        void Frames(int x, int y, short fx, short fy)
        {
            object data = foreground.GetMethod("Read", All)!.Invoke(null, new[] { Tile(x, y) })!;
            foreground.GetProperty("X")!.SetValue(data, fx); foreground.GetProperty("Y")!.SetValue(data, fy);
            foreground.GetMethod("Write", All)!.Invoke(data, new[] { Tile(x, y) });
        }
        bool Has(int x, int y) => (bool)tile.GetProperty("HasTile")!.GetValue(Tile(x, y))!;
        int Kind(int x, int y) => Has(x, y) ? (ushort)tile.GetProperty("TileType")!.GetValue(Tile(x, y))! : -1;
        int Stack(object item) => (int)itemType.GetField("stack")!.GetValue(item)!;
        bool Active(object item) => (bool)itemType.GetField("active")!.GetValue(item)!;
        object Item(string name, int count)
        {
            object item = Activator.CreateInstance(itemType)!;
            itemType.GetMethod("SetDefaults", new[] { typeof(int) })!.Invoke(item, new object[] { ItemId(name) });
            itemType.GetField("stack")!.SetValue(item, count);
            return item;
        }
        void Reset()
        {
            map.GetType().GetMethod("ClearEverything")!.Invoke(map, null); entities.Clear(); positions.Clear(); Array.Clear(chests);
            for (int i = 0; i < items.Length; i++) items.SetValue(Activator.CreateInstance(itemType), i);
            nextDrop = 0;
        }
        object Hopper(int x, int y)
        {
            Put(x, y, hopperId);
            int id = (int)Call(hopperTemplate, "AfterPlacement", x, y, hopperId, 0, 0, 0)!;
            Call(hopperTemplate, "AfterPlacement", x, y, hopperId, 0, 0, 0);
            return entities[id]!;
        }
        object Chest(int left, int top, int style = 0)
        {
            for (int dx = 0; dx < 2; dx++) for (int dy = 0; dy < 2; dy++)
            {
                Put(left + dx, top + dy, 21);
                Frames(left + dx, top + dy, (short)(style * 36 + dx * 18), (short)(dy * 18));
            }
            Put(left, top + 2, 1); Put(left + 1, top + 2, 1);
            int index = (int)chestType.GetMethod("CreateChest")!.Invoke(null, new object[] { left, top, -1 })!;
            if (index < 0) throw new InvalidOperationException("Could not create native test chest");
            return chests.GetValue(index)!;
        }
        Array Slots(object chest) => (Array)chestType.GetField("item")!.GetValue(chest)!;
        int Count(object chest, string name) => Slots(chest).Cast<object?>().Where(i => i is not null &&
            (int)itemType.GetField("type")!.GetValue(i)! == ItemId(name)).Sum(i => Stack(i!));
        object Drop(string name, int count, float centerX, float bottom)
        {
            object item = Item(name, count);
            itemType.GetField("active")!.SetValue(item, true);
            itemType.GetField("whoAmI")!.SetValue(item, nextDrop);
            Type vector = itemType.GetField("position")!.FieldType;
            itemType.GetProperty("Bottom")!.SetValue(item, Activator.CreateInstance(vector, new object[] { centerX * 16, bottom * 16 }));
            items.SetValue(item, nextDrop++); return item;
        }
        void Tick(int count = 1) { for (int i = 0; i < count; i++) Call(system, "PostUpdateItems"); }
        void Ready(object hopper) => hopper.GetType().GetField("cooldown", All)!.SetValue(hopper, 0);
        try
        {
            Call(system, "Load");
            main.GetField("gameMenu")!.SetValue(null, false); main.GetField("gamePaused")!.SetValue(null, false); main.GetField("netMode")!.SetValue(null, 0);
            Call(hopperTile, "SetStaticDefaults");
            ((bool[])main.GetField("tileFrameImportant")!.GetValue(null)!)[21] = true;
            ((bool[])main.GetField("tileSolid")!.GetValue(null)!)[1] = true;

            Reset(); object destination = Chest(30, 31), hopper = Hopper(30, 30); Put(30, 29, 1);
            object upperSurface = Drop("IronBar", 5, 30.5f, 29), ownSurface = Drop("IronBar", 2, 30.5f, 30);
            object leftItem = Drop("IronBar", 3, 29.99f, 29), rightItem = Drop("IronBar", 4, 31, 29);
            object highItem = Drop("IronBar", 6, 30.5f, 28.9f), lowItem = Drop("IronBar", 7, 30.5f, 30.1f);
            Tick();
            if (entities.Count != 1 || Count(destination, "IronBar") != 7 || Active(upperSurface) || Active(ownSurface) ||
                new[] { leftItem, rightItem, highItem, lowItem }.Any(i => !Active(i)) || Kind(30, 29) != 1)
                throw new InvalidOperationException("Hopper must include the above solid block's top boundary without collecting neighboring columns or altering terrain.");
            Console.WriteLine("PASS native hopper intake: above-block upper surface and hopper top collect, both side columns/out-of-height items remain, solid cover intact");

            Reset(); destination = Chest(29, 31); hopper = Hopper(30, 30); Drop("Wood", 8, 30.5f, 30); Tick();
            if (Count(destination, "Wood") != 8) throw new InvalidOperationException("A hopper over the right half must find the real chest origin.");
            Reset(); destination = Chest(31, 31); hopper = Hopper(30, 30); object outside = Drop("Wood", 8, 30.5f, 30); Tick();
            if (!Active(outside) || Count(destination, "Wood") != 0) throw new InvalidOperationException("An adjacent, non-below chest must not receive items.");
            Reset(); destination = Chest(30, 27); Slots(destination).SetValue(Item("Wood", 12), 0); hopper = Hopper(30, 30); Tick(16);
            if (Count(destination, "Wood") != 12) throw new InvalidOperationException("Hopper cannot extract items from the chest above it.");
            Console.WriteLine("PASS native hopper destination: either chest half accepted, side chest rejected, chest above never extracted");

            Reset(); hopper = Hopper(30, 30); object pending = Drop("IronBar", 5, 30.5f, 30); Tick();
            if (!Active(pending) || Stack(pending) != 5) throw new InvalidOperationException("Missing chest cannot consume an item.");
            destination = Chest(30, 31, 2); Ready(hopper); Tick();
            if (!Active(pending) || Count(destination, "IronBar") != 0) throw new InvalidOperationException("Locked native gold chest must refuse insertion.");
            for (int dx = 0; dx < 2; dx++) for (int dy = 0; dy < 2; dy++) Frames(30 + dx, 31 + dy, (short)(dx * 18), (short)(dy * 18));
            for (int i = 0; i < 40; i++) Slots(destination).SetValue(Item("StoneBlock", 9999), i);
            Ready(hopper); Tick();
            if (!Active(pending) || Stack(pending) != 5) throw new InvalidOperationException("Full chest cannot delete a pending drop.");
            object almostFull = Item("IronBar", 1); int max = (int)itemType.GetField("maxStack")!.GetValue(almostFull)!;
            itemType.GetField("stack")!.SetValue(almostFull, max - 2); Slots(destination).SetValue(almostFull, 7);
            Ready(hopper); Tick();
            if (Stack(almostFull) != max || !Active(pending) || Stack(pending) != 3) throw new InvalidOperationException("Partial deposit must retain the exact remainder in the world.");
            Slots(destination).SetValue(Activator.CreateInstance(itemType), 1); Ready(hopper); Tick();
            if (Active(pending) || Count(destination, "IronBar") != max + 3 || ReferenceEquals(Slots(destination).GetValue(1), pending))
                throw new InvalidOperationException("Retry must insert the remainder without aliasing the live world item or duplicating it.");
            Console.WriteLine("PASS native hopper conservation: missing/locked/full chest preserves drops, partial stack retains exact remainder, retry creates independent inventory item");

            Reset(); destination = Chest(30, 31); hopper = Hopper(30, 30);
            Slots(destination).SetValue(Item("IronBar", 20), 8); Drop("IronBar", 5, 30.5f, 30);
            object sword = Drop("WoodenSword", 1, 30.5f, 30);
            // The input is an already-prefixed world item, not a new reforge.
            itemType.GetField("prefix")!.SetValue(sword, 81);
            int prefix = Convert.ToInt32(itemType.GetField("prefix")!.GetValue(sword));
            object heart = Drop("Heart", 1, 30.5f, 30), mana = Drop("Star", 1, 30.5f, 30);
            Tick();
            object storedSword = Slots(destination).GetValue(0)!;
            if (Count(destination, "IronBar") != 25 || Stack(Slots(destination).GetValue(8)!) != 25 ||
                (int)itemType.GetField("type")!.GetValue(storedSword)! != ItemId("WoodenSword") ||
                Convert.ToInt32(itemType.GetField("prefix")!.GetValue(storedSword)) != prefix || Active(sword) || !Active(heart) || !Active(mana))
                throw new InvalidOperationException("Hopper must merge existing stacks first, retain native weapon prefixes and leave instant pickups for players.");
            Console.WriteLine("PASS native hopper item fidelity: existing stack preferred, native prefix cloned, heart and mana-star instant pickups excluded");

            Reset(); destination = Chest(30, 31); hopper = Hopper(30, 30); Tick();
            pending = Drop("Wood", 4, 30.5f, 30); Tick(7);
            if (!Active(pending)) throw new InvalidOperationException("Hopper must respect its eight-frame transfer interval.");
            main.GetField("gamePaused")!.SetValue(null, true); Tick(10);
            if (!Active(pending)) throw new InvalidOperationException("Paused world cannot transfer items.");
            main.GetField("gamePaused")!.SetValue(null, false);
            tile.GetProperty("IsActuated")!.SetValue(Tile(30, 30), true); Tick();
            if (!Active(pending)) throw new InvalidOperationException("Actuated hopper must not collect items.");
            tile.GetProperty("IsActuated")!.SetValue(Tile(30, 30), false); Tick(8);
            if (Active(pending) || Count(destination, "Wood") != 4) throw new InvalidOperationException("Unpaused, restored hopper must resume exactly once.");
            Console.WriteLine("PASS native hopper scheduling: eight-frame retry, pause and actuation gates, exact single transfer on resume");

            Reset(); destination = Chest(31, 31); hopper = Hopper(31, 30);
            Put(30, 30, pistonId); int pistonIndex = (int)Call(pistonTemplate, "AfterPlacement", 30, 30, pistonId, 0, 1, 0)!;
            object piston = entities[pistonIndex]!; Frames(30, 30, 18, 0);
            Put(29, 30, redstoneId); Call(piston, "Tick");
            object shifted = hopper.GetType().GetField("Position")!.GetValue(hopper)!;
            if (Kind(32, 30) != hopperId || Convert.ToInt32(shifted.GetType().GetField("X")!.GetValue(shifted)) != 32 || entities.Count != 2)
                throw new InvalidOperationException("Piston must move hopper tile and entity together.");
            Drop("Wood", 3, 32.5f, 30); Tick();
            if (Count(destination, "Wood") != 3) throw new InvalidOperationException("Moved hopper must use its new input column and chest location.");
            Console.WriteLine("PASS native moving hopper: piston migrates tile/entity and collection follows the new column above the other chest half");

            Reset(); hopper = Hopper(30, 30);
            MethodInfo kill = game.GetType("Terraria.WorldGen")!.GetMethod("KillTile")!;
            kill.Invoke(null, new object[] { 30, 30, true, false, false }); Tick();
            if (!Has(30, 30) || entities.Count != 1) throw new InvalidOperationException("A failed damage attempt cannot remove hopper state.");
            kill.Invoke(null, new object[] { 30, 30, false, false, false });
            if (entities.Count != 0) throw new InvalidOperationException("Destroyed hopper must release its entity immediately before another machine can replace it.");
            Tick();
            int dropped = items.Cast<object>().Where(i => Active(i) && itemType.GetProperty("ModItem")!.GetValue(i)?.GetType().Name == "Hopper").Sum(Stack);
            if (Has(30, 30) || entities.Count != 0 || dropped != 1) throw new InvalidOperationException("Broken hopper must remove its entity and drop one item.");
            Console.WriteLine("PASS native hopper lifecycle: idempotent placement, failed damage retains entity, successful destruction drops exactly one hopper and cleans state");
        }
        finally
        {
            Call(system, "Unload");
            Reset(); Array.Copy(oldItems, items, items.Length); Array.Copy(oldChests, chests, chests.Length);
            for (int i = 0; i < settings.Length; i++) settings[i].SetValue(null, oldSettings[i]);
            foreach (var a in actors) a.Actor.GetType().GetField("active")!.SetValue(a.Actor, a.Active);
        }
    }
}
