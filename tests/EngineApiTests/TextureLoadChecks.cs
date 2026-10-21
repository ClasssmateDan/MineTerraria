using System.Reflection;

internal static class TextureLoadChecks
{
    internal static void Run(Assembly game, Assembly code, string package)
    {
        Type fileType = game.GetType("Terraria.ModLoader.Core.TmodFile")!;
        object file = Activator.CreateInstance(fileType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, new object?[] { package, null, null }, null)!;
        using var lease = (IDisposable)fileType.GetMethod("Open")!.Invoke(file, null)!;
        Type buildProperties=game.GetType("Terraria.ModLoader.Core.BuildProperties")!;
        object properties=buildProperties.GetMethod("ReadModFile",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new[]{file})!;
        if((string)fileType.GetProperty("Name")!.GetValue(file)! !="DimensionalTerraria" ||
            (string)buildProperties.GetField("displayName",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(properties)! !="MineTerraria")
            throw new InvalidOperationException("Public rename must retain the original internal save identity");
        Console.WriteLine("PASS packaged public name: MineTerraria display metadata, DimensionalTerraria save identity retained");
        object mod = Activator.CreateInstance(code.GetType("DimensionalTerraria.DimensionalTerraria")!)!;
        game.GetType("Terraria.ModLoader.Mod")!.GetProperty("File", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(mod, file);
        Type pixels = code.GetType("DimensionalTerraria.Systems.StorageIconPixels")!;
        MethodInfo faceReader = pixels.GetMethod("ReadFace", BindingFlags.Static | BindingFlags.NonPublic)!;
        MethodInfo thumbnail = pixels.GetMethod("GolemThumbnail", BindingFlags.Static | BindingFlags.NonPublic)!;
        MethodInfo cube = code.GetType("DimensionalTerraria.Core.ContentPixelArt")!.GetMethod("TexturedCube")!;
        int callerThread = Environment.CurrentManagedThreadId;

        // This is the same background-thread context as ModContent.Load, using
        // the real .tmod streams and native ImageIO, without any graphics device.
        Task<byte[][]> load = Task.Factory.StartNew(() =>
        {
            if (Environment.CurrentManagedThreadId == callerThread) throw new InvalidOperationException("Texture regression must run off the main thread.");
            byte[] Read(string path) => (byte[])faceReader.Invoke(null, new[] { mod, path })!;
            byte[] planks = Read("Textures/Minecraft/oak_planks");
            byte[] rocket = Read("Textures/Minecraft/firework_rocket");
            byte[] ingot=Read("Textures/Minecraft/netherite_ingot");
            if(ingot.Length!=1024 || ingot[3]!=0 || !Enumerable.Range(0,256).Any(i=>ingot[i*4+3]==255))
                throw new InvalidOperationException("Netherite must keep its original transparent 16x16 texture");
            if(rocket.Length!=1024 || rocket[3]!=0 || !Enumerable.Range(0,256).Any(i=>rocket[i*4+3]==255))
                throw new InvalidOperationException("Firework must retain its transparent original 16x16 item texture");
            byte[] front = Read("DimensionalTerraria/Textures/Minecraft/crafting_table_front");
            byte[] side = Read("Textures/Minecraft/crafting_table_side");
            byte[] top = Read("Textures/Minecraft/crafting_table_top");
            foreach (byte[] face in new[] { planks, front, side, top })
                if (face.Length != 16 * 16 * 4 || Enumerable.Range(0, 256).Any(i => face[i * 4 + 3] != 255))
                    throw new InvalidOperationException("Packaged block faces must remain native-size, opaque RGBA.");
            byte[] plankIcon = (byte[])cube.Invoke(null, new object[] { planks, planks, planks })!;
            byte[] tableIcon = (byte[])cube.Invoke(null, new object[] { top, front, side })!;
            byte[] golemIcon = (byte[])thumbnail.Invoke(null, new[] { mod })!;
            byte[] boat = Read("Textures/Minecraft/oak_boat");
            byte[] boatAtlas = (byte[])code.GetType("DimensionalTerraria.Core.ContentPixelArt")!.GetMethod("BoatTileAtlas")!.Invoke(null, new object[] { boat })!;
            if (boat.Length != 16 * 16 * 4 || boatAtlas.Length != 54 * 36 * 4 || boat[3] != 0)
                throw new InvalidOperationException("Boat must retain the native transparent item texture and six padded tile frames.");
            for (int fy = 0; fy < 2; fy++) for (int fx = 0; fx < 3; fx++)
                if (!Enumerable.Range(0, 256).Any(i => boatAtlas[((fx * 18 + i % 16) + (fy * 18 + i / 16) * 54) * 4 + 3] > 0))
                    throw new InvalidOperationException("Every boat tile frame must contain part of the actual hull.");
            foreach (string invalid in new[] { "Textures/IronGolem", "Textures/MissingIconSource" })
            {
                try { Read(invalid); throw new InvalidOperationException("Invalid icon input was accepted."); }
                catch (TargetInvocationException ex) when (ex.InnerException is InvalidDataException or FileNotFoundException or KeyNotFoundException) { }
            }
            byte[] dust = Read("Textures/Minecraft/redstone"), red = Read("Textures/Minecraft/redstone_block");
            byte[] pistonSide = Read("Textures/Minecraft/piston_side"), inner = Read("Textures/Minecraft/piston_inner");
            byte[] normalTop = Read("Textures/Minecraft/piston_top"), stickyTop = Read("Textures/Minecraft/piston_top_sticky");
            Read("Textures/Minecraft/piston_bottom");
            if (dust.Length != 1024 || !Enumerable.Range(0, 256).Any(i => dust[i * 4 + 3] == 0))
                throw new InvalidOperationException("Native redstone item must retain its transparent background.");
            MethodInfo atlasBuilder = code.GetType("DimensionalTerraria.Core.ContentPixelArt")!.GetMethod("PistonAtlas")!;
            foreach ((bool sticky, bool head) in new[] { (false, false), (true, false), (false, true) })
            {
                byte[] atlas = (byte[])atlasBuilder.Invoke(null, new object[] { pistonSide, inner, normalTop, stickyTop, sticky, head })!;
                if (atlas.Length != 72 * 36 * 4 || atlas[(16 + 16 * 72) * 4 + 3] != 0 ||
                    Enumerable.Range(0, atlas.Length / 4).Count(i => atlas[i * 4 + 3] > 0) < 500)
                    throw new InvalidOperationException("Piston atlases need eight visible safe frames and transparent padding.");
            }
            byte[] normalIcon = (byte[])cube.Invoke(null, new object[] { normalTop, pistonSide, pistonSide })!;
            byte[] stickyIcon = (byte[])cube.Invoke(null, new object[] { stickyTop, pistonSide, pistonSide })!;
            if (normalIcon.SequenceEqual(stickyIcon)) throw new InvalidOperationException("Normal and sticky piston icons must be visibly distinct.");
            byte[] redIcon = (byte[])cube.Invoke(null, new object[] { red, red, red })!;
            if (redIcon.Length != 24 * 24 * 4 || redIcon[3] != 0) throw new InvalidOperationException("Redstone block icon must be a native textured transparent cube.");
            byte[] pulseOff = Read("Textures/Minecraft/observer_back"), pulseOn = Read("Textures/Minecraft/observer_back_on");
            byte[] pulseSide = Read("Textures/Minecraft/observer_side");
            byte[] pulseAtlas = (byte[])code.GetType("DimensionalTerraria.Core.ContentPixelArt")!.GetMethod("PulseAtlas")!
                .Invoke(null, new object[] { pulseOff, pulseOn })!;
            byte[] pulseIcon = (byte[])cube.Invoke(null, new object[] { pulseSide, pulseOn, pulseSide })!;
            if (pulseOff.SequenceEqual(pulseOn) || pulseAtlas.Length != 16 * 36 * 4 ||
                !pulseAtlas.Take(1024).SequenceEqual(pulseOff) || !pulseAtlas.Skip(16 * 18 * 4).Take(1024).SequenceEqual(pulseOn) ||
                pulseAtlas.Skip(1024).Take(16 * 2 * 4).Any(b => b != 0) || pulseIcon.Length != 24 * 24 * 4 || pulseIcon[3] != 0)
                throw new InvalidOperationException("Pulser needs distinct native on/off frames, safe animation padding and a cube icon prepared without graphics reads.");
            byte[] hopper = Read("Textures/Minecraft/hopper"), hopperTop = Read("Textures/Minecraft/hopper_top");
            byte[] hopperInside = Read("Textures/Minecraft/hopper_inside"), hopperOutside = Read("Textures/Minecraft/hopper_outside");
            if (new[] { hopper, hopperTop, hopperInside, hopperOutside }.Any(face => face.Length != 1024) ||
                !Enumerable.Range(0, 256).Any(i => hopper[i * 4 + 3] == 0) || hopperTop[(8 + 8 * 16) * 4 + 3] != 0 ||
                Enumerable.Range(0, 256).Any(i => hopperInside[i * 4 + 3] != 255 || hopperOutside[i * 4 + 3] != 255))
                throw new InvalidOperationException("Hopper must retain the original transparent icon, open-mouth top and opaque native inside/outside faces.");
            byte[] bedrock = Read("Textures/Minecraft/bedrock");
            byte[] bedrockIcon = (byte[])cube.Invoke(null, new object[] { bedrock, bedrock, bedrock })!;
            if (bedrockIcon.Length != 24 * 24 * 4 || bedrockIcon[3] != 0) throw new InvalidOperationException("Bedrock icon must be a closed textured cube");
            MethodInfo imageReader = pixels.GetMethod("Read", BindingFlags.Static | BindingFlags.NonPublic)!;
            object[] chestSource = { mod, "Textures/Minecraft/chest_normal", 0, 0 };
            byte[] chestPixels = (byte[])imageReader.Invoke(null, chestSource)!;
            if ((int)chestSource[2] != 64 || (int)chestSource[3] != 64) throw new InvalidOperationException("Chest must retain original 64x64 entity UV texture");
            Type chestArt=code.GetType("DimensionalTerraria.Core.ChestArt")!;
            byte[] chestAtlas=(byte[])chestArt.GetMethod("Atlas")!.Invoke(null,new object[]{chestPixels})!;
            if (chestAtlas.Length!=36*114*4 || chestAtlas[3]!=0) throw new InvalidOperationException("Chest needs three complete native 2x2 animation states");
            for(int state=0;state<3;state++) for(int row=0;row<2;row++) for(int col=0;col<2;col++)
                if(!Enumerable.Range(0,256).Any(i=>chestAtlas[((col*18+i%16)+(state*38+row*18+i/16)*36)*4+3]>0))
                    throw new InvalidOperationException("Opening must not make any chest quadrant disappear");
            foreach (int faceIndex in new[]{0,1,2})
            {
                byte[] chestFace=(byte[])chestArt.GetMethod("Face")!.Invoke(null,new object[]{chestPixels,faceIndex})!;
                if (Enumerable.Range(0,256).Any(i=>chestFace[i*4+3]!=255)) throw new InvalidOperationException("Chest UV faces must sample actual opaque model regions");
            }
            byte[] villagerEgg=(byte[])code.GetType("DimensionalTerraria.Core.ContentPixelArt")!.GetMethod("VillagerEgg")!.Invoke(null,null)!;
            if(villagerEgg.Length!=16*24*4 || villagerEgg[3]!=0)throw new InvalidOperationException("Villager egg needs the existing transparent native egg silhouette");
            foreach (var asset in new[] {
                ("Textures/Equipment/HigherDimensionalHelmet_Head",40,1120),
                ("Textures/Equipment/HigherDimensionalChestplate_Body",360,224),
                ("Textures/Equipment/HigherDimensionalLeggings_Legs",40,1120),
                ("Textures/Minecraft/netherite_layer_1",64,32), ("Textures/Minecraft/netherite_layer_2",64,32),
                ("Textures/Equipment/HigherDimensionalPickaxe",32,32),
                ("Textures/Equipment/HigherDimensionalHelmet",32,32),
                ("Textures/Equipment/HigherDimensionalChestplate",32,32),
                ("Textures/Equipment/HigherDimensionalLeggings",32,32) })
            {
                object[] parameters = { mod, asset.Item1, 0, 0 };
                byte[] image = (byte[])imageReader.Invoke(null, parameters)!;
                if ((int)parameters[2] != asset.Item2 || (int)parameters[3] != asset.Item3 ||
                    !Enumerable.Range(0, image.Length / 4).Any(i => image[i * 4 + 3] > 0))
                    throw new InvalidOperationException("Missing or malformed packaged equipment asset: " + asset.Item1);
            }
            foreach (string part in new[] { "Head", "Body", "Legs" })
            {
                object[] parameters = { mod, "Textures/Equipment/SteveArmor_" + part, 0, 0 };
                byte[] image = (byte[])imageReader.Invoke(null, parameters)!;
                if ((int)parameters[2] != 1254 || (int)parameters[3] != 1254)
                    throw new InvalidOperationException("Steve equipment must share his full original atlas coordinates");
                for (int frame = 0; frame < 8; frame++)
                {
                    int count = 0, x0 = frame % 4 * (1254 / 4), y0 = frame / 4 * (1254 / 2);
                    for (int y = y0; y < y0 + 1254 / 2; y++) for (int x = x0; x < x0 + 1254 / 4; x++)
                        if (image[(x + y * 1254) * 4 + 3] > 0) count++;
                    if (count < 500) throw new InvalidOperationException("Missing Steve armor overlay frame: " + part + " " + frame);
                }
            }
            MethodInfo clean=code.GetType("DimensionalTerraria.Core.SpriteEdges")!.GetMethod("Clean")!;
            foreach(string name in new[]{"Steve","Villager","IronGolem","Sheep"})
            {
                object[] args={mod,"Textures/"+name,0,0}; byte[] original=(byte[])imageReader.Invoke(null,args)!;
                int width=(int)args[2],height=(int)args[3];
                byte[] trimmed=(byte[])clean.Invoke(null,new object?[]{original,width,height,null})!;
                int oldOpaque=0,newOpaque=0;
                for(int p=0;p<original.Length;p+=4)
                {
                    if(original[p+3]>=240)oldOpaque++;
                    if(trimmed[p+3]!=0 && trimmed[p+3]!=255)throw new InvalidOperationException("Residual matte alpha");
                    if(trimmed[p+3]==0 && (trimmed[p]!=0 || trimmed[p+1]!=0 || trimmed[p+2]!=0))throw new InvalidOperationException("Transparent RGB would create an additive halo");
                    if(trimmed[p+3]==255){newOpaque++;if(trimmed[p]!=original[p] || trimmed[p+1]!=original[p+1] || trimmed[p+2]!=original[p+2])throw new InvalidOperationException("Interior colors changed");}
                }
                if(newOpaque<oldOpaque*.9 || newOpaque>=oldOpaque)throw new InvalidOperationException("Edge cleanup must preserve the body and remove boundary pixels");
                if(name=="Steve") foreach(string part in new[]{"Head","Body","Legs"})
                {
                    object[] overlayArgs={mod,"Textures/Equipment/SteveArmor_"+part,0,0};
                    byte[] overlay=(byte[])imageReader.Invoke(null,overlayArgs)!;
                    byte[] safe=(byte[])clean.Invoke(null,new object[]{overlay,width,height,original})!;
                    for(int p=3;p<safe.Length;p+=4)if(safe[p]>trimmed[p])throw new InvalidOperationException("Armor protrudes beyond cleaned Steve silhouette");
                }
            }
            return new[] { plankIcon, tableIcon, golemIcon };
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        if (!load.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Background icon preparation must not await a graphics-thread queue.");
        byte[][] icons = load.GetAwaiter().GetResult();
        Console.WriteLine("PASS packaged netherite: original transparent 16x16 ingot decoded off the graphics thread");
        Console.WriteLine("PASS packaged firework: original transparent 16x16 rocket decoded off the graphics thread");
        Console.WriteLine("PASS packaged boat: native transparent oak boat texture and all six atlas frames prepared without a GPU");
        Console.WriteLine("PASS packaged icon preparation: four native faces read on a background thread without a graphics device");
        Console.WriteLine("PASS packaged redstone: native transparent gem and redstone block cube prepare on the loader thread");
        Console.WriteLine("PASS packaged pistons: all original faces, distinct normal/sticky icons and three eight-frame atlases prepare without GPU reads");
        Console.WriteLine("PASS packaged pulser: original on/off/side faces, padded two-state animation and cube icon prepared on the loader thread");
        Console.WriteLine("PASS packaged hopper: native transparent icon, open rim, inside and outside faces read on the loader thread without GPU access");
        Console.WriteLine("PASS packaged bedrock: original face and closed cube prepared on loader thread without GPU access");
        Console.WriteLine("PASS packaged projection equipment: four item icons, three animated armor sheets and two original armor UV atlases read on loader thread");
        Console.WriteLine("PASS packaged Steve armor: three aligned 1254x1254 overlays with eight nonempty poses each, decoded off the graphics thread");
        Console.WriteLine("PASS packaged chest and villager egg: original 64x64 chest UV source, opaque model faces, padded native tile atlas and transparent egg prepare off the graphics thread");
        Console.WriteLine("PASS packaged sprite cleanup: four bodies and three Steve overlays have zero transparent RGB, no matte alpha and intact interior colors");

        foreach (byte[] icon in icons.Take(2))
            if (icon.Length != 24 * 24 * 4 || icon[3] != 0 || Enumerable.Range(0, 24 * 24).Count(i => icon[i * 4 + 3] > 0) < 200)
                throw new InvalidOperationException("Block icons must retain their textured cube and transparent borders.");
        if (icons[0].SequenceEqual(icons[1])) throw new InvalidOperationException("Planks and the crafting table must retain distinct source textures.");
        Console.WriteLine("PASS packaged block icons: distinct 24x24 textured cubes with transparent borders");

        byte[] golem = icons[2];
        if (golem.Length != 32 * 40 * 4 || Enumerable.Range(0, 32 * 40).Count(i => golem[i * 4 + 3] >= 128) < 100 ||
            golem.Take(32 * 4).Any(b => b != 0) || golem.Skip(39 * 32 * 4).Any(b => b != 0))
            throw new InvalidOperationException("Golem thumbnail must be one visible resting pose within its 32x40 transparent canvas.");
        Console.WriteLine("PASS packaged golem thumbnail: cropped resting pose generated on the loader thread, no GPU readback");
    }
}
