using System.Collections;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;

internal static class PresentationChecks
{
    const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
    internal static void Run(Assembly game,Assembly code,object contentMod)
    {
        Type main=game.GetType("Terraria.Main")!;
        object? Get(object o,string name)=>o.GetType().GetField(name,All)?.GetValue(o) ?? o.GetType().GetProperty(name,All)!.GetValue(o);
        void Check(bool pass,string why){if(!pass)throw new InvalidOperationException(why);}
        object nativeMain=RuntimeHelpers.GetUninitializedObject(main);
        var top=main.GetField("bgTopY",All)!;var scale=main.GetField("bgScale",All)!;var parallax=main.GetField("bgParallax",All)!;var adjust=main.GetField("scAdj",All)!;
        top.SetValue(nativeMain,777);scale.SetValue(null,3.3f);parallax.SetValue(nativeMain,.123d);adjust.SetValue(nativeMain,7f);
        object camera=code.GetType("DimensionalTerraria.Core.SurfaceBackdrop")!.GetMethod("Camera")!.Invoke(null,new object[]{220f,250f,1200,1080})!;
        Type layout=code.GetType("DimensionalTerraria.World.NativeBackgroundLayout")!;
        object forest=Enum.Parse(code.GetType("DimensionalTerraria.Core.ProjectedBiome")!,"Forest");
        object result=layout.GetMethod("Sample",All)!.Invoke(null,new[]{nativeMain,(object)258,0,forest,camera})!;
        float expected=186.25f+((int)(-.67*1800+850)+(int)(float)camera.GetType().GetProperty("Adjustment")!.GetValue(camera)!)/16f;
        Check(Math.Abs((float)Get(result,"Item1")!-expected)<.01f && (float)Get(result,"Item2")! ==2.5f,"Native background style offsets must map to world coordinates at native scale");
        Check((int)top.GetValue(nativeMain)! ==777 && (float)scale.GetValue(null)! ==3.3f && (double)parallax.GetValue(nativeMain)! ==.123d && (float)adjust.GetValue(nativeMain)! ==7f,"Background sampling must restore every native scalar");
        Console.WriteLine("PASS native background layout: actual SetBackgroundOffsets binding, projected world position, native scale and all background state restored without graphics");

        Type modType=code.GetType("DimensionalTerraria.DimensionalTerraria")!;
        modType.GetMethod("RegisterBindings",All)!.Invoke(null,new[]{contentMod});
        Check(modType.GetField("SpawnRecordingWave",All) is null,"Recording shortcut must be removed");
        foreach(var pair in new[]{("ToggleDimension","V"),("ReturnToAnchor","B"),("OpenBackpack","E")})
        {
            object binding=modType.GetField(pair.Item1,All)!.GetValue(null)!;
            Check((string)binding.GetType().GetProperty("DefaultBinding",All)!.GetValue(binding)! ==pair.Item2,"Gameplay binding must survive release cleanup");
        }
        game.GetType("Terraria.ModLoader.ContentInstance")!.GetMethod("Register",All)!.Invoke(null,new[]{Activator.CreateInstance(code.GetType("DimensionalTerraria.Configuration.DimensionConfig")!)});
        Console.WriteLine("PASS release bindings: only dimension, return and inventory controls remain, recording shortcut absent");
    }
}
