using System.Numerics;
using System.Reflection;
using System.Reflection.Emit;

internal static class FlightRegressionChecks
{
    const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    internal static void Run(Assembly game, Assembly code, object firework, object enemyState)
    {
        Type main=game.GetType("Terraria.Main")!, npcType=game.GetType("Terraria.NPC")!, dimension=code.GetType("DimensionalTerraria.Systems.DimensionSystem")!;
        object? Get(object o,string name)=>o.GetType().GetField(name,All)?.GetValue(o) ?? o.GetType().GetProperty(name,All)!.GetValue(o);
        void Set(object o,string name,object value){if(o.GetType().GetField(name,All) is {} field)field.SetValue(o,value);else o.GetType().GetProperty(name,All)!.SetValue(o,value);}
        object? Call(object o,string name,params object?[] args)=>o.GetType().GetMethod(name,All)!.Invoke(o,args);
        void Check(bool pass,string reason){if(!pass)throw new InvalidOperationException(reason);}
        Type vector=npcType.GetField("position")!.FieldType;
        object V(float x,float y)=>Activator.CreateInstance(vector,new object[]{x,y})!;
        object player=((Array)main.GetField("player")!.GetValue(null)!).GetValue(0)!;
        Array npcs=(Array)main.GetField("npc")!.GetValue(null)!;
        Array saved=(Array)npcs.Clone();
        object oldSystem=dimension.GetProperty("Instance",All)!.GetValue(null)!;
        object system=Activator.CreateInstance(dimension)!;
        var register=game.GetType("Terraria.ModLoader.ContentInstance")!.GetMethod("Register",All)!;
        register.Invoke(null,new[]{system});
        var flightProperty=dimension.GetProperty("Flight",All)!;
        Type flightType=code.GetType("DimensionalTerraria.Core.FlightState")!;
        object flight=Activator.CreateInstance(flightType,new object[]{new Vector2(40,60),new Vector2(1.25f,2.625f),32f})!;
        main.GetField("netMode")!.SetValue(null,0);main.GetField("gameMenu")!.SetValue(null,false);
        main.GetField("dedServ")!.SetValue(null,true);main.GetField("dayTime")!.SetValue(null,false);
        Set(player,"active",true);Set(player,"dead",false);Set(player,"ghost",false);Set(player,"noItems",false);
        Set(player,"position",V(40*16+100,60*16));
        MethodInfo defaults=npcType.GetMethods().Single(m=>m.Name=="SetDefaults"&&m.GetParameters().Length==2&&m.GetParameters()[0].ParameterType==typeof(int));
        object parameters=Activator.CreateInstance(defaults.GetParameters()[1].ParameterType)!;
        object Npc(string name,int slot=0)
        {
            object n=Activator.CreateInstance(npcType)!;
            defaults.Invoke(n,new[]{game.GetType("Terraria.ID.NPCID")!.GetField(name)!.GetValue(null),parameters});
            Set(n,"whoAmI",slot);Set(n,"active",true);Set(n,"position",V(40*16,60*16));
            npcs.SetValue(n,slot);return n;
        }
        // Headless fixture omits gore rendering only; native AI still executes
        // the orphan's life/active transition and damage remains unmodified.
        EventInfo hitEffects=Type.GetType("Terraria.On_NPC, TerrariaHooks",true)!.GetEvent("HitEffect_HitInfo")!;
        MethodInfo effectInvoke=hitEffects.EventHandlerType!.GetMethod("Invoke")!;
        var skipEffects=new DynamicMethod("SkipHeadlessGore",typeof(void),effectInvoke.GetParameters().Select(p=>p.ParameterType).ToArray(),typeof(FlightRegressionChecks));
        skipEffects.GetILGenerator().Emit(OpCodes.Ret);
        Delegate effectShim=skipEffects.CreateDelegate(hitEffects.EventHandlerType);
        hitEffects.AddEventHandler(null,effectShim);
        bool loaded=false;
        try
        {
            // Real On_NPC and native target hooks; no game window or renderer.
            Call(system,"LoadTargetingHooks");loaded=true;
            flightProperty.SetValue(system,flight);
            foreach(string name in new[]{"KingSlime","EyeofCthulhu","SkeletronHand","TheDestroyerBody","GolemFistLeft","MoonLordHand","EaterofWorldsBody","Creeper"})
            {
                string? parentName=name switch {"SkeletronHand"=>"SkeletronHead","TheDestroyerBody"=>"TheDestroyer",
                    "GolemFistLeft"=>"Golem","MoonLordHand"=>"MoonLordCore","EaterofWorldsBody"=>"EaterofWorldsHead","Creeper"=>"BrainofCthulhu",_=>null};
                object? parent=parentName is null?null:Npc(parentName,2);
                object n=Npc(name);Set(n,"target",0);Set(n,"oldTarget",0);Set(n,"timeLeft",2);Set(n,"velocity",V(9,-15));
                float[] ai=(float[])Get(n,"ai")!,localAi=(float[])Get(n,"localAI")!;
                ai[0]=1;ai[1]=13;localAi[0]=7;
                float[] oldAi=(float[])ai.Clone(),oldLocalAi=(float[])localAi.Clone();
                int life=(int)Get(n,"life")!;
                for(int tick=0;tick<900;tick++)
                {
                    Call(n,"AI");
                    main.GetField("dedServ")!.SetValue(null,false);
                    try{Call(n,"CheckActive");}finally{main.GetField("dedServ")!.SetValue(null,true);}
                }
                Check((bool)Get(n,"active")! && (int)Get(n,"timeLeft")!>=750 && (int)Get(n,"life")! ==life,$"Boss must remain alive beyond retreat timeout: {name}, active={Get(n,"active")}, time={Get(n,"timeLeft")}, life={Get(n,"life")}/{life}, held={Call(system,"HoldDetachedBoss",n)}");
                Check(ai.SequenceEqual(oldAi)&&localAi.SequenceEqual(oldLocalAi)&&(int)Get(n,"target")! ==255&&(int)Get(n,"oldTarget")! ==255,"Boss wait must preserve phase timers and exclude player: "+name);
                Check((float)Get(Get(n,"velocity")!,"X")! ==0 && (float)Get(Get(n,"velocity")!,"Y")! ==0,"Retreat velocity must not persist: "+name);
                if(parent is not null)
                {
                    Set(parent,"active",false);
                    Check(!(bool)Call(system,"HoldDetachedBoss",n)!,"Orphaned boss parts must resume native cleanup: "+name);
                    if(name=="SkeletronHand") {Call(n,"AI");Check(!(bool)Get(n,"active")!,"Actual Skeletron hand AI must remove the orphan in 3D");}
                }
            }
            Console.WriteLine("PASS real boss hooks: eight bosses/parts remain for 900 ticks each with phase data, life and empty targets intact, native activity checks cannot despawn them");
            Console.WriteLine("PASS boss death cleanup: six families release orphaned parts, actual native Skeletron hand AI removes its orphan in 3D");
            object eye=Npc("EyeofCthulhu");
            Call(eye,"AI");
            int beforeHit=(int)Get(eye,"life")!;
            Type hitType=game.GetType("Terraria.NPC+HitInfo")!;
            object hit=Activator.CreateInstance(hitType)!;
            hitType.GetProperty("Damage")!.SetValue(hit,100);hitType.GetField("HideCombatText")!.SetValue(hit,true);
            npcType.GetMethod("StrikeNPC",new[]{hitType,typeof(bool),typeof(bool)})!.Invoke(eye,new object[]{hit,false,true});
            Check((int)Get(eye,"life")! ==beforeHit-100 && (bool)Call(system,"HoldDetachedBoss",eye)!,"Actual damage must continue while boss AI waits");
            Console.WriteLine("PASS real boss damage during 3D retention: native StrikeNPC applies 100 damage without releasing a living encounter");
            Set(eye,"life",0);
            Check(!(bool)Call(system,"HoldDetachedBoss",eye)!,"Boss retention must not suppress death");
            Set(eye,"life",(int)Get(eye,"lifeMax")!);
            object slime=Npc("BlueSlime",1);
            Check(!(bool)Call(system,"HoldDetachedBoss",slime)! && (bool)Call(enemyState,"CheckActive",slime)!,"Ordinary mobs must retain activity/despawn handling");
            flightProperty.SetValue(system,null);
            Check(!(bool)Call(system,"HoldDetachedBoss",eye)!,"Returning to 2D must immediately release boss retention");
            Call(eye,"AI");
            Check((int)Get(eye,"target")! ==0,"A nearby returned player must be targeted by actual vanilla Eye AI");
            Set(player,"position",V(10000*16,60*16));
            Call(system,"ForgetLocal",eye);
            Call(eye,"AI");
            Check((int)Get(eye,"target")! ==255,"A far returned player must remain outside boss sensing range");
            Console.WriteLine("PASS native boss recovery: nearby Eye resumes native targeting, far player stays excluded, zero-life bosses and ordinary enemies are not retained");

            Set(player,"position",V(40*16,60*16));Set(player,"velocity",V(0,0));
            Call(firework,"SetDefaults");object item=Get(firework,"Item")!;Set(item,"stack",4);
            bool NativeConsume()=>(bool)game.GetType("Terraria.ModLoader.ItemLoader")!.GetMethod("ConsumeItem")!.Invoke(null,new[]{item,player})!;
            Check((bool)Get(item,"consumable")! && (int)Get(item,"maxStack")! ==9999 && (int)Get(item,"useTime")! ==20,"Firework must have native consumable/cooldown defaults");
            Check(!(bool)Call(firework,"ConsumeItem",player)! && !NativeConsume(),"An unused firework cannot be consumed");
            object center=Get(player,"Center")!;float cx=(float)Get(center,"X")!,cy=(float)Get(center,"Y")!;
            Check(!(bool)Call(firework,"TryLaunch",player,null,V(cx,cy))! && !(bool)Call(firework,"ConsumeItem",player)!,"Zero aim must fail without consumption");
            Check((bool)Call(firework,"TryLaunch",player,null,V(cx+160,cy-160))! && (bool)Call(firework,"ConsumeItem",player)! && NativeConsume(),"Valid 2D mouse thrust must permit native consumption");
            object velocity=Get(player,"velocity")!;
            Check((float)Get(velocity,"X")!>11 && (float)Get(velocity,"Y")! < -11,"2D thrust must follow the mouse including elevation");
            Call(firework,"UseItem",player);
            Check(Get(player,"velocity")!.Equals(velocity),"Repeated native UseItem in the same animation must not add another impulse");
            for(int i=0;i<10;i++)Call(firework,"TryLaunch",player,null,V(cx+160,cy-160));
            velocity=Get(player,"velocity")!;
            Check(Math.Abs(MathF.Sqrt(MathF.Pow((float)Get(velocity,"X")!,2)+MathF.Pow((float)Get(velocity,"Y")!,2))-24)<.001f,"Repeated 2D fireworks must obey speed cap");
            Check(!(bool)Call(firework,"TryLaunch",player,null,V(float.NaN,cy))! && !(bool)Call(firework,"ConsumeItem",player)!,"Invalid aim cannot consume a rocket");
            Check(!(bool)Call(firework,"TryLaunch",player,flight,V(0,0))! && !(bool)Call(firework,"ConsumeItem",player)!,"Departure transition cannot consume a rocket");
            // Advance the actual flight through the real tile-world adapter.
            object world=Activator.CreateInstance(code.GetType("DimensionalTerraria.World.TerrariaCollisionWorld")!)!;
            for(int i=0;i<24;i++)Call(flight,"Update",world,1f/60,Vector3.Zero,false,12f);
            Call(flight,"Look",.8f,.4f);
            Check((bool)Call(firework,"TryLaunch",player,flight,V(0,0))! && (bool)Call(firework,"ConsumeItem",player)!,"A 3D rocket must start boost and permit consumption");
            Vector3 boost=(Vector3)Get(flight,"BoostVelocity")!,forward=(Vector3)Get(flight,"Forward")!;
            Check(Vector3.Dot(Vector3.Normalize(boost),forward)>.9999f && Math.Abs(boost.Length()-32)<.001f,"3D thrust must follow the crosshair, including depth and pitch");
            Set(player,"noItems",true);
            Check(!(bool)Call(firework,"TryLaunch",player,flight,V(0,0))! && !(bool)Call(firework,"ConsumeItem",player)!,"Item-use restrictions must preserve rockets");Set(player,"noItems",false);
            Check(!((string)Get(firework,"Texture")!).Contains("Terraria/Images"),"Firework must use its Minecraft texture");
            Console.WriteLine("PASS real firework item: native defaults, mouse/crosshair directions, once per animation, speed cap, consumption permission and invalid/restricted/departing uses preserve items");
        }
        finally
        {
            if(loaded)Call(system,"UnloadTargetingHooks");flightProperty.SetValue(system,null);
            hitEffects.RemoveEventHandler(null,effectShim);
            register.Invoke(null,new[]{oldSystem});Array.Copy(saved,npcs,saved.Length);
            main.GetField("dedServ")!.SetValue(null,true);Set(player,"noItems",false);
        }
    }
}
