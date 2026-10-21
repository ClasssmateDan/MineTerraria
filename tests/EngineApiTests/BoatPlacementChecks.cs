using System.Collections;
using System.Reflection;
using System.Reflection.Emit;

internal static class BoatPlacementChecks
{
    const BindingFlags Hidden = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    internal static void Run(Assembly game, Assembly code, object boatItem)
    {
        Type main = game.GetType("Terraria.Main")!, tile = game.GetType("Terraria.Tile")!;
        Type tileId = game.GetType("Terraria.ID.TileID")!;
        object map = main.GetField("tile")!.GetValue(null)!;
        object Tile(int x, int y) => map.GetType().GetProperty("Item", new[] { typeof(int), typeof(int) })!.GetValue(map, new object[] { x, y })!;
        bool Has(int x, int y) => (bool)tile.GetProperty("HasTile")!.GetValue(Tile(x, y))!;
        void Put(int x, int y, int type) => tile.GetMethod("ResetToType")!.Invoke(Tile(x, y), new object[] { (ushort)type });
        void Clear(int x, int y) => tile.GetMethod("ClearTile")!.Invoke(Tile(x, y), null);
        int Id(string name) => Convert.ToInt32(tileId.GetField(name)!.GetRawConstantValue());
        int grass = Id("Plants"), rubble = Id("SmallPiles"), stone = Id("Stone"), chest = Id("Containers");
        bool[] cut = (bool[])main.GetField("tileCut")!.GetValue(null)!;
        bool oldGrassCut = cut[grass];
        cut[grass] = true; // This headless harness does not run Main's vanilla tile setup.
        object player = Activator.CreateInstance(game.GetType("Terraria.Player")!)!;
        object placement = Activator.CreateInstance(code.GetType("DimensionalTerraria.Core.BoatPlacement")!, new object[] { 29, 48, 50f })!;
        MethodInfo place = boatItem.GetType().GetMethod("TryPlace", Hidden)!;
        bool Place() => (bool)place.Invoke(boatItem, new object?[] { player, placement, null })!;
        bool Consumed() => (bool)boatItem.GetType().GetMethod("ConsumeItem")!.Invoke(boatItem, new[] { player })!;
        Type adapter = code.GetType("DimensionalTerraria.World.TerrariaCollisionWorld")!;
        object world = Activator.CreateInstance(adapter, nonPublic: true)!;
        MethodInfo preview = code.GetType("DimensionalTerraria.Core.BoatRules")!.GetMethod("TryPlacement")!;
        bool Preview() => (bool)preview.Invoke(null, new object?[] { world, 30, 50, null, null })!;

        // Override only the native rejection point under test. Keep the real
        // adapter, placement, destruction, framing and tile-entity paths.
        (EventInfo Event, Delegate Callback) Veto(string name)
        {
            EventInfo e = Type.GetType("Terraria.On_WorldGen, TerrariaHooks", true)!.GetEvent(name)!;
            MethodInfo invoke = e.EventHandlerType!.GetMethod("Invoke")!;
            var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("PlacementVeto" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
            var shim = assembly.DefineDynamicModule("Shim").DefineType("Shim", TypeAttributes.Public);
            var method = shim.DefineMethod("Veto", MethodAttributes.Public | MethodAttributes.Static, invoke.ReturnType,
                invoke.GetParameters().Select(p => p.ParameterType).ToArray());
            var il = method.GetILGenerator();
            if (invoke.ReturnType == typeof(bool)) il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ret);
            Delegate callback = shim.CreateType()!.GetMethod("Veto")!.CreateDelegate(e.EventHandlerType);
            e.AddEventHandler(null, callback);
            return (e, callback);
        }
        try
        {
            for (int x = 29; x <= 31; x++) { Clear(x, 48); Clear(x, 49); Put(x, 50, stone); }
            Put(29, 49, grass); Put(31, 48, stone);
            if (Place() || Consumed() || !Has(29, 49) || !Has(31, 48))
                throw new InvalidOperationException("A hard obstruction must reject the entire footprint before grass is destroyed or a boat consumed.");
            Put(31, 48, chest);
            if (Place() || !Has(29, 49) || !Has(31, 48)) throw new InvalidOperationException("Boat placement must not erase furniture or chests.");
            Clear(31, 48);
            if (!Preview() || !Has(29, 49)) throw new InvalidOperationException("A valid preview must accept grass without changing the map.");
            Console.WriteLine("PASS native boat preflight: grass preview is read-only; hard blocks and furniture reject before any clearing or consumption");

            var protectedTile = Veto("CanKillTile_int_int");
            try
            {
                if (Place() || Consumed() || !Has(29, 49)) throw new InvalidOperationException("Native tile-protection veto must preserve the plant and boat item.");
            }
            finally { protectedTile.Event.RemoveEventHandler(null, protectedTile.Callback); }
            var failedBreak = Veto("KillTile");
            try
            {
                if (Place() || Consumed() || !Has(29, 49)) throw new InvalidOperationException("If a kill hook leaves the plant intact, do not create an incomplete boat or consume the item.");
            }
            finally { failedBreak.Event.RemoveEventHandler(null, failedBreak.Callback); }
            Console.WriteLine("PASS native boat clearing veto: protection or failed native destruction preserves the item and creates no partial boat");

            Put(31, 49, rubble); Clear(30, 50); Clear(31, 50);
            if (!Place() || !Consumed()) throw new InvalidOperationException("A boat must clear native cuttable grass and replaceable rubble while supported by only one edge.");
            IDictionary entities = (IDictionary)game.GetType("Terraria.DataStructures.TileEntity")!.GetField("ByID")!.GetValue(null)!;
            object point = Activator.CreateInstance(game.GetType("Terraria.DataStructures.Point16")!, new object[] { 29, 48 })!;
            IDictionary byPosition = (IDictionary)game.GetType("Terraria.DataStructures.TileEntity")!.GetField("ByPosition")!.GetValue(null)!;
            object boat = byPosition[point]!;
            int id = (int)boat.GetType().GetField("ID")!.GetValue(boat)!;
            void Reconcile() => boat.GetType().GetMethod("Reconcile", Hidden)!.Invoke(boat, null);
            bool Ready() => (bool)boat.GetType().GetProperty("Ready", Hidden)!.GetValue(boat)!;
            Reconcile();
            if (!Ready() || !(bool)boat.GetType().GetMethod("IsTileValidForEntity")!.Invoke(boat, new object[] { 29, 48 })!)
                throw new InvalidOperationException("Cleared plants must be replaced by one complete six-frame boat with a live entity.");
            Put(31, 50, stone); Clear(29, 50); Reconcile();
            if (!Ready() || !entities.Contains(id)) throw new InvalidOperationException("Removing one support must keep the boat while another support remains.");
            Clear(31, 50); Reconcile();
            if (entities.Contains(id)) throw new InvalidOperationException("Removing the last support must remove the unsupported boat.");
            Console.WriteLine("PASS native overhanging boat: grass and rubble cleared, one edge support accepted, remaining support retained, last-support removal cleans up");
        }
        finally
        {
            cut[grass] = oldGrassCut;
            for (int x = 29; x <= 31; x++) { Clear(x, 48); Clear(x, 49); Put(x, 50, stone); }
        }
    }
}
