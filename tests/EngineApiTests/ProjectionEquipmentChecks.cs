using System.Reflection;

internal static class ProjectionEquipmentChecks
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    internal static void Run(Assembly game, Assembly code, object bedrock, object block, object pick, object helmet, object chest, object legs)
    {
        Type main = game.GetType("Terraria.Main")!, itemType = game.GetType("Terraria.Item")!, playerType = game.GetType("Terraria.Player")!;
        Type worldGen = game.GetType("Terraria.WorldGen")!, tileType = game.GetType("Terraria.Tile")!;
        object? Call(object obj, string name, params object?[] args) => obj.GetType().GetMethod(name, All)!.Invoke(obj, args);
        object? Get(object obj, string name) => obj.GetType().GetField(name, All)!.GetValue(obj);
        void Set(object obj, string name, object value) => obj.GetType().GetField(name, All)!.SetValue(obj, value);
        int Id(object obj) => Convert.ToInt32(obj.GetType().GetProperty("Type")!.GetValue(obj));
        void Check(bool value, string reason) { if (!value) throw new InvalidOperationException(reason); }
        object Item(int id)
        {
            object item = Activator.CreateInstance(itemType)!;
            itemType.GetMethod("SetDefaults", new[] { typeof(int) })!.Invoke(item, new object[] { id }); return item;
        }
        main.GetField("dedServ")!.SetValue(null, true);
        object[] armor = { helmet, chest, legs };
        string[] solarNames = { "SolarFlareHelmet", "SolarFlareBreastplate", "SolarFlareLeggings" };
        string[] slots = { "headSlot", "bodySlot", "legSlot" };
        int total = 0;
        for (int i = 0; i < armor.Length; i++)
        {
            object worn = Item(Id(armor[i]));
            object solar = Item(Convert.ToInt32(game.GetType("Terraria.ID.ItemID")!.GetField(solarNames[i])!.GetValue(null)));
            Check((int)Get(worn, "defense")! == (int)Get(solar, "defense")!, "Armor must match each Solar Flare piece's base defense");
            Check(Convert.ToInt32(Get(worn, slots[i])) > 0 && Convert.ToInt32(Get(worn, slots[i])) != Convert.ToInt32(Get(solar, slots[i])), "Armor must own its equip slot instead of triggering vanilla armor bonuses");
            total += (int)Get(worn, "defense")!;
        }
        Check(total == 78, "Full projection set must have 78 base defense");
        Console.WriteLine("PASS projection armor: 24/34/20 defense matches native Solar Flare, three independent equipment slots");
        object headItem = Item(Id(helmet)), bodyItem = Item(Id(chest)), legItem = Item(Id(legs));
        Check((bool)Call(helmet, "IsArmorSet", headItem, bodyItem, legItem)! &&
            !(bool)Call(helmet, "IsArmorSet", headItem, Item(0), legItem)!, "Only the complete armor set grants the set bonus");
        object player = Activator.CreateInstance(playerType)!;
        object melee = game.GetType("Terraria.ModLoader.DamageClass")!.GetProperty("Melee")!.GetValue(null)!;
        Type damageClass = game.GetType("Terraria.ModLoader.DamageClass")!;
        MethodInfo getDamage = playerType.GetMethod("GetDamage", new[] { damageClass })!, getSpeed = playerType.GetMethod("GetAttackSpeed", new[] { damageClass })!;
        object damageBefore = getDamage.Invoke(player, new[] { melee })!;
        object speedBefore = getSpeed.Invoke(player, new[] { melee })!;
        object moveBefore = Get(player, "moveSpeed")!;
        foreach (object piece in armor) Call(piece, "UpdateEquip", player);
        Call(helmet, "UpdateArmorSet", player);
        foreach (string flag in new[] { "lavaImmune", "fireWalk", "noFallDmg", "noKnockback" })
            Check((bool)Get(player, flag)!, "Missing passive armor immunity: " + flag);
        bool[] immune = (bool[])Get(player, "buffImmune")!;
        Type buffID = game.GetType("Terraria.ID.BuffID")!;
        foreach (string buff in new[] { "Poisoned", "Venom", "Stoned", "OnFire3", "Frostburn2", "Ichor", "Burning", "Frozen" })
            Check(immune[Convert.ToInt32(buffID.GetField(buff)!.GetValue(null))], "Missing common debuff immunity: " + buff);
        foreach (string buff in new[] { "PotionSickness", "ManaSickness", "MoonLeech" })
            Check(!immune[Convert.ToInt32(buffID.GetField(buff)!.GetValue(null))], "Armor must preserve resource and encounter mechanics: " + buff);
        Check(damageBefore.Equals(getDamage.Invoke(player, new[] { melee })) && speedBefore.Equals(getSpeed.Invoke(player, new[] { melee })) &&
            moveBefore.Equals(Get(player, "moveSpeed")) && !(bool)Get(player, "setSolar")!, "Armor must not add offensive/speed bonuses or Solar shield abilities");
        Console.WriteLine("PASS projection set: passive environment/debuff protection, no damage/speed/Solar abilities or potion/boss immunity");

        object tool = Item(Id(pick));
        Check((int)Get(tool, "pick")! == 350 && (int)Get(tool, "useTime")! == 3 && (bool)Get(tool, "autoReuse")!, "Pickaxe must mine quickly with 350% power");
        Check((int)Get(Item(Id(block)), "createTile")! == Id(bedrock), "Bedrock item must place the protected tile");
        Console.WriteLine("PASS projection pickaxe: 350% power, 3-tick mining, automatic reuse; bedrock placement uses protected tile");
        Call(bedrock, "SetStaticDefaults");
        object map = main.GetField("tile")!.GetValue(null)!;
        object Cell(int x, int y) => map.GetType().GetProperty("Item", new[] { typeof(int), typeof(int) })!.GetValue(map, new object[] { x, y })!;
        void Put() => tileType.GetMethod("ResetToType")!.Invoke(Cell(40, 40), new object[] { (ushort)Id(bedrock) });
        bool Has() => (bool)tileType.GetProperty("HasTile")!.GetValue(Cell(40, 40))!;
        Put();
        Check(!(bool)Call(bedrock, "CanExplode", 40, 40)! && !(bool)Call(bedrock, "CanReplace", 40, 40, 1)! &&
            !(bool)Call(bedrock, "Slope", 40, 40)! && !(bool)worldGen.GetMethod("CanKillTile", new[] { typeof(int), typeof(int) })!.Invoke(null, new object[] { 40, 40 })!, "Normal native destruction must reject bedrock");
        worldGen.GetMethod("KillTile")!.Invoke(null, new object[] { 40, 40, false, false, false });
        Check(Has(), "Direct native KillTile must preserve bedrock outside an authorized operation");
        Array inventory = (Array)Get(player, "inventory")!; inventory.SetValue(tool, 0);
        playerType.GetMethod("PickTile")!.Invoke(player, new object[] { 40, 40, 350 });
        Check(Has(), "Even the new 350%-power pick must preserve bedrock in 2D");
        object pistonWorld = Activator.CreateInstance(code.GetType("DimensionalTerraria.World.TerrariaPistonWorld")!, true)!;
        object cell = Activator.CreateInstance(code.GetType("DimensionalTerraria.Core.GridCell")!, new object[] { 40, 40 })!;
        Check(Call(pistonWorld, "At", cell)!.ToString() == "Obstacle", "Pistons must not bypass bedrock's native protection");
        Console.WriteLine("PASS native bedrock protection: 350%-power 2D pick, KillTile, explosives, replacement, hammering and piston movement blocked");

        Type scope = code.GetType("DimensionalTerraria.Systems.BedrockMining")!;
        IDisposable Allow(int x, int y) => (IDisposable)scope.GetMethod("Allow", All)!.Invoke(null, new object[] { x, y })!;
        bool Allows(int x, int y) => (bool)scope.GetMethod("Allows", All)!.Invoke(null, new object[] { x, y })!;
        using (Allow(40, 40))
        {
            Check(Allows(40, 40) && !Allows(41, 40), "Bedrock permission must be restricted to one cell");
            using (Allow(41, 40)) Check(!Allows(40, 40) && Allows(41, 40), "Nested permits must be isolated");
            Check(Allows(40, 40) && !Task.Run(() => Allows(40, 40)).Result, "Permit must restore and never leak to another thread");
        }
        Check(!Allows(40, 40), "Permission must expire at the end of the operation");
        Console.WriteLine("PASS bedrock permission: exact cell, nested restoration, synchronous thread isolation and automatic expiry");

        Type ray = code.GetType("DimensionalTerraria.Core.TileRayHit")!;
        object hit = Activator.CreateInstance(ray, new[] { (object)40, 40, Enum.Parse(code.GetType("DimensionalTerraria.Core.RayLayer")!, "Tile"), 1f,
            Activator.CreateInstance(ray.GetProperty("Min")!.PropertyType), Activator.CreateInstance(ray.GetProperty("Max")!.PropertyType) })!;
        Type interactions = code.GetType("DimensionalTerraria.World.NativeInteractions")!;
        bool HasTool() => (bool)interactions.GetMethod("HasTool")!.Invoke(null, new[] { player, hit })!;
        Check(!HasTool(), "2D player must not receive bedrock permission");
        Type dimension = code.GetType("DimensionalTerraria.Systems.DimensionSystem")!;
        object system = Activator.CreateInstance(dimension)!;
        game.GetType("Terraria.ModLoader.ContentInstance")!.GetMethod("Register", All)!.Invoke(null, new[] { system });
        object flight = Activator.CreateInstance(code.GetType("DimensionalTerraria.Core.FlightState")!,
            new object[] { new System.Numerics.Vector2(40), new System.Numerics.Vector2(1.25f, 2.625f), 32f })!;
        dimension.GetProperty("Flight", All)!.SetValue(system, flight);
        Set(player, "whoAmI", main.GetField("myPlayer")!.GetValue(null)!);
        main.GetField("netMode")!.SetValue(null, 0);
        bool mapEnabled = (bool)main.GetField("mapEnabled")!.GetValue(null)!;
        main.GetField("mapEnabled")!.SetValue(null, false); // No desktop map is allocated in this headless fixture.
        main.GetField("dedServ")!.SetValue(null, false);
        try
        {
            Check(HasTool(), "Detached player with projection pick must be able to mine bedrock");
            inventory.SetValue(Item(Convert.ToInt32(game.GetType("Terraria.ID.ItemID")!.GetField("CopperPickaxe")!.GetValue(null))), 0);
            Check(!HasTool(), "3D ordinary tools must not break bedrock");
            inventory.SetValue(tool, 0); Set(player, "noBuilding", true);
            Check(!HasTool(), "No-building restrictions must still apply"); Set(player, "noBuilding", false);
            interactions.GetMethod("ShootTile")!.Invoke(null, new[] { player, hit, (object)999999 });
            Check(Has(), "3D ranged damage must not borrow the held pick's bedrock permission");
            interactions.GetMethod("Hit")!.Invoke(null, new[] { player, hit });
            Check(!Has() && !Allows(40, 40), "Authorized 3D mining must remove bedrock and immediately close the permit");
            int drops = ((Array)main.GetField("item")!.GetValue(null)!).Cast<object>().Where(i =>
                (bool)Get(i, "active")! && (int)Get(i, "type")! == Id(block)).Sum(i => (int)Get(i, "stack")!);
            Check(drops == 1, "Bedrock mining must return exactly one item");
        }
        finally
        {
            main.GetField("dedServ")!.SetValue(null, true);
            main.GetField("mapEnabled")!.SetValue(null, mapEnabled);
            dimension.GetProperty("Flight", All)!.SetValue(system, null);
        }
        Console.WriteLine("PASS real 3D bedrock mining: special pick only, no-building honored, ranged bypass rejected, exactly one recoverable drop");
    }
}
