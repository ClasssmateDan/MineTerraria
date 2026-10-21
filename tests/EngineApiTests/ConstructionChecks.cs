using System.Collections;
using System.Reflection;

internal static class ConstructionChecks
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    internal static void Run(Assembly game, Assembly code, object chestTile, object chestItem, object egg, object villager, object hopperTile, object hopperTemplate)
    {
        Type main=game.GetType("Terraria.Main")!, tile=game.GetType("Terraria.Tile")!, itemType=game.GetType("Terraria.Item")!;
        Type playerType=game.GetType("Terraria.Player")!, chestType=game.GetType("Terraria.Chest")!, world=game.GetType("Terraria.WorldGen")!;
        object? Call(object obj,string name,params object?[] args)=>obj.GetType().GetMethod(name,All)!.Invoke(obj,args);
        object? Get(object obj,string name)=>obj.GetType().GetField(name,All)!.GetValue(obj);
        void Set(object obj,string name,object value)=>obj.GetType().GetField(name,All)!.SetValue(obj,value);
        int Id(object obj)=>Convert.ToInt32(obj.GetType().GetProperty("Type")!.GetValue(obj));
        void Check(bool value,string message) {if(!value) throw new InvalidOperationException(message);}
        object map=main.GetField("tile")!.GetValue(null)!;
        object Cell(int x,int y)=>map.GetType().GetProperty("Item",new[]{typeof(int),typeof(int)})!.GetValue(map,new object[]{x,y})!;
        void Put(int x,int y,int kind)=>tile.GetMethod("ResetToType")!.Invoke(Cell(x,y),new object[]{(ushort)kind});
        object Item(int kind,int amount=1) {
            object value=Activator.CreateInstance(itemType)!;
            itemType.GetMethod("SetDefaults",new[]{typeof(int)})!.Invoke(value,new object[]{kind});Set(value,"stack",amount);return value;
        }
        int NativeItem(string name)=>Convert.ToInt32(game.GetType("Terraria.ID.ItemID")!.GetField(name)!.GetValue(null));
        Type vector=playerType.GetField("position")!.FieldType;
        object V(float x,float y)=>Activator.CreateInstance(vector,new object[]{x,y})!;
        main.GetField("dedServ")!.SetValue(null,true);main.GetField("netMode")!.SetValue(null,0);main.GetField("gameMenu")!.SetValue(null,false);
        map.GetType().GetMethod("ClearEverything")!.Invoke(map,null);
        ((IDictionary)game.GetType("Terraria.DataStructures.TileEntity")!.GetField("ByID")!.GetValue(null)!).Clear();
        ((IDictionary)game.GetType("Terraria.DataStructures.TileEntity")!.GetField("ByPosition")!.GetValue(null)!).Clear();
        Array chests=(Array)main.GetField("chest")!.GetValue(null)!;Array.Clear(chests);
        Call(chestTile,"SetStaticDefaults");
        for(int x=25;x<50;x++)Put(x,50,1);
        Check((bool)world.GetMethod("PlaceObject")!.Invoke(null,new object[]{30,49,Id(chestTile),true,0,0,-1,-1})!,"Real 2x2 chest placement failed");
        Call(chestTile,"PlaceInWorld",30,49,Item(Id(chestItem)));
        int index=(int)chestType.GetMethod("FindChest")!.Invoke(null,new object[]{30,48})!;
        Check(index>=0,"Chest placement must allocate native storage");
        object chest=chests.GetValue(index)!;
        Array slots=(Array)Get(chest,"item")!;
        Check(slots.Length==40 && (int)Get(Item(Id(chestItem)),"createTile")! == Id(chestTile),"Chest must be a native 40-slot placeable container");
        Check((int)chestTile.GetType().GetProperty("MinPick")!.GetValue(chestTile)! ==0 &&
            (float)chestTile.GetType().GetProperty("MineResist")!.GetValue(chestTile)!<1,"Chest must be easy to mine with an ordinary pick");
        Console.WriteLine("PASS native Minecraft chest: actual 2x2 placement, registered 40-slot storage and low mining resistance");
        object player=((Array)main.GetField("player")!.GetValue(null)!).GetValue((int)main.GetField("myPlayer")!.GetValue(null)!)!;
        // Native OpenChest consults the ordinary keyboard/gamepad profile even
        // in a headless call. Initialize key data without devices or windows.
        game.GetType("Terraria.GameInput.PlayerInput")!.GetMethod("Initialize_Inner",All)!.Invoke(null,null);
        Set(player,"chest",-1);
        Check((bool)Call(chestTile,"RightClick",31,49)! && (int)Get(player,"chest")! == index && (bool)main.GetField("playerInventory")!.GetValue(null)!,"Any chest cell must open the native inventory");
        Check((bool)Call(chestTile,"RightClick",30,48)! && (int)Get(player,"chest")! ==-1,"Second click must close the same chest");
        Set(chest,"name","MC storage");slots.SetValue(Item(NativeItem("Wood"),37),0);slots.SetValue(Item(Id(egg),3),39);
        world.GetMethod("KillTile")!.Invoke(null,new object[]{30,48,false,false,false});
        Check((bool)tile.GetProperty("HasTile")!.GetValue(Cell(30,48))! && !(bool)Call(chestTile,"CanExplode",30,48)! &&
            (int)Get(slots.GetValue(0)!,"stack")! ==37,"Mining and explosions must preserve a nonempty chest");
        object pistonWorld=Activator.CreateInstance(code.GetType("DimensionalTerraria.World.TerrariaPistonWorld")!,true)!;
        object grid=Activator.CreateInstance(code.GetType("DimensionalTerraria.Core.GridCell")!,new object[]{30,48})!;
        Check(Call(pistonWorld,"At",grid)!.ToString()=="Obstacle","Pistons must not move chest inventory");
        Console.WriteLine("PASS native Minecraft chest access/protection: open and close from any cell, contents block direct destruction/explosions, pistons rejected");
        Type worldIO=game.GetType("Terraria.ModLoader.IO.WorldIO")!, worldFile=game.GetType("Terraria.IO.WorldFile")!;
        object modInventory=worldIO.GetMethod("SaveChestInventory",All)!.Invoke(null,null)!;
        using(var stream=new MemoryStream()) {
            using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true)) worldFile.GetMethod("SaveChests",All)!.Invoke(null,new object[]{writer});
            Array.Clear(chests);stream.Position=0;
            using var reader=new BinaryReader(stream,System.Text.Encoding.UTF8,true);
            worldFile.GetMethod("LoadChests",All)!.Invoke(null,new object[]{reader});
        }
        worldIO.GetMethod("LoadChestInventory",All)!.Invoke(null,new[]{modInventory});
        index=(int)chestType.GetMethod("FindChest")!.Invoke(null,new object[]{30,48})!;chest=chests.GetValue(index)!;slots=(Array)Get(chest,"item")!;
        Check((string)Get(chest,"name")! =="MC storage" && (int)Get(slots.GetValue(0)!,"stack")! ==37 &&
            (int)Get(slots.GetValue(39)!,"type")! ==Id(egg) && (int)Get(slots.GetValue(39)!,"stack")! ==3,"Native and mod inventory/name must survive real chest binary saving");
        Console.WriteLine("PASS native chest save round-trip: WorldFile binary plus WorldIO inventory restore coordinates, name, vanilla stack and mod egg in slot 40");
        Put(30,47,Id(hopperTile));
        int hopper=(int)Call(hopperTemplate,"AfterPlacement",30,47,Id(hopperTile),0,0,0)!;
        object hopperEntity=((IDictionary)game.GetType("Terraria.DataStructures.TileEntity")!.GetField("ByID")!.GetValue(null)!)[hopper]!;
        object drop=Item(NativeItem("Wood"),5);Set(drop,"active",true);Set(drop,"position",V(30*16+3,46*16));
        ((Array)main.GetField("item")!.GetValue(null)!).SetValue(drop,0);Call(hopperEntity,"Tick");
        Check((int)Get(slots.GetValue(0)!,"stack")! ==42,"Existing hopper must insert into the new native chest");
        Console.WriteLine("PASS real hopper integration: loose item merges into Minecraft chest's existing native stack");
        for(int i=0;i<slots.Length;i++)slots.SetValue(Activator.CreateInstance(itemType),i);
        // Real native chest destruction recalculates recipes; supply the normal empty guide item.
        main.GetField("guideItem")!.SetValue(null,Activator.CreateInstance(itemType));
        Type recipe=game.GetType("Terraria.Recipe")!;
        Array recipeTable=(Array)main.GetField("recipe")!.GetValue(null)!;
        for(int i=0;i<recipeTable.Length;i++) if(recipeTable.GetValue(i) is null)
            recipeTable.SetValue(Activator.CreateInstance(recipe,All,null,new object?[]{null},null),i);
        recipe.GetMethod("CreateRequiredItemQuickLookups",All)!.Invoke(null,null);
        Array inventory=(Array)Get(player,"inventory")!;inventory.SetValue(Item(NativeItem("CopperPickaxe")),0);Set(player,"selectedItem",0);
        playerType.GetMethod("PickTile")!.Invoke(player,new object[]{30,48,35});
        Check(!(bool)tile.GetProperty("HasTile")!.GetValue(Cell(30,48))! &&
            (int)chestType.GetMethod("FindChest")!.Invoke(null,new object[]{30,48})!<0,"A copper pick must remove an empty chest and release its native slot");
        int returned=((Array)main.GetField("item")!.GetValue(null)!).Cast<object>().Where(i=>(bool)Get(i,"active")! && (int)Get(i,"type")! ==Id(chestItem)).Sum(i=>(int)Get(i,"stack")!);
        Check(returned==1,"An empty chest must return exactly one placeable chest item");
        Console.WriteLine("PASS actual copper-pick chest removal: empty chest breaks in one mining hit and unregisters storage");

        Array npcs=(Array)main.GetField("npc")!.GetValue(null)!;
        foreach(object npc in npcs)Set(npc,"active",false);
        Call(egg,"SetDefaults");
        Check(!(bool)Call(egg,"TrySpawn",player,1,1,null)! && !(bool)Call(egg,"ConsumeItem",player)!,"Failed villager egg placement must not consume it");
        Check((bool)Call(egg,"TrySpawn",player,40,50,null)! && (bool)Call(egg,"ConsumeItem",player)!,"Valid villager egg must spawn and consume once");
        object born=npcs.Cast<object>().Single(n=>(bool)Get(n,"active")!);
        Check((int)Get(born,"type")! ==Id(villager) && (int)Get(born,"lifeMax")! ==100 && (int)Get(born,"damage")! ==0,"Egg must create the existing passive 100-HP villager");
        Console.WriteLine("PASS actual villager egg: invalid ground preserves item, successful native NPC.NewNPC produces existing passive 100-HP villager");

        object crouch=Activator.CreateInstance(code.GetType("DimensionalTerraria.Players.CrouchPlayer")!)!;
        object walker=Activator.CreateInstance(playerType)!;
        crouch.GetType().GetProperty("Entity",All)!.SetValue(crouch,walker);
        Set(walker,"active",true);Set(walker,"controlDown",true);Set(walker,"position",V(40*16,50*16-42));Set(walker,"velocity",V(0,0));
        Check((bool)crouch.GetType().GetProperty("IsCrouching",All)!.GetValue(crouch)!,"Down on hard ground must crouch");
        Set(walker,"maxRunSpeed",3f);Set(walker,"accRunSpeed",6f);Call(crouch,"PostUpdateRunSpeeds");
        Check(Math.Abs((float)Get(walker,"maxRunSpeed")!-.9f)<.001f && Math.Abs((float)Get(walker,"accRunSpeed")!-1.8f)<.001f,"Crouch must slow both native run speeds");
        Call(crouch,"PostUpdate");
        Check((float)crouch.GetType().GetProperty("Amount")!.GetValue(crouch)! ==1 && (int)Get(walker,"height")! ==42,"Crouch must become fully active on its first update without changing native collision height");
        Put(40,50,19);Put(41,50,19);
        Check(!(bool)crouch.GetType().GetProperty("IsCrouching",All)!.GetValue(crouch)!,"Down on platforms must remain native fall-through input");
        Set(walker,"controlDown",false);Call(crouch,"PostUpdate");
        Check((float)crouch.GetType().GetProperty("Amount")!.GetValue(crouch)! ==0,"Releasing Down must restore standing");
        Console.WriteLine("PASS native crouch input: instantaneous activation/release, 30% run speeds, platform fall-through and original collision height");
        // Exercise the actual final draw layer using native DrawData values.
        // An explicit source rectangle avoids requesting any Texture2D/GPU.
        Type modPlayer=game.GetType("Terraria.ModLoader.ModPlayer")!;
        var ownIndex=modPlayer.GetProperty("Index",All)!;ownIndex.SetValue(crouch,Convert.ChangeType(0,ownIndex.PropertyType));
        Array components=Array.CreateInstance(modPlayer,1);components.SetValue(crouch,0);Set(walker,"modPlayers",components);
        game.GetType("Terraria.ModLoader.ContentInstance")!.GetMethod("Register",All)!.Invoke(null,new[]{crouch});
        crouch.GetType().GetProperty("Amount")!.SetValue(crouch,1f);Set(walker,"direction",1);
        main.GetField("screenPosition")!.SetValue(null,V(0,0));
        Type draw=game.GetType("Terraria.DataStructures.DrawData")!, infoType=game.GetType("Terraria.DataStructures.PlayerDrawSet")!;
        object drawInfo=Activator.CreateInstance(infoType)!, data=Activator.CreateInstance(draw)!;
        object cache=Activator.CreateInstance(typeof(List<>).MakeGenericType(draw))!;
        Set(data,"position",V(0,42));Set(data,"origin",V(50,100));Set(data,"scale",V(.42f,.42f));Set(data,"shader",7);
        Set(data,"sourceRect",Activator.CreateInstance(draw.GetField("sourceRect")!.FieldType.GetGenericArguments()[0],new object[]{0,0,100,100})!);
        ((IList)cache).Add(data);Set(drawInfo,"drawPlayer",walker);Set(drawInfo,"Position",V(0,0));Set(drawInfo,"DrawDataCache",cache);Set(drawInfo,"projectileDrawPosition",1);
        object layer=Activator.CreateInstance(code.GetType("DimensionalTerraria.Rendering.CrouchPoseLayer")!)!;
        object[] drawArguments={drawInfo};layer.GetType().GetMethod("Draw",All)!.Invoke(layer,drawArguments);
        IList bent=(IList)Get(drawArguments[0],"DrawDataCache")!;
        object first=Get(bent[0]!,"position")!,last=Get(bent[bent.Count-1]!,"position")!;
        Check(bent.Count>20 && (float)Get(first,"X")! ==6 && (float)Get(first,"Y")! ==46 &&
            (float)Get(last,"X")! ==0 && (float)Get(last,"Y")! ==42 &&
            bent.Cast<object>().All(d=>(int)Get(d,"shader")! ==7) && (int)Get(drawArguments[0],"projectileDrawPosition")! ==bent.Count,
            "Actual crouch drawing must lean only the upper body, preserve feet/dyes and remap held-projectile layering");
        Console.WriteLine("PASS real crouch draw cache: forward/down head translation, stationary feet, preserved dye shaders and remapped held-projectile order without graphics access");
    }
}
