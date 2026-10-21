using System.Reflection;
using System.Collections;

internal static class BoatChecks
{
    const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public;
    internal static void Run(Assembly game, Assembly code, object boatTile, object boatItem, object entityTemplate, object captiveTemplate)
    {
        Type main = game.GetType("Terraria.Main")!, tiles = game.GetType("Terraria.ObjectData.TileObjectData")!;
        Type tileEntity = game.GetType("Terraria.DataStructures.TileEntity")!, npcType = game.GetType("Terraria.NPC")!;
        Type tile = game.GetType("Terraria.Tile")!, worldGen = game.GetType("Terraria.WorldGen")!;
        foreach (string arrayName in new[] { "player", "item" })
        {
            Array entries = (Array)main.GetField(arrayName)!.GetValue(null)!;
            for (int i = 0; i < entries.Length; i++) if (entries.GetValue(i) is null)
                entries.SetValue(Activator.CreateInstance(entries.GetType().GetElementType()!), i);
        }
        int tileId = Convert.ToInt32(boatTile.GetType().GetProperty("Type")!.GetValue(boatTile));
        object map = main.GetField("tile")!.GetValue(null)!;
        map.GetType().GetMethod("ClearEverything")!.Invoke(map, null);
        tiles.GetMethod("Initialize")!.Invoke(null, null);
        // This isolated test already resized SetFactory once for item recipes.
        // Enlarge Main's ordinary tile arrays without reinitializing all ID factories.
        int tileCount = (int)game.GetType("Terraria.ModLoader.TileLoader")!.GetProperty("TileCount")!.GetValue(null)!;
        foreach (FieldInfo field in main.GetFields(BindingFlags.Public | BindingFlags.Static))
            if (field.Name.StartsWith("tile") && field.GetValue(null) is Array values && values.Rank == 1 && values.Length == Convert.ToInt32(game.GetType("Terraria.ID.TileID")!.GetField("Count")!.GetValue(null)))
            {
                Array enlarged = Array.CreateInstance(field.FieldType.GetElementType()!, tileCount);
                Array.Copy(values, enlarged, values.Length); field.SetValue(null, enlarged);
            }
        IList objectData = (IList)tiles.GetField("_data", Hidden)!.GetValue(null)!;
        while (objectData.Count < tileCount) objectData.Add(null);
        foreach (FieldInfo field in game.GetType("Terraria.ModLoader.TileLoader")!.GetFields(BindingFlags.NonPublic | BindingFlags.Static))
            if (field.FieldType.IsArray && typeof(Delegate).IsAssignableFrom(field.FieldType.GetElementType()) && field.GetValue(null) is null)
                field.SetValue(null, Array.CreateInstance(field.FieldType.GetElementType()!, 0));
        boatTile.GetType().GetMethod("SetStaticDefaults")!.Invoke(boatTile, null);
        object data = tiles.GetMethod("GetTileData", new[] { typeof(int), typeof(int), typeof(int) })!.Invoke(null, new object[] { tileId, 0, 0 })!;
        if ((int)tiles.GetProperty("Width")!.GetValue(data)! != 3 || (int)tiles.GetProperty("Height")!.GetValue(data)! != 2 ||
            !((bool[])main.GetField("tileCut")!.GetValue(null)!)[tileId] || ((bool[])main.GetField("tileSolid")!.GetValue(null)!)[tileId])
            throw new InvalidOperationException("Boat must be a fragile, non-solid 3x2 native multi-tile.");
        Console.WriteLine("PASS native boat registration: non-solid, cuttable 3x2 multi-tile, no land-only anchor");
        object Tile(int x, int y) => map.GetType().GetProperty("Item", new[] { typeof(int), typeof(int) })!.GetValue(map, new object[] { x, y })!;
        for (int x = 29; x <= 31; x++) tile.GetMethod("ResetToType")!.Invoke(Tile(x, 50), new object[] { (ushort)1 });
        ((bool[])main.GetField("tileSolid")!.GetValue(null)!)[1] = true;
        var place = worldGen.GetMethod("PlaceObject")!;
        bool Place() => (bool)place.Invoke(null, new object[] { 30, 49, tileId, true, 0, 0, -1, -1 })!;
        if (!Place()) throw new InvalidOperationException("Native boat object placement failed on clear supported ground.");
        int id = (int)entityTemplate.GetType().GetMethod("Place")!.Invoke(entityTemplate, new object[] { 29, 48 })!;
        IDictionary entities = (IDictionary)tileEntity.GetField("ByID")!.GetValue(null)!;
        object boat = entities[id]!;
        object? Call(string method, params object?[] arguments) => boat.GetType().GetMethod(method, Hidden)!.Invoke(boat, arguments);
        Call("Reconcile");
        if (!(bool)boat.GetType().GetProperty("Ready", Hidden)!.GetValue(boat)! ||
            !(bool)boat.GetType().GetProperty("Live", Hidden)!.GetValue(boat)!)
            throw new InvalidOperationException("A placed boat must have exactly six correct frames and one entity.");
        object placement = Activator.CreateInstance(code.GetType("DimensionalTerraria.Core.BoatPlacement")!, new object[] { 29, 48, 50f })!;
        if ((bool)boatItem.GetType().GetMethod("TryPlace", Hidden)!.Invoke(boatItem, new object?[] { Activator.CreateInstance(game.GetType("Terraria.Player")!), placement, null })!)
            throw new InvalidOperationException("The boat item must reject overlapping placement before calling the native cuttable replacement path.");
        Console.WriteLine("PASS native boat placement: actual PlaceObject frames validate, duplicate placement rejected");
        object toolUser = Activator.CreateInstance(game.GetType("Terraria.Player")!)!;
        Array inventory = (Array)toolUser.GetType().GetField("inventory")!.GetValue(toolUser)!;
        Type ray = code.GetType("DimensionalTerraria.Core.TileRayHit")!;
        object hit = Activator.CreateInstance(ray, new[] { (object)30, 48, Enum.Parse(code.GetType("DimensionalTerraria.Core.RayLayer")!, "Tile"), 1f,
            Activator.CreateInstance(ray.GetProperty("Min")!.PropertyType), Activator.CreateInstance(ray.GetProperty("Max")!.PropertyType) })!;
        foreach (string name in new[] { "CopperPickaxe", "CopperAxe", "WoodenHammer", "WoodenSword" })
        {
            object tool = Activator.CreateInstance(game.GetType("Terraria.Item")!)!;
            tool.GetType().GetMethod("SetDefaults", new[] { typeof(int) })!.Invoke(tool, new[] { game.GetType("Terraria.ID.ItemID")!.GetField(name)!.GetValue(null) });
            inventory.SetValue(tool, 0);
            if (!(bool)code.GetType("DimensionalTerraria.World.NativeInteractions")!.GetMethod("HasTool")!.Invoke(null, new[] { toolUser, hit })!)
                throw new InvalidOperationException("Fragile boats must accept this native tool or weapon: " + name);
        }
        Console.WriteLine("PASS native boat tools: pickaxe, axe, hammer and ordinary sword all accepted");

        // Use real per-NPC global slots, without running unrelated NPC content setup.
        Type globalBase = game.GetType("Terraria.ModLoader.GlobalNPC")!;
        game.GetType("Terraria.ModLoader.GlobalList`1")!.MakeGenericType(globalBase)
            .GetMethod("FinishLoading", Hidden)!.Invoke(null, new[] { game.GetType("Terraria.ModLoader.NPCLoader")!.GetProperty("NPCCount")!.GetValue(null) });
        int perEntity = Convert.ToInt32(captiveTemplate.GetType().GetProperty("PerEntityIndex", Hidden)!.GetValue(captiveTemplate));
        FieldInfo globalSlots = npcType.GetField("_globals", Hidden)!;
        object CreateNpc(int slot)
        {
            object npc = Activator.CreateInstance(npcType)!;
            foreach (var pair in new[] { ("type", 1), ("whoAmI", slot), ("life", 50), ("lifeMax", 50), ("width", 32), ("height", 24), ("realLife", -1), ("damage", 12) })
                npcType.GetField(pair.Item1)!.SetValue(npc, pair.Item2);
            npcType.GetField("active")!.SetValue(npc, true);
            var point = npcType.GetField("position")!.FieldType;
            npcType.GetField("position")!.SetValue(npc, Activator.CreateInstance(point, new object[] { 478f, 770f }));
            Array globals = Array.CreateInstance(globalBase, perEntity + 1);
            globals.SetValue(Activator.CreateInstance(captiveTemplate.GetType()), perEntity);
            globalSlots.SetValue(npc, globals);
            ((Array)main.GetField("npc")!.GetValue(null)!).SetValue(npc, slot);
            return npc;
        }
        object passenger = CreateNpc(0), second = CreateNpc(1);
        MethodInfo eligible = captiveTemplate.GetType().GetMethod("Eligible", Hidden)!;
        bool Eligible(object candidate) => (bool)eligible.Invoke(null, new[] { candidate })!;
        foreach (string style in new[] { "Worm", "TheDestroyer", "SkeletronHand", "GolemHead", "MoonLordHand" })
        {
            npcType.GetField("aiStyle")!.SetValue(second, Convert.ToInt32(game.GetType("Terraria.ID.NPCAIStyleID")!.GetField(style)!.GetValue(null)));
            if (Eligible(second)) throw new InvalidOperationException("A worm head or boss attachment must not be capturable: " + style);
        }
        npcType.GetField("aiStyle")!.SetValue(second, 0);
        npcType.GetField("realLife")!.SetValue(second, 0);
        if (Eligible(passenger) || Eligible(second)) throw new InvalidOperationException("Both linked body and its head must be rejected, even with a head realLife of -1.");
        npcType.GetField("realLife")!.SetValue(second, -1);
        npcType.GetField("boss")!.SetValue(second, true);
        npcType.GetField("noGravity")!.SetValue(second, true);
        if (Eligible(second)) throw new InvalidOperationException("Flying bosses remain outside the terrestrial capture rules.");
        npcType.GetField("noGravity")!.SetValue(second, false);
        npcType.GetField("boss")!.SetValue(second, false);
        Console.WriteLine("PASS native capture exclusions: worm heads, shared-health heads and bodies, boss attachments and flying bosses remain excluded");
        if (!(bool)Call("TryCapture", passenger)! || (bool)Call("TryCapture", second)!)
            throw new InvalidOperationException("An empty boat must capture one passing creature and reject a second passenger.");
        object state = ((Array)globalSlots.GetValue(passenger)!).GetValue(perEntity)!;
        object[] contact = { Activator.CreateInstance(game.GetType("Terraria.Player")!)!, 0 };
        if ((bool)state.GetType().GetMethod("CanHitPlayer")!.Invoke(state, new[] { passenger, contact[0], contact[1] })! ||
            (bool)state.GetType().GetMethod("CanHitNPC")!.Invoke(state, new[] { passenger, second })! ||
            (bool)state.GetType().GetMethod("CheckActive")!.Invoke(state, new[] { passenger })!)
            throw new InvalidOperationException("Captured NPCs cannot attack players/NPCs or despawn by distance.");
        if (!(bool)code.GetType("DimensionalTerraria.Systems.BoatCaptureSystem")!.GetMethod("HoldAI", Hidden)!.Invoke(null, new[] { passenger })!)
            throw new InvalidOperationException("Captured NPC AI must be suppressed in both dimensions.");
        if ((int)npcType.GetField("damage")!.GetValue(passenger)! != 12 || (bool)npcType.GetField("friendly")!.GetValue(passenger)!)
            throw new InvalidOperationException("Capturing cannot permanently rewrite native NPC damage or faction.");
        Console.WriteLine("PASS real NPC capture: one passenger, AI held, contact attacks and despawn denied, native stats intact");

        // Refuse the old slot's state if the engine resets/reuses that NPC object.
        Array originalGlobals = (Array)globalSlots.GetValue(passenger)!;
        Array reusedGlobals = Array.CreateInstance(globalBase, perEntity + 1);
        reusedGlobals.SetValue(Activator.CreateInstance(captiveTemplate.GetType()), perEntity);
        globalSlots.SetValue(passenger, reusedGlobals);
        FieldInfo velocity = npcType.GetField("velocity")!;
        object moving = Activator.CreateInstance(velocity.FieldType, new object[] { 2f, 3f })!;
        velocity.SetValue(passenger, moving); Call("Reconcile");
        if (!velocity.GetValue(passenger)!.Equals(moving) || boat.GetType().GetField("Occupant", Hidden)!.GetValue(boat) is not null)
            throw new InvalidOperationException("An old passenger reference must not freeze or reset a newly reused NPC slot.");
        globalSlots.SetValue(passenger, originalGlobals);
        state.GetType().GetField("Cooldown", Hidden)!.SetValue(state, 0);
        if (!(bool)Call("TryCapture", passenger)!) throw new InvalidOperationException("Recapture setup failed.");
        Console.WriteLine("PASS native NPC-slot reuse: stale capture cleared without changing the replacement creature's velocity");

        // Break a non-origin tile through the engine, not by manually clearing data.
        worldGen.GetMethod("KillTile")!.Invoke(null, new object[] { 31, 49, false, false, false });
        for (int x = 29; x <= 31; x++) for (int y = 48; y <= 49; y++)
            if ((bool)tile.GetProperty("HasTile")!.GetValue(Tile(x, y))!) throw new InvalidOperationException("Breaking any boat part must remove the entire multi-tile.");
        if (entities.Contains(id) || state.GetType().GetField("Host", Hidden)!.GetValue(state) is not null ||
            !(bool)state.GetType().GetMethod("CanHitNPC")!.Invoke(state, new[] { passenger, second })! ||
            (int)state.GetType().GetField("Cooldown", Hidden)!.GetValue(state)! != 60)
            throw new InvalidOperationException("Boat destruction must remove the entity, release its NPC and prevent immediate recapture.");
        Console.WriteLine("PASS native boat destruction: all six tiles removed, entity removed, passenger released with recapture cooldown");
        int itemId = Convert.ToInt32(boatItem.GetType().GetProperty("Type")!.GetValue(boatItem));
        int drops = ((Array)main.GetField("item")!.GetValue(null)!).Cast<object>()
            .Where(i => (bool)i.GetType().GetField("active")!.GetValue(i)! && (int)i.GetType().GetField("type")!.GetValue(i)! == itemId)
            .Sum(i => (int)i.GetType().GetField("stack")!.GetValue(i)!);
        if (drops != 1) throw new InvalidOperationException("Breaking a six-tile boat must drop exactly one reusable boat item, not six.");
        Console.WriteLine("PASS native boat drop: exactly one reusable item from the complete multi-tile");

        if (!Place()) throw new InvalidOperationException("Large-passenger boat setup failed.");
        int largeId = (int)entityTemplate.GetType().GetMethod("Place")!.Invoke(entityTemplate, new object[] { 29, 48 })!;
        object largeBoat = entities[largeId]!;
        object? LargeCall(string method, params object?[] values) => largeBoat.GetType().GetMethod(method, Hidden)!.Invoke(largeBoat, values);
        LargeCall("Reconcile");
        object golemNpc = CreateNpc(2);
        object golem = Activator.CreateInstance(code.GetType("DimensionalTerraria.NPCs.MinecraftIronGolem")!)!;
        golem.GetType().GetProperty("Entity", Hidden)!.SetValue(golem, golemNpc);
        npcType.GetProperty("ModNPC")!.SetValue(golemNpc, golem);
        golem.GetType().GetMethod("SetDefaults")!.Invoke(golem, null);
        npcType.GetProperty("Bottom")!.SetValue(golemNpc, Activator.CreateInstance(velocity.FieldType, new object[] { 488f, 800f }));
        tile.GetMethod("ResetToType")!.Invoke(Tile(30, 44), new object[] { (ushort)1 });
        if ((bool)LargeCall("TryCapture", golemNpc)!) throw new InvalidOperationException("The complete golem body must fit, not only the boat footprint.");
        tile.GetMethod("ClearTile")!.Invoke(Tile(30, 44), null);
        if (!(bool)LargeCall("TryCapture", golemNpc)! ||
            !(bool)code.GetType("DimensionalTerraria.Systems.BoatCaptureSystem")!.GetMethod("HoldAI", Hidden)!.Invoke(null, new[] { golemNpc })! ||
            (int)npcType.GetField("width")!.GetValue(golemNpc)! != 44 || (int)npcType.GetField("height")!.GetValue(golemNpc)! != 76)
            throw new InvalidOperationException("Real iron golems must board without shrinking and have their uppercut AI paused.");
        LargeCall("Release");
        Console.WriteLine("PASS real iron golem capture: full 44x76 body retained, low ceiling rejected, AI held and released");
        object giant = CreateNpc(3);
        npcType.GetField("width")!.SetValue(giant, 200); npcType.GetField("height")!.SetValue(giant, 160);
        npcType.GetProperty("Bottom")!.SetValue(giant, Activator.CreateInstance(velocity.FieldType, new object[] { 488f, 800f }));
        if (!(bool)LargeCall("TryCapture", giant)!) throw new InvalidOperationException("Ordinary large passengers must not inherit the former small-creature cap.");
        npcType.GetField("dontTakeDamage")!.SetValue(giant, true); LargeCall("Pin");
        if (largeBoat.GetType().GetField("Occupant", Hidden)!.GetValue(largeBoat) is not null)
            throw new InvalidOperationException("Changing into a protected state must release a captured creature.");
        npcType.GetField("dontTakeDamage")!.SetValue(giant, false);
        object giantState = ((Array)globalSlots.GetValue(giant)!).GetValue(perEntity)!;
        giantState.GetType().GetField("Cooldown", Hidden)!.SetValue(giantState, 0);
        npcType.GetField("height")!.SetValue(giant, 900);
        npcType.GetProperty("Bottom")!.SetValue(giant, Activator.CreateInstance(velocity.FieldType, new object[] { 488f, 800f }));
        if ((bool)LargeCall("TryCapture", giant)!) throw new InvalidOperationException("A giant whose body extends beyond the world must be rejected.");
        worldGen.GetMethod("KillTile")!.Invoke(null, new object[] { 31, 49, false, false, false });
        Console.WriteLine("PASS large capture boundaries: 200x160 accepted, world border rejected, changed eligibility releases immediately");
        BoatDamageChecks.Run(game, code, () => {
            if (!Place()) throw new InvalidOperationException("Damage-test boat setup failed.");
            int damageId = (int)entityTemplate.GetType().GetMethod("Place")!.Invoke(entityTemplate, new object[] { 29, 48 })!;
            object damageBoat = entities[damageId]!;
            damageBoat.GetType().GetMethod("Reconcile", Hidden)!.Invoke(damageBoat, null);
            object passengerToHit = CreateNpc(4);
            if (!(bool)damageBoat.GetType().GetMethod("TryCapture", Hidden)!.Invoke(damageBoat, new[] { passengerToHit })!)
                throw new InvalidOperationException("Damage-test passenger setup failed.");
            return (damageBoat, passengerToHit, damageId);
        });

        BoatPlacementChecks.Run(game, code, boatItem);

        KingSlimeBoatChecks.Run(game, code, () => {
            if (!Place()) throw new InvalidOperationException("King Slime trial boat setup failed.");
            int trialId = (int)entityTemplate.GetType().GetMethod("Place")!.Invoke(entityTemplate, new object[] { 29, 48 })!;
            object trialBoat = entities[trialId]!;
            trialBoat.GetType().GetMethod("Reconcile", Hidden)!.Invoke(trialBoat, null);
            return trialBoat;
        });

        Type liquidData = game.GetType("Terraria.LiquidData")!;
        Array liquids = (Array)map.GetType().GetMethod("GetData")!.MakeGenericMethod(liquidData).Invoke(map, null)!;
        for (int x = 29; x <= 31; x++)
        {
            tile.GetMethod("ClearTile")!.Invoke(Tile(x, 50), null);
            object fluid = liquids.GetValue(x * 100 + 50)!;
            liquidData.GetField("Amount")!.SetValue(fluid, (byte)128); liquids.SetValue(fluid, x * 100 + 50);
        }
        if (!Place()) throw new InvalidOperationException("Native boat placement must work over partial liquid.");
        int floatingId = (int)entityTemplate.GetType().GetMethod("Place")!.Invoke(entityTemplate, new object[] { 29, 48 })!;
        object floating = entities[floatingId]!;
        floating.GetType().GetMethod("Reconcile", Hidden)!.Invoke(floating, null);
        object[] draw = { 29, 48, 16, 0, 16, (short)0, (short)0 };
        boatTile.GetType().GetMethod("SetDrawPositions")!.Invoke(boatTile, draw);
        if (!(bool)floating.GetType().GetProperty("Ready", Hidden)!.GetValue(floating)! || (int)draw[3] is < 7 or > 8)
            throw new InvalidOperationException("Floating boat visual offset must follow the actual partial-liquid surface.");
        for (int x = 29; x <= 31; x++) liquids.SetValue(Activator.CreateInstance(liquidData), x * 100 + 50);
        floating.GetType().GetMethod("Reconcile", Hidden)!.Invoke(floating, null);
        if (entities.Contains(floatingId)) throw new InvalidOperationException("A drained surface must cleanly remove its unsupported boat entity.");
        Console.WriteLine("PASS native floating boat: partial water supports placement and drawing; drained support removes the boat");

        // The real chunk stamp must react to liquid flow/type changes even with no foreground tile.
        int index = 30 * 100 + 50;
        MethodInfo stamp = code.GetType("DimensionalTerraria.Rendering.WorldMeshBuilder")!.GetMethod("ContentStamp")!;
        object key = Activator.CreateInstance(stamp.GetParameters()[0].ParameterType, new object[] { 1, 2 })!;
        int Stamp() => (int)stamp.Invoke(null, new object?[] { key, null })!;
        int dry = Stamp(); object water = liquids.GetValue(index)!;
        liquidData.GetField("Amount")!.SetValue(water, (byte)255); liquids.SetValue(water, index);
        int wet = Stamp();
        liquidData.GetField("typeAndFlags", Hidden)!.SetValue(water, (byte)1); liquids.SetValue(water, index);
        int lava = Stamp();
        if (dry == wet || wet == lava) throw new InvalidOperationException("Chunk cache must detect liquid amount and kind changes.");
        Console.WriteLine("PASS real liquid chunk invalidation: amount and type changes refresh cached geometry");
    }
}
