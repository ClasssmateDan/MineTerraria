using System.Collections;
using System.Reflection;
using System.Reflection.Emit;

internal static class RedstoneChecks
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    internal static void Run(Assembly game, Assembly code, object redstoneTile, object pulserTile, object pistonTile, object stickyTile, object headTile,
        object pistonTemplate, object boatTile, object boatTemplate)
    {
        Type main = game.GetType("Terraria.Main")!, tile = game.GetType("Terraria.Tile")!;
        Type worldType = code.GetType("DimensionalTerraria.World.TerrariaPistonWorld")!;
        Type foreType = worldType.GetNestedType("Foreground", All)!;
        Type gridType = code.GetType("DimensionalTerraria.Core.GridCell")!;
        Type rules = code.GetType("DimensionalTerraria.Core.PistonRules")!;
        Type boatSystem = code.GetType("DimensionalTerraria.Systems.BoatCaptureSystem")!;
        object boatLayers = Activator.CreateInstance(boatSystem)!;
        object redstoneSystem = Activator.CreateInstance(code.GetType("DimensionalTerraria.Systems.RedstoneSystem")!)!;
        var runtimeFields = new[] { "gameMenu", "gamePaused", "netMode" }.Select(name => main.GetField(name)!).ToArray();
        object?[] savedRuntime = runtimeFields.Select(field => field.GetValue(null)).ToArray();
        object map = main.GetField("tile")!.GetValue(null)!;
        IDictionary entities = (IDictionary)game.GetType("Terraria.DataStructures.TileEntity")!.GetField("ByID")!.GetValue(null)!;
        IDictionary positions = (IDictionary)game.GetType("Terraria.DataStructures.TileEntity")!.GetField("ByPosition")!.GetValue(null)!;
        Array players = (Array)main.GetField("player")!.GetValue(null)!, npcs = (Array)main.GetField("npc")!.GetValue(null)!;
        Array savedNpcs = (Array)npcs.Clone();
        var actors = players.Cast<object>().Concat(npcs.Cast<object>()).Where(o => o is not null).Select(o => (Value: o, Active: (bool)o.GetType().GetField("active")!.GetValue(o)!)).ToArray();
        foreach (var actor in actors) actor.Value.GetType().GetField("active")!.SetValue(actor.Value, false);
        int Id(object content) => Convert.ToInt32(content.GetType().GetProperty("Type")!.GetValue(content));
        int red = Id(redstoneTile), pulse = Id(pulserTile), normal = Id(pistonTile), sticky = Id(stickyTile), head = Id(headTile), boatId = Id(boatTile);
        object Tile(int x, int y) => map.GetType().GetProperty("Item", new[] { typeof(int), typeof(int) })!.GetValue(map, new object[] { x, y })!;
        void Put(int x, int y, int type) => tile.GetMethod("ResetToType")!.Invoke(Tile(x, y), new object[] { (ushort)type });
        void Clear(int x, int y) => tile.GetMethod("ClearTile")!.Invoke(Tile(x, y), null);
        bool Has(int x, int y) => (bool)tile.GetProperty("HasTile")!.GetValue(Tile(x, y))!;
        object Read(int x, int y) => foreType.GetMethod("Read", All)!.Invoke(null, new[] { Tile(x, y) })!;
        int TypeAt(int x, int y) => Has(x, y) ? Convert.ToInt32(foreType.GetProperty("Type")!.GetValue(Read(x, y))) : -1;
        object? Call(object target, string name, params object?[] args) => target.GetType().GetMethod(name, All)!.Invoke(target, args);
        Type wallType = game.GetType("Terraria.WallTypeData")!;
        Array walls = (Array)map.GetType().GetMethod("GetData")!.MakeGenericMethod(wallType).Invoke(map, null)!;
        void Wall(int x, int y, ushort value)
        {
            object entry = walls.GetValue(x * 100 + y)!;
            wallType.GetField("Type")!.SetValue(entry, value); walls.SetValue(entry, x * 100 + y);
        }
        ushort WallAt(int x, int y) => (ushort)wallType.GetField("Type")!.GetValue(walls.GetValue(x * 100 + y))!;
        Type liquidType = game.GetType("Terraria.LiquidData")!;
        Array liquids = (Array)map.GetType().GetMethod("GetData")!.MakeGenericMethod(liquidType).Invoke(map, null)!;
        void Water(int x, int y, byte amount)
        {
            object entry = liquids.GetValue(x * 100 + y)!;
            liquidType.GetField("Amount")!.SetValue(entry, amount); liquids.SetValue(entry, x * 100 + y);
        }
        byte WaterAt(int x, int y) => (byte)liquidType.GetField("Amount")!.GetValue(liquids.GetValue(x * 100 + y))!;
        int Drops(string itemName) => ((Array)main.GetField("item")!.GetValue(null)!).Cast<object>().Where(i =>
            (bool)i.GetType().GetField("active")!.GetValue(i)! && i.GetType().GetProperty("ModItem")!.GetValue(i)?.GetType().Name == itemName)
            .Sum(i => (int)i.GetType().GetField("stack")!.GetValue(i)!);
        void Reset()
        {
            boatSystem.GetMethod("OnWorldUnload")!.Invoke(Activator.CreateInstance(boatSystem), null);
            map.GetType().GetMethod("ClearEverything")!.Invoke(map, null);
            entities.Clear(); positions.Clear();
        }
        object Place(int x, int y, bool adhesive, int direction)
        {
            Put(x, y, adhesive ? sticky : normal);
            int id = (int)Call(pistonTemplate, "AfterPlacement", x, y, adhesive ? sticky : normal, 0, 1, 0)!;
            object entity = entities[id]!;
            while ((int)entity.GetType().GetProperty("Direction", All)!.GetValue(entity)! != direction) Call(entity, "Rotate");
            entity.GetType().GetField("cooldown", All)!.SetValue(entity, 0);
            return entity;
        }
        void Tick(object piston) { piston.GetType().GetField("cooldown", All)!.SetValue(piston, 0); Call(piston, "Tick"); }
        bool Extended(object piston) => (bool)piston.GetType().GetProperty("Extended", All)!.GetValue(piston)!;
        try
        {
            main.GetField("gameMenu")!.SetValue(null, false);
            main.GetField("gamePaused")!.SetValue(null, false);
            main.GetField("netMode")!.SetValue(null, 0);
            Call(redstoneSystem, "Load");
            Call(boatLayers, "Load");
            ((bool[])main.GetField("tileFrameImportant")!.GetValue(null)!)[21] = true;
            ((bool[])main.GetField("tileSolid")!.GetValue(null)!)[0] = true;
            foreach (object content in new[] { redstoneTile, pulserTile, pistonTile, stickyTile, headTile }) Call(content, "SetStaticDefaults");
            Reset(); Put(40, 40, red);
            MethodInfo charged = code.GetType("DimensionalTerraria.Systems.RedstoneSystem")!.GetMethod("Charged", All)!;
            bool Charged(int x, int y) => (bool)charged.Invoke(null, new object[] { x, y })!;
            if (!Charged(39, 40) || !Charged(41, 40) || !Charged(40, 39) || !Charged(40, 41) || Charged(41, 41) || Charged(42, 40))
                throw new InvalidOperationException("Redstone must power exactly the four immediate neighbors.");
            Put(41, 40, 1);
            if (!Charged(41, 40) || Charged(42, 40)) throw new InvalidOperationException("A neighboring solid block is charged without introducing unrequested power relay.");
            Console.WriteLine("PASS native redstone power: four direct neighbors, no diagonal/distance-two relay");

            Reset(); Put(40, 40, pulse); Put(41, 40, 1); Put(60, 60, pulse);
            Call(redstoneSystem, "OnWorldLoad");
            Type sourceSystem = redstoneSystem.GetType();
            bool PulseOn() => (bool)sourceSystem.GetProperty("PulseOn", All)!.GetValue(null)!;
            int[] animation = (int[])main.GetField("tileFrame")!.GetValue(null)!;
            MethodInfo stampMethod = code.GetType("DimensionalTerraria.Rendering.WorldMeshBuilder")!.GetMethod("ContentStamp")!;
            object chunk = Activator.CreateInstance(game.GetType("Microsoft.Xna.Framework.Point") ??
                AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Microsoft.Xna.Framework.Point")).First(t => t is not null)!, 1, 1)!;
            int Stamp() => (int)stampMethod.Invoke(null, new object?[] { chunk, null })!;
            int offStamp = Stamp(), poweredTicks = 0;
            List<(int Tick, bool On)> transitions = new();
            bool previous = PulseOn();
            if (previous || Charged(39, 40)) throw new InvalidOperationException("A reloaded clock must start in its off half.");
            for (int tick = 1; tick <= 120; tick++)
            {
                Call(redstoneSystem, "PostUpdateWorld");
                bool on = PulseOn(); if (on) poweredTicks++;
                if (on != previous) { transitions.Add((tick, on)); previous = on; }
                if (new[] { (39, 40), (41, 40), (40, 39), (40, 41), (59, 60) }.Any(p => Charged(p.Item1, p.Item2) != on) ||
                    Charged(41, 41) || Charged(42, 40) || animation[pulse] != (on ? 1 : 0))
                    throw new InvalidOperationException("Pulse power must synchronize all sources and exactly four neighbors, without solid-block relay.");
                if (on && Stamp() == offStamp) throw new InvalidOperationException("3D chunk invalidation must see the pulser's current indicator frame.");
            }
            if (poweredTicks != 60 || !transitions.SequenceEqual(new[] { (30, true), (60, false), (90, true), (120, false) }))
                throw new InvalidOperationException("The real world update must produce 30 on ticks and 30 off ticks per 60-tick period.");
            for (int i = 0; i < 30; i++) Call(redstoneSystem, "PostUpdateWorld");
            tile.GetProperty("IsActuated")!.SetValue(Tile(40, 40), true);
            if (Charged(39, 40)) throw new InvalidOperationException("An actuated pulser cannot output power.");
            tile.GetProperty("IsActuated")!.SetValue(Tile(40, 40), false);
            object[] offsets = { pulse, 40, 40, 0, 0 };
            game.GetType("Terraria.ModLoader.TileLoader")!.GetMethod("SetAnimationFrame")!.Invoke(null, offsets);
            if ((int)offsets[4] != 18) throw new InvalidOperationException("Native tile animation must select the padded ON face.");
            main.GetField("gamePaused")!.SetValue(null, true);
            for (int i = 0; i < 40; i++) Call(redstoneSystem, "PostUpdateWorld");
            if (!PulseOn() || animation[pulse] != 1) throw new InvalidOperationException("Pause cannot advance the pulse clock or indicator.");
            main.GetField("gamePaused")!.SetValue(null, false);
            main.GetField("gameMenu")!.SetValue(null, true);
            for (int i = 0; i < 40; i++) Call(redstoneSystem, "PostUpdateWorld");
            if (!PulseOn()) throw new InvalidOperationException("Menus outside the world cannot advance the clock.");
            main.GetField("gameMenu")!.SetValue(null, false);
            Call(redstoneSystem, "OnWorldUnload"); Call(redstoneSystem, "OnWorldLoad");
            Put(42, 40, red);
            if (PulseOn() || animation[pulse] != 0 || !Charged(41, 40))
                throw new InvalidOperationException("World reload must reset the clock; a constant source must still override the off pulse.");
            Console.WriteLine("PASS native pulser clock: exact 30/30 ticks, four neighbors, shared phase, actuated suppression, pause/menu suspension and world reset");
            Console.WriteLine("PASS native pulser visuals: real animation selects the on face and invalidates cached 3D chunks on phase changes");

            Reset(); Put(40,40,pulse); Put(60,60,pulse); Call(redstoneSystem,"OnWorldLoad");
            if (!(bool)Call(pulserTile,"RightClick",40,40)! || !Charged(39,40) || Charged(59,60))
                throw new InvalidOperationException("Right-click must invert this source immediately, without changing another source");
            for (int tick=0;tick<120;tick++)
            {
                bool clock=PulseOn();
                if (Charged(39,40)==clock || Charged(59,60)!=clock) throw new InvalidOperationException("Inverted and normal sources must remain complementary every tick");
                object[] flipOffsets={pulse,40,40,0,0};
                game.GetType("Terraria.ModLoader.TileLoader")!.GetMethod("SetAnimationFrame")!.Invoke(null,flipOffsets);
                if ((short)tile.GetProperty("TileFrameX")!.GetValue(Tile(40,40))!+(int)flipOffsets[3]!=0 || (int)flipOffsets[4]!=(clock?0:18))
                    throw new InvalidOperationException("Inverted native texture frame must agree with power without sampling outside the atlas");
                Call(redstoneSystem,"PostUpdateWorld");
            }
            bool clockBefore=PulseOn(); Call(pulserTile,"RightClick",40,40);
            if (PulseOn()!=clockBefore || Charged(39,40)!=clockBefore) throw new InvalidOperationException("Second click must restore phase without resetting the clock");
            Console.WriteLine("PASS native pulser inversion: immediate per-source complement, 120 exact opposite ticks, matching indicator UVs and double-click restoration without clock reset");

            Reset(); object flippedMover=Place(30,30,true,1); Put(29,30,red); Put(31,30,pulse);
            Call(pulserTile,"RightClick",31,30); Tick(flippedMover);
            if (TypeAt(32,30)!=pulse || (short)tile.GetProperty("TileFrameX")!.GetValue(Tile(32,30))! != 18)
                throw new InvalidOperationException("Piston movement must preserve the source's inverted phase");
            Call(redstoneSystem,"OnWorldUnload"); Call(redstoneSystem,"OnWorldLoad");
            if (!Charged(32,29)) throw new InvalidOperationException("World clock reset must preserve the tile's inverted phase bit");
            Type tileIO=game.GetType("Terraria.ModLoader.IO.TileIO")!;
            // Native tile-save metadata records fallback names even when no
            // unloaded tile exists. Supply those built-in singleton names in
            // this isolated harness without registering extra tile IDs.
            foreach(string fallback in new[]{"UnloadedSolidTile","UnloadedChest","UnloadedDresser","UnloadedSupremeFurniture","UnloadedSemiSolidTile","UnloadedNonSolidTile"})
            {
                object placeholder=Activator.CreateInstance(game.GetType("Terraria.ModLoader.Default."+fallback)!)!;
                placeholder.GetType().GetProperty("Mod",All)!.SetValue(placeholder,pulserTile.GetType().GetProperty("Mod")!.GetValue(pulserTile));
                game.GetType("Terraria.ModLoader.ContentInstance")!.GetMethod("Register",All)!.Invoke(null,new[]{placeholder});
            }
            Type serializer=game.GetType("Terraria.ModLoader.IO.TagSerializer")!;
            foreach(var primitive in new[]{("UShortTagSerializer",typeof(ushort)),("BoolTagSerializer",typeof(bool))})
            {
                object?[] lookup={primitive.Item2,null};
                if(!(bool)serializer.GetMethod("TryGetSerializer")!.Invoke(null,lookup)!)
                    serializer.GetMethod("AddSerializer",All)!.Invoke(null,new[]{Activator.CreateInstance(game.GetType("Terraria.ModLoader.IO."+primitive.Item1)!)});
            }
            object tileSave=tileIO.GetMethod("SaveBasics",All)!.Invoke(null,null)!;
            map.GetType().GetMethod("ClearEverything")!.Invoke(map,null);
            tileIO.GetMethod("LoadBasics",All)!.Invoke(null,new[]{tileSave});
            if (TypeAt(32,30)!=pulse || (short)tile.GetProperty("TileFrameX")!.GetValue(Tile(32,30))! !=18 || !Charged(32,29))
                throw new InvalidOperationException("Native mod-tile save/load must restore the inverted frame and output");
            Console.WriteLine("PASS native inverted pulser movement: sticky piston carries the saved frame bit and world clock reset preserves the opposite output");
            Console.WriteLine("PASS actual pulser tile persistence: native TileIO SaveBasics/LoadBasics restores moved source identity, phase bit and inverse power");

            Reset(); Put(40, 40, pulse); Call(redstoneSystem, "OnWorldLoad");
            List<(object Piston, int X, int Y, int Dx, int Dy)> surrounding = new();
            for (int direction = 0; direction < 4; direction++)
            {
                object d = rules.GetMethod("Direction")!.Invoke(null, new object[] { direction })!;
                int dx = (int)gridType.GetProperty("X")!.GetValue(d)!, dy = (int)gridType.GetProperty("Y")!.GetValue(d)!;
                int x = 40 + dx, y = 40 + dy;
                surrounding.Add((Place(x, y, true, direction), x, y, dx, dy));
                Put(x + dx, y + dy, 1);
            }
            for (int tick = 1; tick <= 125; tick++)
            {
                Call(redstoneSystem, "PostUpdateWorld");
                foreach (var p in surrounding)
                    if (Extended(p.Piston) != PulseOn() || TypeAt(p.X + p.Dx, p.Y + p.Dy) != (PulseOn() ? head : 1) ||
                        TypeAt(p.X + p.Dx * 2, p.Y + p.Dy * 2) != (PulseOn() ? 1 : -1))
                        throw new InvalidOperationException("One pulser must repeatedly drive all four real sticky pistons, preserving each block.");
            }
            for (int i = 0; i < 25; i++) Call(redstoneSystem, "PostUpdateWorld");
            int pulseDrops = Drops("RedstonePulser");
            game.GetType("Terraria.WorldGen")!.GetMethod("KillTile")!.Invoke(null, new object[] { 40, 40, false, false, false });
            for (int i = 0; i < 9; i++) Call(redstoneSystem, "PostUpdateWorld");
            if (Has(40, 40) || surrounding.Any(p => Extended(p.Piston)) || Drops("RedstonePulser") != pulseDrops + 1)
                throw new InvalidOperationException("Removing the active source must retract neighboring pistons and drop exactly one pulser.");
            Console.WriteLine("PASS native pulser integration: two complete four-piston sticky cycles without block loss; removing source retracts all and drops one pulser");

            Reset(); object sourceMover = Place(30, 30, false, 1); Put(29, 30, red); Put(31, 30, pulse);
            Tick(sourceMover);
            if (TypeAt(32, 30) != pulse || TypeAt(31, 30) != head || entities.Count != 1)
                throw new InvalidOperationException("A pulser must remain an ordinary pushable single-tile block without a stranded tile entity.");
            for (int direction = 0; direction < 4; direction++)
            {
                Reset(); object d = rules.GetMethod("Direction")!.Invoke(null, new object[] { direction })!;
                int dx = (int)gridType.GetProperty("X")!.GetValue(d)!, dy = (int)gridType.GetProperty("Y")!.GetValue(d)!;
                object piston = Place(40, 40, false, direction); Put(40 - dx, 40 - dy, red);
                for (int i = 1; i <= 12; i++) Put(40 + dx * i, 40 + dy * i, i % 2 == 0 ? 1 : 0);
                Tick(piston);
                if (!Extended(piston) || TypeAt(40 + dx, 40 + dy) != head) throw new InvalidOperationException("Native piston failed to extend in direction " + direction);
                for (int i = 2; i <= 13; i++) if (TypeAt(40 + dx * i, 40 + dy * i) != ((i - 1) % 2 == 0 ? 1 : 0)) throw new InvalidOperationException("A twelve-block push lost order or duplicated blocks.");
                Clear(40 - dx, 40 - dy); Tick(piston);
                if (Extended(piston) || Has(40 + dx, 40 + dy) || TypeAt(40 + dx * 2, 40 + dy * 2) != 0) throw new InvalidOperationException("Normal retraction must leave pushed blocks in place.");
            }
            Console.WriteLine("PASS native piston cardinal movement: twelve ordered blocks move exactly one tile in all four directions; normal retraction never pulls");

            Reset(); object limited = Place(30, 30, false, 1); Put(29, 30, red);
            for (int i = 1; i <= 13; i++) Put(30 + i, 30, 1);
            Tick(limited);
            if (Extended(limited) || Enumerable.Range(1, 13).Any(i => TypeAt(30 + i, 30) != 1)) throw new InvalidOperationException("Thirteen blocks must reject atomically.");
            Clear(43, 30); Put(33, 30, 21); Tick(limited);
            if (Extended(limited) || TypeAt(31, 30) != 1 || TypeAt(33, 30) != 21) throw new InvalidOperationException("Furniture/chests must reject the complete push.");
            Console.WriteLine("PASS native piston rejection: thirteen blocks and furniture leave the complete column untouched");

            Reset(); object adhesivePiston = Place(30, 30, true, 1); Put(29, 30, red); Put(31, 30, 1); Put(32, 30, 0);
            Tick(adhesivePiston); Clear(29, 30); Tick(adhesivePiston);
            if (Extended(adhesivePiston) || TypeAt(31, 30) != 1 || Has(32, 30) || TypeAt(33, 30) != 0)
                throw new InvalidOperationException("A sticky piston must pull only the closest block, not the whole column.");
            Put(29, 30, red); Tick(adhesivePiston); Put(31, 30, 1); Clear(29, 30); Tick(adhesivePiston);
            if (TypeAt(31, 30) != 1 || TypeAt(32, 30) != 1) throw new InvalidOperationException("A missing/replaced head must not authorize deleting its replacement.");
            Console.WriteLine("PASS native sticky piston: one-block pull, no chain pulling, replaced-head foreground preserved");

            // Use actual native item placement to cover platform families/styles
            // and the real furniture framing that runs after a piston commit.
            Type itemType = game.GetType("Terraria.Item")!, itemIds = game.GetType("Terraria.ID.ItemID")!;
            Type tileIds = game.GetType("Terraria.ID.TileID")!;
            bool[] important = (bool[])main.GetField("tileFrameImportant")!.GetValue(null)!;
            bool[] solid = (bool[])main.GetField("tileSolid")!.GetValue(null)!;
            bool[] solidTop = (bool[])main.GetField("tileSolidTop")!.GetValue(null)!;
            bool[] platforms = (bool[])tileIds.GetNestedType("Sets")!.GetField("Platforms")!.GetValue(null)!;
            MethodInfo placeNative = game.GetType("Terraria.WorldGen")!.GetMethod("PlaceTile")!;
            object NativeItem(int id)
            {
                object item = Activator.CreateInstance(itemType)!;
                itemType.GetMethod("SetDefaults", new[] { typeof(int) })!.Invoke(item, new object[] { id });
                return item;
            }
            void PlaceItem(object item, int x, int y)
            {
                int kind = (int)itemType.GetField("createTile")!.GetValue(item)!, style = (int)itemType.GetField("placeStyle")!.GetValue(item)!;
                if (!(bool)placeNative.Invoke(null, new object[] { x, y, kind, true, true, -1, style })! || TypeAt(x, y) != kind)
                    throw new InvalidOperationException("Native item placement failed for tile " + kind + ", style " + style);
            }
            int platformStyles = 0;
            foreach (FieldInfo field in itemIds.GetFields(BindingFlags.Public | BindingFlags.Static).Where(f => f.IsLiteral &&
                (f.Name.Contains("Platform") || f.Name.EndsWith("PlanterBox"))))
            {
                object item = NativeItem(Convert.ToInt32(field.GetRawConstantValue()));
                int kind = (int)itemType.GetField("createTile")!.GetValue(item)!;
                if (kind < 0 || !platforms[kind] && kind != 380) continue;
                Reset(); important[kind] = solid[kind] = solidTop[kind] = true;
                object mover = Place(30, 30, true, 1); Put(29, 30, red); PlaceItem(item, 31, 30);
                short originalStyle = (short)tile.GetProperty("TileFrameY")!.GetValue(Tile(31, 30))!;
                tile.GetProperty("TileColor")!.SetValue(Tile(31, 30), (byte)13);
                Tick(mover);
                if (!Extended(mover) || TypeAt(32, 30) != kind || (short)tile.GetProperty("TileFrameY")!.GetValue(Tile(32, 30))! != originalStyle ||
                    (byte)tile.GetProperty("TileColor")!.GetValue(Tile(32, 30))! != 13)
                    throw new InvalidOperationException("Pushing changed or blocked native platform style: " + field.Name);
                Clear(29, 30); Tick(mover);
                if (Extended(mover) || TypeAt(31, 30) != kind || Has(32, 30) ||
                    (short)tile.GetProperty("TileFrameY")!.GetValue(Tile(31, 30))! != originalStyle)
                    throw new InvalidOperationException("Sticky return lost a platform or changed its material: " + field.Name);
                platformStyles++;
            }
            if (platformStyles < 40) throw new InvalidOperationException("Platform regression must cover the native styles and planter boxes, not only one wood platform.");
            Console.WriteLine($"PASS native platform movement: {platformStyles} actual platform/planter items push and pull, preserving material style and paint");

            foreach (string name in new[] { "Torch", "BlueTorch", "Candle", "Switch", "GrayPressurePlate" })
            {
                Reset(); object item = NativeItem(Convert.ToInt32(itemIds.GetField(name)!.GetRawConstantValue()));
                int kind = (int)itemType.GetField("createTile")!.GetValue(item)!;
                important[kind] = true;
                // Native candles require a table/platform surface, unlike torches.
                ((bool[])main.GetField("tileTable")!.GetValue(null)!)[19] = true;
                for (int x = 30; x <= 34; x++) Put(x, 31, name == "Candle" ? 19 : 1);
                Wall(31, 30, 2); Wall(32, 30, 2);
                object mover = Place(30, 30, true, 1); Put(29, 30, red); PlaceItem(item, 31, 30);
                object beforeFurniture = Read(31, 30);
                Tick(mover);
                if (!Extended(mover) || TypeAt(32, 30) != kind || new[] { "X", "Y" }.Any(p =>
                    !Equals(foreType.GetProperty(p)!.GetValue(beforeFurniture), foreType.GetProperty(p)!.GetValue(Read(32, 30)))))
                    throw new InvalidOperationException("Supported single-cell furniture must move with its color/state frames: " + name);
                Clear(29, 30); Tick(mover);
                if (Extended(mover) || TypeAt(31, 30) != kind || Has(32, 30))
                    throw new InvalidOperationException("Sticky return must retain one complete supported furniture item: " + name);
            }
            Console.WriteLine("PASS native single-cell furniture: torches, colored torches, candles, switches and pressure plates push/pull with native support and state frames");

            Reset(); object longPlatforms = Place(30, 30, false, 1); Put(29, 30, red);
            for (int i = 1; i <= 13; i++) Put(30 + i, 30, 19);
            Tick(longPlatforms);
            if (Extended(longPlatforms) || Enumerable.Range(1, 13).Any(i => TypeAt(30 + i, 30) != 19))
                throw new InvalidOperationException("The thirteen-block limit must still reject a column of platforms without partial movement.");
            foreach (string name in new[] { "Chairs", "WorkBenches", "Containers" })
            {
                Reset(); object blocked = Place(30, 30, false, 1); Put(29, 30, red); Put(31, 30, 19);
                int kind = Convert.ToInt32(tileIds.GetField(name)!.GetRawConstantValue()); important[kind] = true;
                Put(32, 30, kind); object original = Read(32, 30);
                Tick(blocked);
                if (Extended(blocked) || TypeAt(31, 30) != 19 || !Equals(Read(32, 30), original))
                    throw new InvalidOperationException("Multi-tile furniture must stop the complete mixed platform push: " + name);
            }
            Console.WriteLine("PASS native furniture boundaries: thirteen platforms and a mixed column ending at a chair, workbench or chest reject atomically");

            Reset(); object paintPiston = Place(30, 30, false, 1); Put(29, 30, red); Put(31, 30, 1);
            object data = Activator.CreateInstance(foreType, All, null, new object[] { true, (ushort)1, (short)0, (short)0, true,
                Enum.ToObject(game.GetType("Terraria.ID.SlopeType")!, 0), (byte)13, true, true, false, true }, null)!;
            foreType.GetMethod("Write", All)!.Invoke(data, new[] { Tile(31, 30) });
            tile.GetProperty("RedWire")!.SetValue(Tile(31, 30), true);
            Wall(31, 30, 2); Wall(32, 30, 3);
            Water(31, 30, 96); Water(32, 30, 64);
            Tick(paintPiston);
            object moved = Read(32, 30);
            if (new[] { "Has", "Type", "Half", "Slope", "Color", "Invisible", "Fullbright", "Actuated", "Actuator" }.Any(p =>
                !Equals(foreType.GetProperty(p)!.GetValue(moved), foreType.GetProperty(p)!.GetValue(data))) || !(bool)tile.GetProperty("RedWire")!.GetValue(Tile(31, 30))! ||
                (bool)tile.GetProperty("RedWire")!.GetValue(Tile(32, 30))! ||
                WallAt(31, 30) != 2 || WallAt(32, 30) != 3 || WaterAt(31, 30) != 96 || WaterAt(32, 30) != 64)
                throw new InvalidOperationException("Foreground paint/coatings/shape/actuator must move while walls and wires stay at their positions.");
            Console.WriteLine("PASS native piston payload: shape, paint, coatings and actuator move intact; walls, liquids and wiring stay fixed");

            Reset(); object first = Place(30, 30, false, 1), second = Place(31, 30, false, 0); Put(29, 30, red);
            Tick(first);
            if (!Extended(first) || !positions.Contains(second.GetType().GetField("Position")!.GetValue(second)!) ||
                !second.GetType().GetField("Position")!.GetValue(second)!.Equals(Activator.CreateInstance(game.GetType("Terraria.DataStructures.Point16")!, new object[] { 32, 30 })))
                throw new InvalidOperationException("A closed one-block piston must migrate its tile entity with its block.");
            Console.WriteLine("PASS native moving piston: closed base and tile entity relocate together, retaining orientation");

            Reset(); object pushing = Place(30, 30, false, 1); Put(29, 30, red); Put(31, 30, 1);
            object player = players.GetValue(0)!; Type playerType = player.GetType();
            object initial = Activator.CreateInstance(playerType.GetField("position")!.FieldType, new object[] { 512f, 480f })!;
            playerType.GetField("position")!.SetValue(player, initial); playerType.GetField("active")!.SetValue(player, true);
            playerType.GetField("dead")!.SetValue(player, false); playerType.GetField("ghost")!.SetValue(player, false);
            Tick(pushing);
            object movedPlayer = playerType.GetField("position")!.GetValue(player)!;
            if (!Extended(pushing) || (float)movedPlayer.GetType().GetField("X")!.GetValue(movedPlayer)! != 528f)
                throw new InvalidOperationException("A moved block must displace the player safely by one tile.");
            playerType.GetField("active")!.SetValue(player, false);
            Reset(); pushing = Place(30, 30, false, 1); Put(29, 30, red); Put(31, 30, 1); Put(33, 30, 1);
            playerType.GetField("position")!.SetValue(player, initial); playerType.GetField("active")!.SetValue(player, true);
            Tick(pushing);
            if (Extended(pushing) || TypeAt(31, 30) != 1 || Has(32, 30) || !playerType.GetField("position")!.GetValue(player)!.Equals(initial))
                throw new InvalidOperationException("A trapped player must block the complete transaction without crushing or partial movement.");
            playerType.GetField("active")!.SetValue(player, false);
            Console.WriteLine("PASS native piston actor safety: unobstructed player displacement succeeds, blocked displacement preserves the entire map");

            Reset(); object breaking = Place(30, 30, false, 1); Put(29, 30, red); Tick(breaking);
            FieldInfo killHooks = game.GetType("Terraria.ModLoader.TileLoader")!.GetField("HookKillTile", All)!;
            object savedHooks = killHooks.GetValue(null)!;
            Type callbackType = killHooks.FieldType.GetElementType()!;
            MethodInfo callbackSignature = callbackType.GetMethod("Invoke")!;
            var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("PistonProtection" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
            var shim = assembly.DefineDynamicModule("Protection").DefineType("Protection", TypeAttributes.Public);
            var method = shim.DefineMethod("DenyBase", MethodAttributes.Public | MethodAttributes.Static, typeof(void), callbackSignature.GetParameters().Select(p => p.ParameterType).ToArray());
            var il = method.GetILGenerator(); var done = il.DefineLabel();
            il.Emit(OpCodes.Ldarg_2); il.Emit(OpCodes.Ldc_I4, normal); il.Emit(OpCodes.Bne_Un_S, done);
            il.Emit(OpCodes.Ldarg_3); il.Emit(OpCodes.Ldc_I4_1); il.Emit(OpCodes.Stind_I1); il.MarkLabel(done); il.Emit(OpCodes.Ret);
            Array protectedHooks = Array.CreateInstance(callbackType, 1);
            protectedHooks.SetValue(shim.CreateType()!.GetMethod("DenyBase")!.CreateDelegate(callbackType), 0);
            killHooks.SetValue(null, protectedHooks);
            try
            {
                game.GetType("Terraria.WorldGen")!.GetMethod("KillTile")!.Invoke(null, new object[] { 30, 30, false, false, false });
                game.GetType("Terraria.WorldGen")!.GetMethod("KillTile")!.Invoke(null, new object[] { 31, 30, false, false, false });
                if (!Has(30, 30) || TypeAt(31, 30) != head || !entities.Contains((int)breaking.GetType().GetField("ID")!.GetValue(breaking)!))
                    throw new InvalidOperationException("A final global protection veto must retain the complete piston, restoring the head if its base cannot break.");
            }
            finally { killHooks.SetValue(null, savedHooks); }
            int beforeDrop = Drops("Piston");
            game.GetType("Terraria.WorldGen")!.GetMethod("KillTile")!.Invoke(null, new object[] { 31, 30, false, false, false });
            if (Has(30, 30) || Has(31, 30) || entities.Contains((int)breaking.GetType().GetField("ID")!.GetValue(breaking)!) || Drops("Piston") != beforeDrop + 1)
                throw new InvalidOperationException("Breaking an extended head must remove the owning base and tile entity.");
            Console.WriteLine("PASS native piston head destruction: final protection veto retains machine; successful break removes head/base/entity and drops exactly one piston");

            Reset(); object overlapPiston = Place(28, 48, true, 1);
            for (int x = 29; x <= 31; x++) Put(x, 50, 1);
            if (!(bool)game.GetType("Terraria.WorldGen")!.GetMethod("PlaceObject")!.Invoke(null, new object[] { 30, 49, boatId, true, 0, 0, -1, -1 })!) throw new InvalidOperationException("Overlay boat setup failed.");
            int boatIndex = (int)Call(boatTemplate, "Place", 29, 48)!;
            object boat = entities[boatIndex]!; Call(boat, "Reconcile");
            Type npcType = game.GetType("Terraria.NPC")!;
            object passenger = Activator.CreateInstance(npcType)!;
            MethodInfo defaults = npcType.GetMethods().Single(m => m.Name == "SetDefaults" && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(int));
            defaults.Invoke(passenger, new[] { (object)1, Activator.CreateInstance(defaults.GetParameters()[1].ParameterType)! });
            npcType.GetField("active")!.SetValue(passenger, true); npcType.GetField("whoAmI")!.SetValue(passenger, 0);
            npcType.GetProperty("Bottom")!.SetValue(passenger, Activator.CreateInstance(npcType.GetField("position")!.FieldType, new object[] { 488f, 800f }));
            npcs.SetValue(passenger, 0);
            if (!(bool)Call(boat, "TryCapture", passenger)!) throw new InvalidOperationException("Passenger setup for the overlapping boat failed.");
            Put(27, 48, red); Tick(overlapPiston); Call(boat, "Reconcile");
            if (!Extended(overlapPiston) || TypeAt(29, 48) != head || !(bool)boat.GetType().GetProperty("Live", All)!.GetValue(boat)! ||
                !(bool)boat.GetType().GetField("Overlay", All)!.GetValue(boat)!) throw new InvalidOperationException("Piston head must coexist with a live retained boat, not destroy it.");
            if (!ReferenceEquals(boat.GetType().GetField("Occupant", All)!.GetValue(boat), passenger))
                throw new InvalidOperationException("The boat must retain its passenger while a head passes through its footprint.");
            float hullDepth = (float)code.GetType("DimensionalTerraria.Rendering.WorldMeshBuilder")!.GetField("OverlayBoatDepth", All)!.GetRawConstantValue()!;
            float pistonFront = (float)code.GetType("DimensionalTerraria.Core.SpatialRules")!.GetField("FrontMaxZ")!.GetRawConstantValue()!;
            MethodInfo creatureDepth = code.GetType("DimensionalTerraria.Rendering.CreatureRenderer")!.GetMethod("SpriteDepth", All)!;
            float RiderDepth() => (float)creatureDepth.Invoke(null, new[] { passenger })!;
            bool BoatBehind() => (bool)boatSystem.GetMethod("BehindTiles", All)!.Invoke(null, new[] { boat })!;
            if (!(pistonFront < hullDepth && hullDepth < RiderDepth()) || BoatBehind())
                throw new InvalidOperationException("An overlapping boat must be in front of the head and behind its rider, in the rider's 2D pass.");
            float stableSurface = (float)boat.GetType().GetProperty("DrawSurface", All)!.GetValue(boat)!;
            void StableBoatDraw()
            {
                object[] visible = ((IEnumerable)boatSystem.GetMethod("RenderBoats", All)!.Invoke(null, null)!).Cast<object>().ToArray();
                if (visible.Length != 1 || !ReferenceEquals(visible[0], boat) ||
                    (float)boat.GetType().GetProperty("DrawSurface", All)!.GetValue(boat)! != stableSurface ||
                    !ReferenceEquals(boat.GetType().GetField("Occupant", All)!.GetValue(boat), passenger))
                    throw new InvalidOperationException("Boat visual identity, surface and passenger must remain continuous through retraction.");
                if ((bool)Call(boatTile, "PreDraw", 29, 48, null)!)
                    throw new InvalidOperationException("Physical boats must not switch back to a cached tile draw path.");
            }
            for (int cycle = 0; cycle < 16; cycle++)
            {
                StableBoatDraw();
                Clear(27, 48); Tick(overlapPiston); StableBoatDraw();
                Call(boat, "Reconcile"); StableBoatDraw();
                Put(27, 48, red); Tick(overlapPiston); StableBoatDraw();
                Call(boat, "Reconcile"); StableBoatDraw();
            }
            Console.WriteLine("PASS native boat render continuity: 16 piston cycles keep one live hull, identical draw surface and rider before/after representation changes; cached native drawing stays disabled");
            npcType.GetField("behindTiles")!.SetValue(passenger, true);
            if (!BoatBehind() || hullDepth >= RiderDepth())
                throw new InvalidOperationException("Normally-behind-tile riders must select their native 2D pass and sit above the boat in 3D.");
            object storage = Activator.CreateInstance(boatSystem)!;
            object tag = Activator.CreateInstance(game.GetType("Terraria.ModLoader.IO.TagCompound")!)!;
            Call(storage, "SaveWorldData", tag); Call(storage, "OnWorldUnload"); Call(storage, "LoadWorldData", tag);
            if (RiderDepth() != 0 || !(bool)npcType.GetField("behindTiles")!.GetValue(passenger)!)
                throw new InvalidOperationException("Releasing the boat must restore the species' native depth without mutating its behindTiles flag.");
            npcType.GetField("behindTiles")!.SetValue(passenger, false);
            Console.WriteLine("PASS native boat layer lifecycle: head below hull below rider, native 2D pass selected, behind-tile species restore depth on release");
            object restored = boatSystem.GetMethod("FindAt", All)!.Invoke(null, new object[] { 29, 48 })!;
            if (restored is null || !(bool)restored.GetType().GetProperty("Live", All)!.GetValue(restored)!) throw new InvalidOperationException("Overlapping boat save/load lost the retained boat.");
            Clear(27, 48); Tick(overlapPiston); Call(restored, "Reconcile");
            if ((bool)restored.GetType().GetField("Overlay", All)!.GetValue(restored)! || TypeAt(29, 48) != boatId ||
                !(bool)restored.GetType().GetProperty("Live", All)!.GetValue(restored)!) throw new InvalidOperationException("A boat must restore all physical frames after the head leaves.");
            Call(restored, "BecomeOverlay"); Put(29, 48, 1); int beforeBoatDrop = Drops("OakBoat"); Call(restored, "Break"); Call(restored, "Break");
            if (TypeAt(29, 48) != 1 || (bool)restored.GetType().GetProperty("Live", All)!.GetValue(restored)!)
                throw new InvalidOperationException("Breaking an overlay boat cannot destroy the foreground it overlaps.");
            if (Drops("OakBoat") != beforeBoatDrop + 1) throw new InvalidOperationException("An overlay boat must drop exactly one boat even after repeated breaks.");
            Console.WriteLine("PASS native boat overlap: piston passes through without boat loss; independent save/load, physical restoration and isolated boat break work");
        }
        finally
        {
            Call(redstoneSystem, "OnWorldUnload");
            Call(redstoneSystem, "Unload");
            Call(boatLayers, "Unload");
            Reset(); Array.Copy(savedNpcs, npcs, npcs.Length);
            foreach (var actor in actors) actor.Value.GetType().GetField("active")!.SetValue(actor.Value, actor.Active);
            for (int i = 0; i < runtimeFields.Length; i++) runtimeFields[i].SetValue(null, savedRuntime[i]);
        }
    }
}
