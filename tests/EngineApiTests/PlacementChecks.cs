using System.Collections;
using System.Reflection;
using System.Numerics;

internal static class PlacementChecks
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    internal static void Run(Assembly game, Assembly code, object chestItem, object pistonItem)
    {
        Type main=game.GetType("Terraria.Main")!,tile=game.GetType("Terraria.Tile")!,itemType=game.GetType("Terraria.Item")!;
        Type playerType=game.GetType("Terraria.Player")!,native=code.GetType("DimensionalTerraria.World.NativePlacement")!;
        object? Get(object o,string f)=>o.GetType().GetField(f,All)!.GetValue(o);
        void Set(object o,string f,object v){if(o.GetType().GetField(f,All) is {} field)field.SetValue(o,v);else o.GetType().GetProperty(f,All)!.SetValue(o,v);}
        void Check(bool v,string why){if(!v)throw new InvalidOperationException(why);}
        object map=main.GetField("tile")!.GetValue(null)!;
        object Cell(int x,int y)=>map.GetType().GetProperty("Item",new[]{typeof(int),typeof(int)})!.GetValue(map,new object[]{x,y})!;
        void Put(int x,int y,int t)=>tile.GetMethod("ResetToType")!.Invoke(Cell(x,y),new object[]{(ushort)t});
        bool Has(int x,int y)=>(bool)tile.GetProperty("HasTile")!.GetValue(Cell(x,y))!;
        int Id(object o)=>Convert.ToInt32(o.GetType().GetProperty("Type")!.GetValue(o));
        int ItemID(string name)=>Convert.ToInt32(game.GetType("Terraria.ID.ItemID")!.GetField(name)!.GetValue(null));
        object Item(int id,int count=3){var o=Activator.CreateInstance(itemType)!;itemType.GetMethod("SetDefaults",new[]{typeof(int)})!.Invoke(o,new object[]{id});Set(o,"stack",count);return o;}
        main.GetField("dedServ")!.SetValue(null,true);main.GetField("netMode")!.SetValue(null,0);
        main.GetField("SceneMetrics")!.SetValue(null,Activator.CreateInstance(game.GetType("Terraria.SceneMetrics")!));
        game.GetType("Terraria.Framing")!.GetMethod("Initialize")!.Invoke(null,null);
        // No GlobalWall content is registered in this isolated fixture. Supply
        // its normal empty hook arrays without resetting ID static factories.
        foreach(var field in game.GetType("Terraria.ModLoader.WallLoader")!.GetFields(All).Where(f=>f.Name.StartsWith("Hook") && f.FieldType.IsArray))
            field.SetValue(null,Array.CreateInstance(field.FieldType.GetElementType()!,0));
        map.GetType().GetMethod("ClearEverything")!.Invoke(map,null);
        foreach(string key in new[]{"ByID","ByPosition"})((IDictionary)game.GetType("Terraria.DataStructures.TileEntity")!.GetField(key)!.GetValue(null)!).Clear();
        Array.Clear((Array)main.GetField("chest")!.GetValue(null)!);
        Array players=(Array)main.GetField("player")!.GetValue(null)!,npcs=(Array)main.GetField("npc")!.GetValue(null)!;
        foreach(var p in players)Set(p!,"active",false);foreach(var n in npcs)Set(n!,"active",false);
        object player=players.GetValue((int)main.GetField("myPlayer")!.GetValue(null)!)!;
        Set(player,"selectedItem",0);Set(player,"noBuilding",false);Set(player,"noItems",false);Set(player,"direction",1);
        var positionType=playerType.GetField("position")!.FieldType;
        object XY(float x,float y)=>Activator.CreateInstance(positionType,new object[]{x,y})!;
        Set(player,"position",XY(16*15,16*15));object bodyBefore=Get(player,"position")!;
        Array inventory=(Array)Get(player,"inventory")!;
        object stone=Item(ItemID("StoneBlock"));inventory.SetValue(stone,0);
        for(int x=25;x<55;x++)Put(x,50,1);
        Vector3 eye=new(30.5f,50.1f,4),forward=-Vector3.UnitZ;
        bool Place(Vector3 from,Vector3 dir,object? fog=null)
        {
            object?[] args={player,from,dir,null,fog,null};
            return (bool)native.GetMethod("TryPlace",All)!.Invoke(null,args)!;
        }
        Check(Place(eye,forward) && Has(30,49) && (int)Get(stone,"stack")! ==2,"Actual 3D placement must attach a stone block and consume exactly one item");
        Check(bodyBefore.Equals(Get(player,"position")),"Placement must not move the detached 2D player body");
        Check(!Place(eye,forward) && (int)Get(stone,"stack")! ==2,"Occupied target must fail without consuming another item");
        Check(!Place(new(32.5f,50.1f,9),forward),"Distant 3D building must fail");
        Set(player,"noBuilding",true);Check(!Place(new(32.5f,50.1f,4),forward),"No-building must block placement");Set(player,"noBuilding",false);
        var fogType=code.GetType("DimensionalTerraria.Core.ExplorationMask")!;
        object hidden=Activator.CreateInstance(fogType,new object[]{(int)main.GetField("maxTilesX")!.GetValue(null)!,(int)main.GetField("maxTilesY")!.GetValue(null)!,new Func<int,int,bool>((x,y)=>false)})!;
        Check(!Place(new(32.5f,50.1f,4),forward,hidden) && (int)Get(stone,"stack")! ==2,"Fog must reject placement before item consumption");
        Check(!Place(new(20.5f,40.5f,4),forward),"Unsupported floating blocks must fail");
        object obstruction=npcs.GetValue(0)!;Set(obstruction,"active",true);Set(obstruction,"width",16);Set(obstruction,"height",16);Set(obstruction,"position",XY(32*16,49*16));
        Check(!Place(new(32.5f,50.1f,4),forward),"Occupied creature hitboxes must block building");Set(obstruction,"active",false);
        Console.WriteLine("PASS actual 3D block placement: one-item consumption, no body teleport, occupied/range/fog/support/no-building/creature rejection");
        object wall=Item(ItemID("StoneWall"));inventory.SetValue(wall,0);
        Check(Place(new(33.5f,49.5f,4),forward) && (ushort)tile.GetProperty("WallType")!.GetValue(Cell(33,49))!>0 && (int)Get(wall,"stack")! ==2,"Native wall placement must consume once on success");
        Check(!Place(new(33.5f,49.5f,4),forward) && (int)Get(wall,"stack")! ==2,"Existing walls must not be replaced");
        Console.WriteLine("PASS actual 3D wall placement: native wall creation, adjacency and no replacement");
        object chest=Item(Id(chestItem));inventory.SetValue(chest,0);
        Check(Place(new(35.5f,50.1f,4),forward) && Has(35,48) && Has(36,49) && (int)Get(chest,"stack")! ==2,"Native furniture placement must fill all four chest cells");
        Type chestType=game.GetType("Terraria.Chest")!;
        int slot=(int)chestType.GetMethod("FindChest")!.Invoke(null,new object[]{35,48})!;
        Check(slot>=0 && ((Array)Get(((Array)main.GetField("chest")!.GetValue(null)!).GetValue(slot)!,"item")!).Length==40,"3D chest placement must allocate native inventory via hooks");
        Check(!Place(new(54.5f,50.1f,4),forward) && (int)Get(chest,"stack")! ==2,"Unsupported second furniture column must reject the entire operation");
        Console.WriteLine("PASS actual 3D chest placement: native anchors, complete 2x2 footprint, 40-slot registration and atomic unsupported failure");
        object piston=Item(Id(pistonItem));inventory.SetValue(piston,0);
        Check(Place(new(40.5f,50.1f,4),forward),"Piston must place through the native object path");
        IDictionary entities=(IDictionary)game.GetType("Terraria.DataStructures.TileEntity")!.GetField("ByPosition")!.GetValue(null)!;
        Check(entities.Values.Cast<object>().Any(e=>e.GetType().Name=="PistonEntity"),"Native post-placement hooks must create the piston entity");
        Console.WriteLine("PASS actual 3D piston placement: native object and TileEntity post-placement hooks run");

        Type combat=code.GetType("DimensionalTerraria.World.NativeCombat")!;
        foreach(string key in new[]{"CobaltDrill","PalladiumDrill","Drax","LaserDrill","CobaltChainsaw"})
        {
            object drill=Item(ItemID(key),1);inventory.SetValue(drill,0);
            object mode=combat.GetMethod("Mode")!.Invoke(null,new[]{drill})!;
            Check(mode.ToString()=="Melee","Drill must use direct aimed melee: "+key);
            object?[] args={player,mode,null,null};
            Check((bool)combat.GetMethod("Prepare")!.Invoke(null,args)!,"Drill attack must prepare: "+key);
            int cooldown=(int)args[2]!.GetType().GetProperty("Cooldown")!.GetValue(args[2])!;
            Check(cooldown>=3 && cooldown<=6,"Drill must have a bounded high-frequency cooldown");
        }
        foreach(string key in new[]{"LastPrism","Flamethrower"})
        {
            object weapon=Item(ItemID(key),1);
            Check(!(bool)combat.GetMethod("IsDrill",All)!.Invoke(null,new[]{weapon})!,"Other channeled weapons must not become drills");
        }
        Console.WriteLine("PASS actual drill defaults and attack preparation: five drill/chainsaw families use melee at 3-6 ticks, unrelated channels excluded");
    }
}
