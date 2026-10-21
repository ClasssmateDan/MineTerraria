using System.Reflection;
using System.Runtime.Loader;

try
{
if (args.Length is not (2 or 3)) throw new ArgumentException("Expected tModLoader directory, compiled mod DLL and optional packed mod for texture-load checks.");
string root = Path.GetFullPath(args[0]);
string[] assemblies = Directory.GetFiles(root, "*.dll", SearchOption.AllDirectories);
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    string? path = assemblies.FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == name.Name);
    return path is null ? null : AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
};
Assembly game = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(root, "tModLoader.dll"));
// Normal game startup initializes these ID classes before ItemLoader resets
// their sets. Reflection-only setup must also mark the CLR cctors initialized.
foreach(string idClass in new[]{"Terraria.ID.AmmoID+Sets","Terraria.ID.WallID+Sets"})
    System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(game.GetType(idClass)!.TypeHandle);
Assembly mod = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(args[1]));
if (args.Length == 3)
{
    TextureLoadChecks.Run(game, mod, Path.GetFullPath(args[2]));
    return;
}
Type native = mod.GetType("DimensionalTerraria.World.NativeInteractions", throwOnError: true)!;
// Reading these fields initializes the real adapters and validates the private
// method signatures against this exact engine version, without starting a game.
foreach (string field in new[] { "Mine", "Pickup", "RestoreUseCount", "CutIgnore", "Cut" })
{
    if (native.GetField(field, BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) is not Delegate binding)
        throw new InvalidOperationException("Could not bind native interaction: " + field);
    Console.WriteLine("PASS native binding: " + binding.Method.Name);
}

// Exercise the real mod's NPC frame configuration against vanilla's frame table
// without creating a game, graphics device or audio engine.
game.GetType("Terraria.Program")!.GetField("SavePath")!.SetValue(null,
    Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1]))!, "api-test-profile"));
int[] frames = (int[])game.GetType("Terraria.Main")!.GetField("npcFrameCount")!.GetValue(null)!;
int vanillaDummy = Convert.ToInt32(game.GetType("Terraria.ID.NPCID")!.GetField("TargetDummy")!.GetRawConstantValue());
Type dummyType = mod.GetType("DimensionalTerraria.NPCs.DebugTargetDummy", throwOnError: true)!;
object dummy = Activator.CreateInstance(dummyType)!;
object npc = Activator.CreateInstance(game.GetType("Terraria.NPC")!)!;
dummyType.GetProperty("Entity", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(dummy, npc);
int testSlot = frames.Length - 1;
npc.GetType().GetField("type")!.SetValue(npc, testSlot);
dummyType.GetMethod("SetStaticDefaults")!.Invoke(dummy, null);
if (frames[testSlot] != frames[vanillaDummy] || frames[testSlot] <= 1)
    throw new InvalidOperationException("Dummy texture must be divided using the native animation frame count.");
Console.WriteLine($"PASS real dummy sprite sheet: {frames[testSlot]} native frames");
FieldInfo frameField = npc.GetType().GetField("frame")!;
frameField.SetValue(npc, Activator.CreateInstance(frameField.FieldType, new object[] { 0, 168, 40, 616 }));
dummyType.GetMethod("FindFrame")!.Invoke(dummy, new object[] { 56 });
object frame = frameField.GetValue(npc)!;
if ((int)frameField.FieldType.GetField("Y")!.GetValue(frame)! != 0 || (int)frameField.FieldType.GetField("Height")!.GetValue(frame)! != 56)
    throw new InvalidOperationException("Dummy must draw one resting frame rather than the entire sheet.");
Console.WriteLine("PASS real dummy frame selection: one resting pose");

Type sheepType = mod.GetType("DimensionalTerraria.NPCs.MinecraftSheep", throwOnError: true)!;
object sheep = Activator.CreateInstance(sheepType)!;
object sheepNpc = Activator.CreateInstance(npc.GetType())!;
sheepType.GetProperty("Entity", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(sheep, sheepNpc);
npc.GetType().GetField("type")!.SetValue(sheepNpc, testSlot);
sheepType.GetMethod("SetDefaults")!.Invoke(sheep, null);
sheepType.GetMethod("SetStaticDefaults")!.Invoke(sheep, null);
if ((int)npc.GetType().GetField("lifeMax")!.GetValue(sheepNpc)! != 50 ||
    (int)npc.GetType().GetField("damage")!.GetValue(sheepNpc)! != 0 ||
    (bool)npc.GetType().GetField("friendly")!.GetValue(sheepNpc)! || frames[testSlot] != 8)
    throw new InvalidOperationException("Sheep must be passive but attackable, with 50 HP, no damage and eight frames.");
Console.WriteLine("PASS real sheep defaults: 50 HP, attackable critter, zero contact damage, eight animation poses");
sheepType.GetMethod("ApplyDifficultyAndPlayerScaling")!.Invoke(sheep, new object[] { 4, 4f, 4f });
if ((int)npc.GetType().GetField("lifeMax")!.GetValue(sheepNpc)! != 50)
    throw new InvalidOperationException("Sheep health must remain 50 after difficulty scaling.");
Console.WriteLine("PASS real sheep health remains 50 after difficulty scaling");

Type playerType = game.GetType("Terraria.Player")!;
object attacker = Activator.CreateInstance(playerType)!;
npc.GetType().GetProperty("ModNPC")!.SetValue(sheepNpc, sheep);
npc.GetType().GetField("active")!.SetValue(sheepNpc, true);
npc.GetType().GetField("life")!.SetValue(sheepNpc, 50);
MethodInfo eligible = mod.GetType("DimensionalTerraria.World.NativeCombat")!.GetMethod("Eligible")!;
if (!(bool)eligible.Invoke(null, new[] { attacker, sheepNpc })!)
    throw new InvalidOperationException("An ordinary player must be able to select a sheep for 3D attacks.");
playerType.GetField("dontHurtCritters")!.SetValue(attacker, true);
if ((bool)eligible.Invoke(null, new[] { attacker, sheepNpc })!)
    throw new InvalidOperationException("The native critter protection accessory must still protect sheep.");
if ((bool)mod.GetType("DimensionalTerraria.Systems.DimensionSystem")!
    .GetMethod("IsHostile", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new[] { sheepNpc })!)
    throw new InvalidOperationException("Passive sheep must not enter the detached enemy pursuit controller.");
Console.WriteLine("PASS sheep targeting: attackable in 3D, native critter protection retained, no enemy pursuit");

Type golemType = mod.GetType("DimensionalTerraria.NPCs.MinecraftIronGolem", throwOnError: true)!;
object golem = Activator.CreateInstance(golemType)!;
object golemNpc = Activator.CreateInstance(npc.GetType())!;
golemType.GetProperty("Entity", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(golem, golemNpc);
npc.GetType().GetField("type")!.SetValue(golemNpc, testSlot);
npc.GetType().GetProperty("ModNPC")!.SetValue(golemNpc, golem);
golemType.GetMethod("SetDefaults")!.Invoke(golem, null);
golemType.GetMethod("SetStaticDefaults")!.Invoke(golem, null);
int GolemInt(string field) => (int)npc.GetType().GetField(field)!.GetValue(golemNpc)!;
if (GolemInt("height") != 76 || GolemInt("width") != 44 || GolemInt("lifeMax") != 500 || GolemInt("defense") != 20 ||
    GolemInt("damage") != 0 || GolemInt("aiStyle") != -1 || frames[testSlot] != 8 ||
    (bool)npc.GetType().GetField("friendly")!.GetValue(golemNpc)! || (bool)npc.GetType().GetField("noGravity")!.GetValue(golemNpc)!)
    throw new InvalidOperationException("Iron golems need a tall attackable neutral body, native gravity and no contact/jumping AI.");
var targetFlags = game.GetType("Terraria.ID.NPCID+Sets")!;
if (!((bool[])targetFlags.GetField("TakesDamageFromHostilesWithoutBeingFriendly")!.GetValue(null)!)[testSlot] ||
    ((bool[])targetFlags.GetField("CountsAsCritter")!.GetValue(null)!)[testSlot])
    throw new InvalidOperationException("Neutral golems must accept hostile NPC damage without being classified as critters.");
golemType.GetMethod("ApplyDifficultyAndPlayerScaling")!.Invoke(golem, new object[] { 4, 4f, 4f });
if (GolemInt("lifeMax") != 500) throw new InvalidOperationException("Golem health must remain 500 across difficulty scaling.");
Console.WriteLine("PASS iron golem defaults: 44x76 body, 500 HP, 20 defense, native gravity, eight poses and hostile-NPC damage enabled");
Type lootType = game.GetType("Terraria.ModLoader.NPCLoot")!;
object lootDatabase = Activator.CreateInstance(game.GetType("Terraria.GameContent.ItemDropRules.ItemDropDatabase")!)!;
object golemLoot = Activator.CreateInstance(lootType, new[] { (object)testSlot, lootDatabase })!;
golemType.GetMethod("ModifyNPCLoot")!.Invoke(golem, new[] { golemLoot });
object[] golemDrops = ((System.Collections.IEnumerable)lootType.GetMethod("Get")!.Invoke(golemLoot, new object[] { false })!).Cast<object>().ToArray();
int ironBar = Convert.ToInt32(game.GetType("Terraria.ID.ItemID")!.GetField("IronBar")!.GetValue(null));
if (golemDrops.Length != 1) throw new InvalidOperationException("Golems need one native death-drop rule, without duplicate manual drops.");
object dropRule = golemDrops.Single();
int DropInt(string field) => (int)dropRule.GetType().GetField(field)!.GetValue(dropRule)!;
if (DropInt("itemId") != ironBar || DropInt("chanceDenominator") != 1 || DropInt("chanceNumerator") != 1 ||
    DropInt("amountDroppedMinimum") != 3 || DropInt("amountDroppedMaximum") != 5)
    throw new InvalidOperationException("Every golem death must drop 3-5 real Iron Bars through the native loot pipeline.");
Console.WriteLine("PASS real golem death-loot registration: exactly one guaranteed 3-5 Iron Bar rule");
if ((bool)mod.GetType("DimensionalTerraria.Systems.DimensionSystem")!
    .GetMethod("IsHostile", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new[] { golemNpc })! ||
    (bool)golemType.GetMethod("CanHitPlayer")!.Invoke(golem, new object[] { attacker, 0 })!)
    throw new InvalidOperationException("Neutral golems must bypass detached enemy wandering and cannot deal passive contact damage.");
npc.GetType().GetField("active")!.SetValue(golemNpc, true);
npc.GetType().GetField("life")!.SetValue(golemNpc, 500);
if (!(bool)eligible.Invoke(null, new[] { attacker, golemNpc })!)
    throw new InvalidOperationException("Players must still be able to deliberately attack golems in 3D.");
Console.WriteLine("PASS golem faction: neutral in detached AI, attackable in 3D, no passive contact damage");
FieldInfo brainField = golemType.GetField("brain", BindingFlags.NonPublic | BindingFlags.Instance)!;
int AngryPlayer() => (int)brainField.GetValue(golem)!.GetType().GetProperty("AngryPlayer")!.GetValue(brainField.GetValue(golem))!;
object weaponForHit = Activator.CreateInstance(game.GetType("Terraria.Item")!)!;
object hitInfo = Activator.CreateInstance(game.GetType("Terraria.NPC+HitInfo")!)!;
playerType.GetField("whoAmI")!.SetValue(attacker, 0);
Type retaliationMain = game.GetType("Terraria.Main")!;
Array retaliationNpcs = (Array)retaliationMain.GetField("npc")!.GetValue(null)!;
for (int i = 0; i < retaliationNpcs.Length; i++)
    if (retaliationNpcs.GetValue(i) is null) retaliationNpcs.SetValue(Activator.CreateInstance(npc.GetType()), i);
Array retaliationPlayers = (Array)retaliationMain.GetField("player")!.GetValue(null)!;
playerType.GetField("active")!.SetValue(attacker, true);
retaliationPlayers.SetValue(attacker, 0);
object secondAttacker = Activator.CreateInstance(playerType)!;
playerType.GetField("active")!.SetValue(secondAttacker, true);
playerType.GetField("whoAmI")!.SetValue(secondAttacker, 1);
retaliationPlayers.SetValue(secondAttacker, 1);
golemType.GetMethod("OnHitByItem")!.Invoke(golem, new[] { attacker, weaponForHit, hitInfo, (object)0 });
if (AngryPlayer() != -1) throw new InvalidOperationException("Zero-damage item hits must not provoke retaliation.");
golemType.GetMethod("OnHitByItem")!.Invoke(golem, new[] { attacker, weaponForHit, hitInfo, (object)10 });
if (AngryPlayer() != 0) throw new InvalidOperationException("Player weapon damage must provoke the actual attacker.");
golemType.GetMethod("SetDefaults")!.Invoke(golem, null);
if (AngryPlayer() != -1) throw new InvalidOperationException("Reinitializing a golem must clear old grudges.");
object projectileForHit = Activator.CreateInstance(game.GetType("Terraria.Projectile")!)!;
projectileForHit.GetType().GetField("owner")!.SetValue(projectileForHit, 1);
projectileForHit.GetType().GetField("friendly")!.SetValue(projectileForHit, true);
golemType.GetMethod("OnHitByProjectile")!.Invoke(golem, new[] { projectileForHit, hitInfo, (object)10 });
if (AngryPlayer() != 1) throw new InvalidOperationException("Projectile damage must provoke its owner.");
golemType.GetMethod("SetDefaults")!.Invoke(golem, null);
projectileForHit.GetType().GetField("hostile")!.SetValue(projectileForHit, true);
golemType.GetMethod("OnHitByProjectile")!.Invoke(golem, new[] { projectileForHit, hitInfo, (object)10 });
if (AngryPlayer() != -1) throw new InvalidOperationException("Hostile projectiles cannot blame an arbitrary player owner.");
Console.WriteLine("PASS golem retaliation hooks: player weapon/projectile ownership, zero damage ignored, reset clears grudges");
foreach (string invalidSource in new[] { "notFriendly", "npcProj", "trap", "hostile", "inactiveOwner", "missingOwner", "negativeOwner", "zeroDamage" })
{
    golemType.GetMethod("SetDefaults")!.Invoke(golem, null);
    object invalidProjectile = Activator.CreateInstance(projectileForHit.GetType())!;
    invalidProjectile.GetType().GetField("owner")!.SetValue(invalidProjectile, 1);
    invalidProjectile.GetType().GetField("friendly")!.SetValue(invalidProjectile, invalidSource != "notFriendly");
    if (invalidSource is "npcProj" or "trap" or "hostile") invalidProjectile.GetType().GetField(invalidSource)!.SetValue(invalidProjectile, true);
    if (invalidSource == "inactiveOwner") playerType.GetField("active")!.SetValue(secondAttacker, false);
    if (invalidSource is "missingOwner" or "negativeOwner") invalidProjectile.GetType().GetField("owner")!.SetValue(invalidProjectile, invalidSource == "missingOwner" ? 255 : -1);
    golemType.GetMethod("OnHitByProjectile")!.Invoke(golem, new[] { invalidProjectile, hitInfo, (object)(invalidSource == "zeroDamage" ? 0 : 10) });
    if (AngryPlayer() != -1) throw new InvalidOperationException("A non-player damage source must not provoke the projectile's numeric owner: " + invalidSource);
    playerType.GetField("active")!.SetValue(secondAttacker, true);
}
Console.WriteLine("PASS golem projectile attribution: NPC, trap, hostile, nonfriendly, absent/inactive owners and zero damage never provoke");
object normalEnemy = Activator.CreateInstance(npc.GetType())!;
npc.GetType().GetField("active")!.SetValue(normalEnemy, true);
npc.GetType().GetField("life")!.SetValue(normalEnemy, 50);
npc.GetType().GetField("damage")!.SetValue(normalEnemy, 10);
var hostileTarget = golemType.GetMethod("HostileTarget", BindingFlags.Static | BindingFlags.NonPublic)!;
if (!(bool)hostileTarget.Invoke(null, new[] { normalEnemy })! || (bool)hostileTarget.Invoke(null, new[] { sheepNpc })! ||
    (bool)hostileTarget.Invoke(null, new[] { golemNpc })!) throw new InvalidOperationException("Golems must distinguish enemy creatures from passive mod creatures.");
npc.GetType().GetField("friendly")!.SetValue(normalEnemy, true);
if ((bool)hostileTarget.Invoke(null, new[] { normalEnemy })!) throw new InvalidOperationException("Golems must not attack friendly NPCs.");
Console.WriteLine("PASS golem hostile filtering: enemies accepted, friendlies/sheep/golems excluded");

// Exercise the actual hit hooks before any golem AI update, using live engine entities.
retaliationNpcs.SetValue(normalEnemy, 10);
golemType.GetMethod("SetDefaults")!.Invoke(golem, null);
golemType.GetMethod("OnHitByItem")!.Invoke(golem, new[] { attacker, weaponForHit, hitInfo, (object)10 });
if (AngryPlayer() != 0) throw new InvalidOperationException("Friendly nearby creatures cannot disable deliberate retaliation.");
npc.GetType().GetField("friendly")!.SetValue(normalEnemy, false);
golemType.GetMethod("OnHitByItem")!.Invoke(golem, new[] { attacker, weaponForHit, hitInfo, (object)10 });
if (AngryPlayer() != -1 || (bool)npc.GetType().GetField("chaseable")!.GetValue(golemNpc)!)
    throw new InvalidOperationException("A live nearby enemy must immediately clear old hostility on a player hit, without waiting for AI.");
projectileForHit.GetType().GetField("hostile")!.SetValue(projectileForHit, false);
golemType.GetMethod("OnHitByProjectile")!.Invoke(golem, new[] { projectileForHit, hitInfo, (object)10 });
if (AngryPlayer() != -1) throw new InvalidOperationException("Nearby monsters must suppress projectile provocation as well as melee.");
npc.GetType().GetField("active")!.SetValue(normalEnemy, false);
if (AngryPlayer() != -1) throw new InvalidOperationException("Combat-time hits must not remain pending after an enemy disappears.");
golemType.GetMethod("OnHitByProjectile")!.Invoke(golem, new[] { projectileForHit, hitInfo, (object)10 });
if (AngryPlayer() != 1) throw new InvalidOperationException("A fresh player projectile can provoke once the last enemy is gone.");
Console.WriteLine("PASS live golem hit hooks: nearby enemies suppress item/projectile grudges immediately, peaceful fresh hits still provoke");
var npcCenter = npc.GetType().GetProperty("Center")!;
object golemCenter = npcCenter.GetValue(golemNpc)!;
float golemX = (float)golemCenter.GetType().GetField("X")!.GetValue(golemCenter)!;
float golemY = (float)golemCenter.GetType().GetField("Y")!.GetValue(golemCenter)!;
npc.GetType().GetField("active")!.SetValue(normalEnemy, true);
foreach (float distance in new[] { 320f, 321f })
{
    golemType.GetMethod("SetDefaults")!.Invoke(golem, null);
    npcCenter.SetValue(normalEnemy, Activator.CreateInstance(npcCenter.PropertyType, new object[] { golemX + distance, golemY }));
    golemType.GetMethod("OnHitByItem")!.Invoke(golem, new[] { attacker, weaponForHit, hitInfo, (object)10 });
    if (AngryPlayer() != (distance <= 320 ? -1 : 0))
        throw new InvalidOperationException("Suppression must use current center-to-center 20-tile distance, not any enemy anywhere.");
}
npc.GetType().GetField("active")!.SetValue(normalEnemy, false);
Console.WriteLine("PASS live golem range gate: 20-tile boundary suppresses, an enemy farther away does not");

Type bladeType = mod.GetType("DimensionalTerraria.Items.HigherDimensionalProjection", throwOnError: true)!;
object blade = Activator.CreateInstance(bladeType)!;
object bladeItem = Activator.CreateInstance(game.GetType("Terraria.Item")!)!;
bladeType.GetProperty("Entity", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(blade, bladeItem);
bladeType.GetMethod("SetDefaults")!.Invoke(blade, null);
if ((int)bladeItem.GetType().GetField("damage")!.GetValue(bladeItem)! != 30 ||
    (float)bladeItem.GetType().GetField("scale")!.GetValue(bladeItem)! > 0.05f)
    throw new InvalidOperationException("The sword's persisted damage must be 30, with a normal-size native held sprite.");
Console.WriteLine("PASS real projection sword defaults: persistent 30 base damage and normal-size held sprite");
if (!bladeType.CustomAttributes.Any(a => a.AttributeType.Name == "LegacyNameAttribute"))
    throw new InvalidOperationException("Existing DebugBlade items need a legacy-name migration.");
Console.WriteLine("PASS projection sword retains a legacy mapping for old DebugBlade items");

bladeItem.GetType().GetProperty("ModItem")!.SetValue(bladeItem, blade);
Type rectangleType = frameField.FieldType;
object rawSwordFrame = Activator.CreateInstance(rectangleType, new object[] { 0, 0, 1254, 1254 })!;
MethodInfo animationFrame = mod.GetType("DimensionalTerraria.Systems.ProjectionSwordAnimation")!
    .GetMethod("AnimationFrame", BindingFlags.Static | BindingFlags.NonPublic)!;
object logicalSwordFrame = animationFrame.Invoke(null, new[] { bladeItem, rawSwordFrame })!;
MethodInfo nativeSwing = playerType.GetMethod("ItemCheck_ApplyUseStyle_Inner", BindingFlags.Instance | BindingFlags.NonPublic)!;
FieldInfo positionField = playerType.GetField("position")!, locationField = playerType.GetField("itemLocation")!;
object fighter = Activator.CreateInstance(playerType)!;
positionField.SetValue(fighter, Activator.CreateInstance(positionField.FieldType, new object[] { 200f, 300f }));
playerType.GetField("itemAnimationMax")!.SetValue(fighter, 18);
foreach (int facing in new[] { -1, 1 })
foreach (float gravity in new[] { -1f, 1f })
for (int tick = 1; tick <= 18; tick++)
{
    playerType.GetField("direction")!.SetValue(fighter, facing);
    playerType.GetField("gravDir")!.SetValue(fighter, gravity);
    playerType.GetField("itemAnimation")!.SetValue(fighter, tick);
    nativeSwing.Invoke(fighter, new[] { (object)0f, bladeItem, logicalSwordFrame });
    object location = locationField.GetValue(fighter)!;
    float x = (float)locationField.FieldType.GetField("X")!.GetValue(location)!;
    float y = (float)locationField.FieldType.GetField("Y")!.GetValue(location)!;
    if (Math.Abs(x - 210) > 48 || Math.Abs(y - 321) > 48)
        throw new InvalidOperationException($"Sword swing left the character: facing={facing}, gravity={gravity}, tick={tick}, xy={x},{y}");
}
object ordinaryItem = Activator.CreateInstance(bladeItem.GetType())!;
object ordinaryFrame = animationFrame.Invoke(null, new[] { ordinaryItem, rawSwordFrame })!;
if ((int)rectangleType.GetField("Width")!.GetValue(ordinaryFrame)! != 1254)
    throw new InvalidOperationException("The sword correction must not change other items' native frames.");
Console.WriteLine("PASS real native sword swing: 72 facing/gravity/animation poses remain by the body; other items unchanged");

Type steveType = mod.GetType("DimensionalTerraria.Players.SteveCharacter")!;
Type tagType = game.GetType("Terraria.ModLoader.IO.TagCompound")!;
object ordinaryCharacter = Activator.CreateInstance(steveType)!, steveCharacter = Activator.CreateInstance(steveType)!;
steveType.GetMethod("ChooseSteve", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(steveCharacter, null);
object steveTag = Activator.CreateInstance(tagType)!, ordinaryTag = Activator.CreateInstance(tagType)!;
steveType.GetMethod("SaveData")!.Invoke(steveCharacter, new[] { steveTag });
steveType.GetMethod("SaveData")!.Invoke(ordinaryCharacter, new[] { ordinaryTag });
Type tagIO = game.GetType("Terraria.ModLoader.IO.TagIO")!;
using (var savedCharacter = new MemoryStream())
{
    tagIO.GetMethod("ToStream", new[] { tagType, typeof(Stream), typeof(bool) })!
        .Invoke(null, new object[] { steveTag, savedCharacter, false });
    using var savedInput = new MemoryStream(savedCharacter.ToArray());
    steveTag = tagIO.GetMethod("FromStream", new[] { typeof(Stream), typeof(bool) })!
        .Invoke(null, new object[] { savedInput, false })!;
}
object reloadedSteve = Activator.CreateInstance(steveType)!;
steveType.GetMethod("Initialize")!.Invoke(reloadedSteve, null);
steveType.GetMethod("LoadData")!.Invoke(reloadedSteve, new[] { steveTag });
if (!(bool)steveType.GetProperty("IsSteve")!.GetValue(reloadedSteve)! ||
    (bool)steveType.GetProperty("IsSteve")!.GetValue(ordinaryCharacter)!)
    throw new InvalidOperationException("Steve must persist per character without changing ordinary characters.");
steveType.GetMethod("LoadData")!.Invoke(reloadedSteve, new[] { ordinaryTag });
if ((bool)steveType.GetProperty("IsSteve")!.GetValue(reloadedSteve)!)
    throw new InvalidOperationException("Old or ordinary saves without the Steve marker must keep the original appearance.");
Console.WriteLine("PASS independent Steve character: saves and reloads marker, ordinary and legacy characters remain ordinary");

// Reproduce ReLogic's pending-asset -> loaded-asset transition using dimension
// data rather than a Texture2D, so this regression needs no graphics device.
Assembly assetAssembly = AssemblyLoadContext.Default.LoadFromAssemblyName(
    game.GetReferencedAssemblies().Single(a => a.Name == "ReLogic"));
Type assetType = assetAssembly.GetType("ReLogic.Content.Asset`1")!.MakeGenericType(typeof(int[]));
object pendingAsset = Activator.CreateInstance(assetType, BindingFlags.Instance | BindingFlags.NonPublic,
    null, new object[] { "pending-sprite-sheet" }, null)!;
assetType.GetProperty("DefaultValue")!.SetValue(null, new[] { 1, 1 });
assetType.GetMethod("SetToLoadingState", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(pendingAsset, null);
Type layoutType = mod.GetType("DimensionalTerraria.Core.SpriteSheetLayout")!;
MethodInfo slice = layoutType.GetMethod("Slice")!, tryFrame = layoutType.GetMethod("TryFrame")!;
Func<int, int, byte> opaque = (_, _) => 255;
int[] pendingSize = (int[])assetType.GetProperty("Value")!.GetValue(pendingAsset)!;
object pendingFrames = slice.Invoke(null, new object[] { pendingSize[0], pendingSize[1], 4, 2, opaque })!;
if ((bool)tryFrame.Invoke(null, new[] { pendingFrames, (object)0, null })!)
    throw new InvalidOperationException("The real pending asset's 1x1 dummy must not provide an animation pose.");
assetType.GetMethod("SubmitLoadedContent", BindingFlags.Instance | BindingFlags.NonPublic)!
    .Invoke(pendingAsset, new object?[] { new[] { 1254, 1254 }, null });
int[] readySize = (int[])assetType.GetProperty("Value")!.GetValue(pendingAsset)!;
object loadedFrames = slice.Invoke(null, new object[] { readySize[0], readySize[1], 4, 2, opaque })!;
for (int pose = 0; pose < 8; pose++)
    if (!(bool)tryFrame.Invoke(null, new[] { loadedFrames, (object)pose, null })!)
        throw new InvalidOperationException("Ready assets must provide all eight valid poses after replacing the dummy.");
Console.WriteLine("PASS real ReLogic pending-asset transition: reject 1x1 dummy, accept all eight loaded poses");

Type materialsType = mod.GetType("DimensionalTerraria.Content.StorageMaterials")!;
object[] materials = ((System.Collections.IEnumerable)materialsType.GetField("All")!.GetValue(null)!).Cast<object>().ToArray();
if (materials.Length != 27 || materials.Select(m => m.GetType().GetProperty("Ingredient")!.GetValue(m)).Distinct().Count() != 27)
    throw new InvalidOperationException("The catalog needs 20 distinct metal bars and seven distinct gems.");
var expectedStorageIngredients = new Dictionary<string, int>();
foreach (object material in materials)
{
    Type materialType = material.GetType();
    string key = (string)materialType.GetProperty("Key")!.GetValue(material)!;
    bool gem = (bool)materialType.GetProperty("Gem")!.GetValue(material)!;
    string nativeName = gem ? key : key == "Luminite" ? "LunarBar" : key + "Bar";
    int expected = Convert.ToInt32(game.GetType("Terraria.ID.ItemID")!.GetField(nativeName)!.GetValue(null));
    if ((int)materialType.GetProperty("Ingredient")!.GetValue(material)! != expected ||
        gem && (int)materialType.GetProperty("BarIngredient")!.GetValue(material)! != 0)
        throw new InvalidOperationException("Compression must prefer the correct bar when one exists and preserve gems: " + key);
    expectedStorageIngredients.Add(key, expected);
}
Console.WriteLine("PASS material catalog: 20 corresponding metal bars selected, seven gem ingredients retained");

object contentMod = Activator.CreateInstance(mod.GetType("DimensionalTerraria.DimensionalTerraria")!)!;
Type modBase = game.GetType("Terraria.ModLoader.Mod")!;
Type fileType = game.GetType("Terraria.ModLoader.Core.TmodFile")!;
object contentFile = Activator.CreateInstance(fileType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
    null, new object[] { Path.GetFullPath(args[1]), "DimensionalTerraria", new Version(0, 10, 0) }, null)!;
modBase.GetProperty("File", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(contentMod, contentFile);
modBase.GetProperty("Code")!.SetValue(contentMod, mod);
modBase.GetField("loading", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(contentMod, true);
materialsType.GetMethod("Register", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new[] { contentMod });
Type storageItemType = mod.GetType("DimensionalTerraria.Items.StorageBlockItem")!;
MethodInfo findItem = modBase.GetMethods().Single(m => m.Name == "Find" && m.IsGenericMethodDefinition).MakeGenericMethod(game.GetType("Terraria.ModLoader.ModItem")!);
foreach (object material in materials)
{
    string name = (string)material.GetType().GetProperty("Key")!.GetValue(material)!;
    object registered = findItem.Invoke(contentMod, new object[] { name + "StorageBlock" })!;
    storageItemType.GetMethod("SetDefaults")!.Invoke(registered, null);
    object registeredItem = storageItemType.GetProperty("Item")!.GetValue(registered)!;
    object freshEntity = Activator.CreateInstance(bladeItem.GetType())!;
    object freshBlock = storageItemType.GetMethod("NewInstance")!.Invoke(registered, new[] { freshEntity })!;
    storageItemType.GetMethod("SetDefaults")!.Invoke(freshBlock, null);
    int tile = (int)material.GetType().GetProperty("TileType")!.GetValue(material)!;
    if (tile <= 0 || (int)registeredItem.GetType().GetField("createTile")!.GetValue(registeredItem)! != tile ||
        (int)registeredItem.GetType().GetField("maxStack")!.GetValue(registeredItem)! != 9999 ||
        (int)freshEntity.GetType().GetField("createTile")!.GetValue(freshEntity)! != tile)
        throw new InvalidOperationException("Storage item has no matching registered tile: " + name);
}
Console.WriteLine("PASS real dynamic registration: 27 block items map to 27 placeable tile types");

Type artType = mod.GetType("DimensionalTerraria.Core.ContentPixelArt")!;
byte[] facePixels = (byte[])artType.GetMethod("BlockFace")!.Invoke(null, new object[] { 0x6DE6E9, true })!;
byte[] rawImage = (byte[])artType.GetMethod("RawImage")!.Invoke(null, new object[] { 16, 16, facePixels })!;
using (var rawStream = new MemoryStream(rawImage))
{
    object?[] rawArguments = { rawStream, 0, 0 };
    byte[] decoded = (byte[])game.GetType("Terraria.ModLoader.IO.ImageIO")!.GetMethod("ReadRaw")!.Invoke(null, rawArguments)!;
    if (!decoded.SequenceEqual(facePixels) || (int)rawArguments[1]! != 16 || (int)rawArguments[2]! != 16)
        throw new InvalidOperationException("Generated textures must decode through the actual engine reader.");
}
Console.WriteLine("PASS actual RawImg reader: logical 16px material texture round-trip");

Type flightConfig = mod.GetType("DimensionalTerraria.Configuration.DimensionConfig")!;
if(mod.GetType("DimensionalTerraria.Configuration.DebugConfig") is not null || mod.GetType("DimensionalTerraria.Systems.DebugTimeSystem") is not null || flightConfig.GetProperty("ShowReadyHint") is not null)
    throw new InvalidOperationException("Release must remove debug configuration, clock override and ready hints");
foreach (string action in new[] { "GiveDebugWeapons", "SpawnDebugDummy", "SpawnSheep", "SpawnIronGolem", "SpawnEyeOfCthulhu", "SpawnKingSlimeTrial", "SpawnVillager", "PermanentDay", "SpawnRecordingWave" })
    if (flightConfig.GetProperty(action) is not null)
        throw new InvalidOperationException("Release settings must not contain debug actions.");
if (mod.GetType("DimensionalTerraria.Configuration.ItemCatalogConfig")!.GetProperty("Items") is null)
    throw new InvalidOperationException("Item catalog must have its own configuration page.");
if (mod.GetType("DimensionalTerraria.Configuration.VanillaItemCatalogConfig")!.GetProperty("Items") is null ||
    mod.GetType("DimensionalTerraria.Configuration.VanillaItemCatalogElement")!.BaseType != mod.GetType("DimensionalTerraria.Configuration.ItemCatalogElement"))
    throw new InvalidOperationException("Vanilla items need their own page using the shared searchable catalog UI.");
Console.WriteLine("PASS release config pages: flight and two item catalogs; debug actions removed");

Type catalogItems = mod.GetType("DimensionalTerraria.Systems.CatalogItems")!;
MethodInfo isVanilla = catalogItems.GetMethod("IsVanilla", BindingFlags.Static | BindingFlags.NonPublic)!;
Type itemId = game.GetType("Terraria.ID.ItemID")!;
int vanillaCount = Convert.ToInt32(itemId.GetField("Count")!.GetValue(null));
bool[] deprecated = (bool[])game.GetType("Terraria.ID.ItemID+Sets")!.GetField("Deprecated")!.GetValue(null)!;
int validCount = 0;
for (int id = -1; id <= vanillaCount; id++)
{
    bool expected = id > 0 && id < vanillaCount && !deprecated[id];
    if ((bool)isVanilla.Invoke(null, new object[] { id })! != expected)
        throw new InvalidOperationException("Vanilla catalog bounds/deprecated filtering mismatch: " + id);
    if (expected) validCount++;
}
Console.WriteLine($"PASS all {validCount} valid vanilla IDs included; air, deprecated and mod IDs excluded");
MethodInfo canGrant = catalogItems.GetMethod("CanGrant", BindingFlags.Static | BindingFlags.NonPublic)!;
object firstMaterial = materials[0];
string firstName = (string)firstMaterial.GetType().GetProperty("Key")!.GetValue(firstMaterial)!;
object firstModItem = findItem.Invoke(contentMod, new object[] { firstName + "StorageBlock" })!;
int firstModId = (int)storageItemType.GetProperty("Type")!.GetValue(firstModItem)!;
foreach ((int id, bool vanilla, bool expected) in new[] { (2, true, true), (2, false, false), (firstModId, true, false),
    (firstModId, false, true), (0, true, false), (-1, false, false), (int.MaxValue, true, false), (int.MaxValue, false, false) })
    if ((bool)canGrant.Invoke(null, new object[] { contentMod, id, vanilla })! != expected)
        throw new InvalidOperationException("Catalog grants must validate the correct item source.");
Console.WriteLine("PASS catalog grants: valid native/mod sources accepted, wrong sources and invalid IDs rejected");

Type swordEffects = mod.GetType("DimensionalTerraria.World.NativeSwordEffects")!;
MethodInfo swordEffect = swordEffects.GetMethod("Effect", BindingFlags.Static | BindingFlags.NonPublic)!;
MethodInfo attackMode = mod.GetType("DimensionalTerraria.World.NativeCombat")!.GetMethod("Mode")!;
MethodInfo swordReach = swordEffects.GetMethod("Reach", BindingFlags.Static | BindingFlags.NonPublic)!;
MethodInfo itemDefaults = bladeItem.GetType().GetMethod("SetDefaults", new[] { typeof(int) })!;
foreach ((string key, string expected, int projectile) in new[] { ("Zenith", "Zenith", 933), ("DD2SquireBetsySword", "FlyingDragon", 684) })
{
    int id = Convert.ToInt32(itemId.GetField(key)!.GetValue(null));
    object weapon = Activator.CreateInstance(bladeItem.GetType())!;
    itemDefaults.Invoke(weapon, new object[] { id });
    object mode = attackMode.Invoke(null, new[] { weapon })!;
    if (swordEffect.Invoke(null, new[] { weapon })!.ToString() != expected || mode.ToString() != "Melee" ||
        (float)swordReach.Invoke(null, new[] { weapon, mode })! != (key == "Zenith" ? 96 : 48) ||
        (int)weapon.GetType().GetField("shoot")!.GetValue(weapon)! != projectile)
        throw new InvalidOperationException("Real native sword defaults must select the matching long-range 3D effect: " + key);
}
Console.WriteLine("PASS real Zenith and Flying Dragon defaults: correct native projectile identities, melee damage and 96/48-tile 3D reach");
object ordinarySword = Activator.CreateInstance(bladeItem.GetType())!;
itemDefaults.Invoke(ordinarySword, new object[] { Convert.ToInt32(itemId.GetField("WoodenSword")!.GetValue(null)) });
object ordinaryMode = attackMode.Invoke(null, new[] { ordinarySword })!;
if (swordEffect.Invoke(null, new[] { ordinarySword })!.ToString() != "None" ||
    (float)swordReach.Invoke(null, new[] { ordinarySword, ordinaryMode })! != 4.5f)
    throw new InvalidOperationException("Ordinary swords must retain their existing short reach and attack path.");
Console.WriteLine("PASS ordinary melee weapons keep 4.5-tile reach and the existing attack path");
MethodInfo depthWeight = swordEffects.GetMethod("DepthWeight", BindingFlags.Static | BindingFlags.NonPublic)!;
foreach (string key in new[] { "WoodenSword", "CopperShortsword", "Spear", "DD2SquireBetsySword", "Zenith", "Musket", "WaterBolt" })
{
    object weapon = Activator.CreateInstance(bladeItem.GetType())!;
    itemDefaults.Invoke(weapon, new object[] { Convert.ToInt32(itemId.GetField(key)!.GetValue(null)) });
    object mode = attackMode.Invoke(null, new[] { weapon })!;
    bool directMelee = key is "WoodenSword" or "CopperShortsword" or "Spear";
    if ((float)depthWeight.Invoke(null, new[] { weapon, mode })! != (directMelee ? .5f : 1))
        throw new InvalidOperationException("Depth weighting must cover direct melee families while leaving projectile flight distances physical: " + key);
}
Console.WriteLine("PASS native melee depth metric: sword/shortsword/spear use 0.5 depth weight; special sword flights, guns and magic retain physical trajectories");
var fractal = game.GetType("Terraria.Graphics.FinalFractalHelper")!;
object profile = fractal.GetMethod("GetFinalFractalProfile")!.Invoke(null, new object[] { Convert.ToInt32(itemId.GetField("Zenith")!.GetValue(null)) })!;
if ((float)profile.GetType().GetField("trailWidth")!.GetValue(profile)! <= 0)
    throw new InvalidOperationException("Zenith rendering needs a valid native sword profile.");
Console.WriteLine("PASS native Zenith profile supplies the sword trail style without invoking screen-space shader code");

object egg = Activator.CreateInstance(mod.GetType("DimensionalTerraria.Items.SheepSpawnEgg")!)!;
object eggItem = Activator.CreateInstance(bladeItem.GetType())!;
egg.GetType().GetProperty("Entity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(egg, eggItem);
egg.GetType().GetMethod("SetDefaults")!.Invoke(egg, null);
if (!(bool)eggItem.GetType().GetField("consumable")!.GetValue(eggItem)! ||
    (bool)egg.GetType().GetMethod("ConsumeItem")!.Invoke(egg, new[] { attacker })!)
    throw new InvalidOperationException("Eggs must be consumable but cannot be spent before a successful spawn.");
Console.WriteLine("PASS sheep egg defaults: consumable item, failed/unattempted spawn does not consume");

Type mainType = game.GetType("Terraria.Main")!;
mainType.GetField("dedServ")!.SetValue(null, true); // No sound engine is created for this inventory check.
mainType.GetField("myPlayer")!.SetValue(null, 0);
((Array)mainType.GetField("player")!.GetValue(null)!).SetValue(attacker, 0);
Type recipeType = game.GetType("Terraria.Recipe")!;
((Array)mainType.GetField("recipe")!.GetValue(null)!).SetValue(Activator.CreateInstance(recipeType,
    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new object?[] { null }, null), 0);
Array inventory = (Array)playerType.GetField("inventory")!.GetValue(attacker)!;
object dirt = Activator.CreateInstance(bladeItem.GetType())!;
dirt.GetType().GetField("type")!.SetValue(dirt, 2);
dirt.GetType().GetField("stack")!.SetValue(dirt, 10);
dirt.GetType().GetField("maxStack")!.SetValue(dirt, 9999);
inventory.SetValue(dirt, 0);
Type backpackType = mod.GetType("DimensionalTerraria.World.NativeBackpack")!;
MethodInfo clickSlot = backpackType.GetMethod("Click", BindingFlags.NonPublic | BindingFlags.Static)!;
FieldInfo cursorItem = mainType.GetField("mouseItem")!;
cursorItem.SetValue(null, Activator.CreateInstance(bladeItem.GetType()));
clickSlot.Invoke(null, new object[] { 0, true, false });
object cursor = cursorItem.GetValue(null)!;
if ((int)cursor.GetType().GetField("stack")!.GetValue(cursor)! != 10)
    throw new InvalidOperationException("Backpack left click must pick up the real inventory stack.");
clickSlot.Invoke(null, new object[] { 1, true, false });
clickSlot.Invoke(null, new object[] { 1, false, true });
cursor = cursorItem.GetValue(null)!;
object remainingDirt = inventory.GetValue(1)!;
if ((int)cursor.GetType().GetField("stack")!.GetValue(cursor)! != 1 ||
    (int)remainingDirt.GetType().GetField("stack")!.GetValue(remainingDirt)! != 9)
    throw new InvalidOperationException("Backpack right click must split one item without duplicating the stack.");
Console.WriteLine("PASS real backpack slots: pick up, move and split inventory stacks through native ItemSlot");

// Catalog grants and cursor returns both use native GetItem rather than spawning
// overflow on the ground. Exercise its real partial/full-inventory behavior.
Type itemType = bladeItem.GetType();
object DirtStack(int stack)
{
    object item = Activator.CreateInstance(itemType)!;
    itemType.GetField("type")!.SetValue(item, 2);
    itemType.GetField("stack")!.SetValue(item, stack);
    itemType.GetField("maxStack")!.SetValue(item, 9999);
    return item;
}
for (int slot = 0; slot < 50; slot++) inventory.SetValue(DirtStack(9999), slot);
cursorItem.SetValue(null, DirtStack(64));
MethodInfo returnCursor = backpackType.GetMethod("ReturnCursorItem", BindingFlags.NonPublic | BindingFlags.Static)!;
if ((bool)returnCursor.Invoke(null, null)! || (int)itemType.GetField("stack")!.GetValue(cursorItem.GetValue(null))! != 64)
    throw new InvalidOperationException("A full inventory must retain all overflow rather than discard or spawn it.");
inventory.SetValue(DirtStack(9989), 10);
if ((bool)returnCursor.Invoke(null, null)! || (int)itemType.GetField("stack")!.GetValue(cursorItem.GetValue(null))! != 54 ||
    (int)itemType.GetField("stack")!.GetValue(inventory.GetValue(10))! != 9999)
    throw new InvalidOperationException("Native grants must merge only the available capacity and return the remainder.");
inventory.SetValue(Activator.CreateInstance(itemType), 11);
if (!(bool)returnCursor.Invoke(null, null)! || (int)itemType.GetField("stack")!.GetValue(inventory.GetValue(11))! != 54)
    throw new InvalidOperationException("A free slot must accept exactly the retained remainder.");
Console.WriteLine("PASS native inventory capacity: full stack retained, partial capacity merged, remainder moved without loss");

// Allocate an isolated native tilemap, then exercise the real structure adapter.
Type tilemapType = game.GetType("Terraria.Tilemap")!;
object tilemap = Activator.CreateInstance(tilemapType, BindingFlags.Instance | BindingFlags.NonPublic,
    null, new object[] { (ushort)100, (ushort)100 }, null)!;
mainType.GetField("tile")!.SetValue(null, tilemap);
mainType.GetField("maxTilesX")!.SetValue(null, 100);
mainType.GetField("maxTilesY")!.SetValue(null, 100);
object testTile = tilemapType.GetProperty("Item", new[] { typeof(int), typeof(int) })!.GetValue(tilemap, new object[] { 30, 50 })!;
Type tileType = testTile.GetType();
void ResetTile(int id) => tileType.GetMethod("ResetToType")!.Invoke(testTile, new object[] { (ushort)id });
object ironMaterial = materials.Single(m => (string)m.GetType().GetProperty("Key")!.GetValue(m)! == "Iron");
int ironTile = (int)ironMaterial.GetType().GetProperty("TileType")!.GetValue(ironMaterial)!;
Type modTileBase = game.GetType("Terraria.ModLoader.ModTile")!;
object registeredIron = modBase.GetMethods().Single(m => m.Name == "Find" && m.IsGenericMethodDefinition)
    .MakeGenericMethod(modTileBase).Invoke(contentMod, new object[] { "IronStorageTile" })!;
if (Convert.ToInt32(modTileBase.GetProperty("Type")!.GetValue(registeredIron)) != ironTile)
    throw new InvalidOperationException("Structure summoning must use the real dynamic IronStorageTile registry entry.");
Type structureWorldType = mod.GetType("DimensionalTerraria.World.TerrariaGolemStructureWorld")!;
object structureWorld = Activator.CreateInstance(structureWorldType, new object[] { ironTile, 30, 50 })!;
string StructureKind() => structureWorldType.GetMethod("StructureAt")!.Invoke(structureWorld, new object[] { 30, 50 })!.ToString()!;
ResetTile(ironTile);
if (StructureKind() != "IronBlock") throw new InvalidOperationException("Full mod iron blocks must match the structure.");
foreach (string state in new[] { "IsHalfBlock", "IsActuated" })
{
    tileType.GetProperty(state)!.SetValue(testTile, true);
    if (StructureKind() != "Other") throw new InvalidOperationException("Half/actuated iron cannot match: " + state);
    ResetTile(ironTile);
}
var slopeProperty = tileType.GetProperty("Slope")!;
slopeProperty.SetValue(testTile, Enum.ToObject(slopeProperty.PropertyType, 1));
if (StructureKind() != "Other") throw new InvalidOperationException("Sloped iron cannot match the T structure.");
int pumpkinBlock = Convert.ToInt32(game.GetType("Terraria.ID.TileID")!.GetField("PumpkinBlock")!.GetValue(null));
int pumpkinPlant = Convert.ToInt32(game.GetType("Terraria.ID.TileID")!.GetField("Pumpkins")!.GetValue(null));
ResetTile(pumpkinBlock);
if (StructureKind() != "Pumpkin") throw new InvalidOperationException("A placed Pumpkin item must match the head.");
object nativePumpkin = Activator.CreateInstance(itemType)!;
itemDefaults.Invoke(nativePumpkin, new object[] { Convert.ToInt32(itemId.GetField("Pumpkin")!.GetValue(null)) });
if ((int)itemType.GetField("createTile")!.GetValue(nativePumpkin)! != pumpkinBlock)
    throw new InvalidOperationException("Native Pumpkin item placement must produce the tile watched by our hook.");
ResetTile(pumpkinPlant);
if (StructureKind() != "Other") throw new InvalidOperationException("A grown pumpkin plant must not be mistaken for a placed head.");
ResetTile(Convert.ToInt32(game.GetType("Terraria.ID.TileID")!.GetField("Iron")!.GetValue(null)));
if (StructureKind() != "Other") throw new InvalidOperationException("Iron ore cannot substitute for the mod iron block.");
Console.WriteLine("PASS native structure materials: registered iron, full/half/sloped/actuated states, placed pumpkin vs crop and iron ore");

ResetTile(ironTile);
tileType.GetProperty("RedWire")!.SetValue(testTile, true);
Type wallDataType = game.GetType("Terraria.WallTypeData")!;
Array wallData = (Array)tilemapType.GetMethod("GetData")!.MakeGenericMethod(wallDataType).Invoke(tilemap, null)!;
int tileIndex = 30 * 100 + 50;
object wallEntry = wallData.GetValue(tileIndex)!;
wallDataType.GetField("Type")!.SetValue(wallEntry, (ushort)1);
wallData.SetValue(wallEntry, tileIndex);
tileType.GetMethod("ClearTile")!.Invoke(testTile, null);
if ((bool)tileType.GetProperty("HasTile")!.GetValue(testTile)! || !(bool)tileType.GetProperty("RedWire")!.GetValue(testTile)! ||
    (ushort)wallDataType.GetField("Type")!.GetValue(wallData.GetValue(tileIndex))! != 1)
    throw new InvalidOperationException("The conversion's native foreground clear must preserve background walls and wiring.");
Console.WriteLine("PASS native foreground conversion: clears the solid block, preserves the wall and wire");

// Placement callbacks enqueue only; clearing the world discards stale requests.
Type summoningType = mod.GetType("DimensionalTerraria.Systems.GolemSummoningSystem")!;
object summoning = Activator.CreateInstance(summoningType)!;
var queuePumpkin = summoningType.GetMethod("PumpkinPlaced", BindingFlags.NonPublic | BindingFlags.Instance)!;
queuePumpkin.Invoke(summoning, new object[] { 30, 50 });
queuePumpkin.Invoke(summoning, new object[] { 30, 50 });
object pendingSummons = summoningType.GetField("pending", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(summoning)!;
int PendingSummons() => (int)pendingSummons.GetType().GetProperty("Count")!.GetValue(pendingSummons)!;
if (PendingSummons() != 1) throw new InvalidOperationException("Duplicate callbacks for one pumpkin must only enqueue one request.");
summoningType.GetMethod("OnWorldUnload")!.Invoke(summoning, null);
if (PendingSummons() != 0) throw new InvalidOperationException("A world reload must not replay a pending summon.");
Console.WriteLine("PASS summon request lifecycle: placement deferred, duplicates coalesced, world unload clears pending heads");

// Register the new content and execute its real recipes through native tML APIs.
MethodInfo addContent = modBase.GetMethods().Single(m => m.Name == "AddContent" && !m.IsGenericMethodDefinition && m.GetParameters().Length == 1);
object RegisterContent(string name)
{
    object value = Activator.CreateInstance(mod.GetType(name)!)!;
    addContent.Invoke(contentMod, new[] { value });
    return value;
}
object oakTileContent = RegisterContent("DimensionalTerraria.Tiles.OakPlanksTile");
object minecraftChestTile = RegisterContent("DimensionalTerraria.Tiles.MinecraftChestTile");
object minecraftChestItem = RegisterContent("DimensionalTerraria.Items.MinecraftChest");
object villagerEggContent = RegisterContent("DimensionalTerraria.Items.VillagerSpawnEgg");
object fireworkContent = RegisterContent("DimensionalTerraria.Items.Firework");
object netheriteContent = RegisterContent("DimensionalTerraria.Items.NetheriteIngot");
object sheepEggContent = RegisterContent("DimensionalTerraria.Items.SheepSpawnEgg");
object debugBlasterContent = RegisterContent("DimensionalTerraria.Items.DebugBlaster");
object enemyStateContent = RegisterContent("DimensionalTerraria.NPCs.DimensionEnemyState");
// Match the server's no-reader asset repository: equipment slots are real,
// while texture requests remain unloaded and never create a graphics device.
((System.Collections.IDictionary)game.GetType("Terraria.ModLoader.ModLoader")!
    .GetField("modsByName", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!)["DimensionalTerraria"] = contentMod;
Type assetRepository = Assembly.Load("ReLogic").GetType("ReLogic.Content.AssetRepository")!;
modBase.GetProperty("Assets", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!
    .SetValue(contentMod, Activator.CreateInstance(assetRepository, new object?[] { null, null }));
object bedrockContent = RegisterContent("DimensionalTerraria.Tiles.BedrockTile");
object bedrockItem = RegisterContent("DimensionalTerraria.Items.Bedrock");
object projectionPick = RegisterContent("DimensionalTerraria.Items.HigherDimensionalPickaxe");
object projectionHelmet = RegisterContent("DimensionalTerraria.Items.HigherDimensionalHelmet");
object projectionChest = RegisterContent("DimensionalTerraria.Items.HigherDimensionalChestplate");
object projectionLegs = RegisterContent("DimensionalTerraria.Items.HigherDimensionalLeggings");
object tableTileContent = RegisterContent("DimensionalTerraria.Tiles.MinecraftCraftingTableTile");
object boatTileContent = RegisterContent("DimensionalTerraria.Tiles.OakBoatTile");
object redstoneTileContent = RegisterContent("DimensionalTerraria.Tiles.RedstoneBlockTile");
object pulserTileContent = RegisterContent("DimensionalTerraria.Tiles.RedstonePulserTile");
object hopperTileContent = RegisterContent("DimensionalTerraria.Tiles.HopperTile");
object pistonTileContent = RegisterContent("DimensionalTerraria.Tiles.PistonTile");
object stickyTileContent = RegisterContent("DimensionalTerraria.Tiles.StickyPistonTile");
object pistonHeadContent = RegisterContent("DimensionalTerraria.Tiles.PistonHeadTile");
game.GetType("Terraria.DataStructures.TileEntity")!.GetMethod("InitializeAll")!.Invoke(null, null);
object boatEntityContent = RegisterContent("DimensionalTerraria.Systems.OakBoatEntity");
object pistonEntityContent = RegisterContent("DimensionalTerraria.Systems.PistonEntity");
object hopperEntityContent = RegisterContent("DimensionalTerraria.Systems.HopperEntity");
object boatItemContent = RegisterContent("DimensionalTerraria.Items.OakBoat");
object captiveContent = RegisterContent("DimensionalTerraria.Systems.BoatCaptive");
object oakItemContent = RegisterContent("DimensionalTerraria.Items.OakPlanks");
object tableItemContent = RegisterContent("DimensionalTerraria.Items.MinecraftCraftingTable");
object prefabContent = RegisterContent("DimensionalTerraria.Items.PrefabIronGolem");
object swordContent = RegisterContent("DimensionalTerraria.Items.HigherDimensionalProjection");
object redstoneItemContent = RegisterContent("DimensionalTerraria.Items.Redstone");
object redstoneBlockContent = RegisterContent("DimensionalTerraria.Items.RedstoneBlock");
object pistonItemContent = RegisterContent("DimensionalTerraria.Items.Piston");
object stickyItemContent = RegisterContent("DimensionalTerraria.Items.StickyPiston");
object pulserItemContent = RegisterContent("DimensionalTerraria.Items.RedstonePulser");
object hopperItemContent = RegisterContent("DimensionalTerraria.Items.Hopper");
object slimeEggContent = RegisterContent("DimensionalTerraria.Items.SlimeSpawnEgg");
object recipeSystem = RegisterContent("DimensionalTerraria.Systems.MinecraftRecipes");
object villagerContent = RegisterContent("DimensionalTerraria.NPCs.MinecraftVillager");
object golemContent = RegisterContent("DimensionalTerraria.NPCs.MinecraftIronGolem");
object sheepContent = RegisterContent("DimensionalTerraria.NPCs.MinecraftSheep");
int ContentId(object content) => Convert.ToInt32(content.GetType().GetProperty("Type")!.GetValue(content));
int oakId = ContentId(oakItemContent), tableId = ContentId(tableItemContent), prefabId = ContentId(prefabContent), tableTileId = ContentId(tableTileContent);
recipeSystem.GetType().GetMethod("AddRecipeGroups")!.Invoke(recipeSystem, null);
Type recipeGroupType = game.GetType("Terraria.RecipeGroup")!;
var groupIds = (System.Collections.IDictionary)recipeGroupType.GetField("recipeGroupIDs")!.GetValue(null)!;
int woodGroupId = (int)groupIds["DimensionalTerraria:AnyTerrariaWood"]!;
object woodGroup = ((System.Collections.IDictionary)recipeGroupType.GetField("recipeGroups")!.GetValue(null)!)[woodGroupId]!;
int[] woodIds = ((System.Collections.IEnumerable)recipeGroupType.GetField("ValidItems")!.GetValue(woodGroup)!).Cast<int>().ToArray();
foreach (string wood in new[] { "Wood", "Ebonwood", "RichMahogany", "Pearlwood", "Shadewood", "SpookyWood", "BorealWood", "PalmWood", "DynastyWood", "AshWood" })
    if (!woodIds.Contains(Convert.ToInt32(itemId.GetField(wood)!.GetValue(null)))) throw new InvalidOperationException("Missing Terraria wood: " + wood);
if (woodIds.Length != 10 || (int)recipeGroupType.GetMethod("CountUsableItems")!.Invoke(woodGroup,
    new object[] { new Dictionary<int, int> { [woodIds[0]] = 1, [woodIds[1]] = 3 } })! != 4)
    throw new InvalidOperationException("All ten woods must support mixed stacks without accepting furniture.");
Console.WriteLine("PASS native wood recipe group: all ten Terraria woods, including Dynasty/Ash/Spooky, support mixed stacks");

Type recipeLoader = game.GetType("Terraria.ModLoader.RecipeLoader")!;
recipeLoader.GetField("setupRecipes", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, true);
recipeLoader.GetProperty("CurrentMod", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, contentMod);
int registeredItemCount = (int)game.GetType("Terraria.ModLoader.ItemLoader")!.GetProperty("ItemCount")!.GetValue(null)!;
game.GetType("Terraria.ID.SetFactory")!.GetMethod("ResizeArrays")!.Invoke(null, new object[] { false });
game.GetType("Terraria.ModLoader.ItemLoader")!.GetMethod("ResizeArrays", BindingFlags.NonPublic | BindingFlags.Static)!
    .Invoke(null, new object[] { false });
recipeLoader.GetField("FirstRecipeForItem", BindingFlags.NonPublic | BindingFlags.Static)!
    .SetValue(null, Array.CreateInstance(recipeType, registeredItemCount));
recipeType.GetField("numRecipes")!.SetValue(null, 0);
foreach (object material in materials)
{
    string key = (string)material.GetType().GetProperty("Key")!.GetValue(material)!;
    object block = findItem.Invoke(contentMod, new object[] { key + "StorageBlock" })!;
    storageItemType.GetMethod("AddRecipes")!.Invoke(block, null);
}
foreach (object content in new[] { oakItemContent, tableItemContent, prefabContent, swordContent, boatItemContent, redstoneBlockContent, pistonItemContent, stickyItemContent, pulserItemContent, hopperItemContent, minecraftChestItem, fireworkContent, netheriteContent, sheepEggContent, slimeEggContent, villagerEggContent, bedrockItem, redstoneItemContent, projectionPick, projectionHelmet, projectionChest, projectionLegs, debugBlasterContent })
    content.GetType().GetMethod("AddRecipes")!.Invoke(content, null);
recipeSystem.GetType().GetMethod("PostAddRecipes")!.Invoke(recipeSystem, null);
int recipeCount = (int)recipeType.GetField("numRecipes")!.GetValue(null)!;
Array realRecipes = (Array)mainType.GetField("recipe")!.GetValue(null)!;
object RecipeOutput(object recipe) => recipeType.GetField("createItem")!.GetValue(recipe)!;
int OutputId(object recipe) => (int)itemType.GetField("type")!.GetValue(RecipeOutput(recipe))!;
int OutputStack(object recipe) => (int)itemType.GetField("stack")!.GetValue(RecipeOutput(recipe))!;
int[] RequiredTiles(object recipe) => ((System.Collections.IEnumerable)recipeType.GetField("requiredTile")!.GetValue(recipe)!).Cast<int>().ToArray();
object[] RecipeIngredients(object recipe) => ((System.Collections.IEnumerable)recipeType.GetField("requiredItem")!.GetValue(recipe)!).Cast<object>().ToArray();
object[] addedRecipes = Enumerable.Range(0, recipeCount).Select(i => realRecipes.GetValue(i)!).ToArray();
if (recipeCount != 78 || addedRecipes.Count(r => RequiredTiles(r).SequenceEqual(new[] { tableTileId })) != 76)
    throw new InvalidOperationException("All 76 restricted recipes must require the Minecraft table.");
object oakRecipe = addedRecipes.Single(r => OutputId(r) == oakId), tableRecipe = addedRecipes.Single(r => OutputId(r) == tableId);
object fireworkRecipe=addedRecipes.Single(r=>OutputId(r)==ContentId(fireworkContent));
var rocketInputs=RecipeIngredients(fireworkRecipe).ToDictionary(i=>(int)itemType.GetField("type")!.GetValue(i)!,i=>(int)itemType.GetField("stack")!.GetValue(i)!);
if(OutputStack(fireworkRecipe)!=3 || rocketInputs.Count!=2 || rocketInputs.GetValueOrDefault(Convert.ToInt32(itemId.GetField("Wood")!.GetValue(null)))!=1 ||
    rocketInputs.GetValueOrDefault(Convert.ToInt32(itemId.GetField("Gel")!.GetValue(null)))!=1 || !RequiredTiles(fireworkRecipe).SequenceEqual(new[]{tableTileId}))
    throw new InvalidOperationException("Fireworks must craft three from one Wood and one Gel at the MC table");
Console.WriteLine("PASS native firework recipe: three rockets from one Wood and one Gel, MC table required");
int VanillaId(string key)=>Convert.ToInt32(itemId.GetField(key)!.GetValue(null));
foreach(var expected in new[]{
    (Output:ContentId(netheriteContent),Count:1,Inputs:new Dictionary<int,int>{[VanillaId("HellstoneBar")]=4,[VanillaId("ChlorophyteBar")]=4,[VanillaId("LunarBar")]=1}),
    (Output:ContentId(swordContent),Count:1,Inputs:new Dictionary<int,int>{[ContentId(netheriteContent)]=2,[oakId]=1}),
    (Output:ContentId(projectionPick),Count:1,Inputs:new Dictionary<int,int>{[ContentId(netheriteContent)]=3,[oakId]=2}),
    (Output:ContentId(projectionHelmet),Count:1,Inputs:new Dictionary<int,int>{[ContentId(netheriteContent)]=5}),
    (Output:ContentId(projectionChest),Count:1,Inputs:new Dictionary<int,int>{[ContentId(netheriteContent)]=8}),
    (Output:ContentId(projectionLegs),Count:1,Inputs:new Dictionary<int,int>{[ContentId(netheriteContent)]=7}),
    (Output:ContentId(bedrockItem),Count:4,Inputs:new Dictionary<int,int>{[VanillaId("StoneBlock")]=10,[VanillaId("Obsidian")]=1,[ContentId(netheriteContent)]=1}),
    (Output:ContentId(redstoneItemContent),Count:4,Inputs:new Dictionary<int,int>{[VanillaId("Ruby")]=1,[VanillaId("StoneBlock")]=4}),
    (Output:ContentId(sheepEggContent),Count:1,Inputs:new Dictionary<int,int>{[VanillaId("Silk")]=5,[VanillaId("Hay")]=10,[VanillaId("FallenStar")]=1}),
    (Output:ContentId(slimeEggContent),Count:1,Inputs:new Dictionary<int,int>{[VanillaId("Gel")]=10,[VanillaId("FallenStar")]=1}),
    (Output:ContentId(villagerEggContent),Count:1,Inputs:new Dictionary<int,int>{[VanillaId("Silk")]=5,[VanillaId("Wood")]=10,[VanillaId("FallenStar")]=1}),
})
{
    object recipe=addedRecipes.Single(r=>OutputId(r)==expected.Output && OutputStack(r)==expected.Count);
    var inputs=RecipeIngredients(recipe).ToDictionary(i=>(int)itemType.GetField("type")!.GetValue(i)!,i=>(int)itemType.GetField("stack")!.GetValue(i)!);
    if(inputs.Count!=expected.Inputs.Count || expected.Inputs.Any(p=>inputs.GetValueOrDefault(p.Key)!=p.Value) || !RequiredTiles(recipe).SequenceEqual(new[]{tableTileId}))
        throw new InvalidOperationException("Release recipe ingredient/station mismatch for "+expected.Output);
}
Console.WriteLine("PASS native release progression: netherite, all five projection pieces, bedrock, redstone and three eggs have exact native ingredients and MC table requirements");
MethodInfo enumerateContent=modBase.GetMethods().Single(m=>m.Name=="GetContent" && m.IsGenericMethodDefinition && m.GetParameters().Length==0);
object[] allItems=((System.Collections.IEnumerable)enumerateContent.MakeGenericMethod(game.GetType("Terraria.ModLoader.ModItem")!).Invoke(contentMod,null)!).Cast<object>().ToArray();
int blasterId=ContentId(debugBlasterContent);
if(allItems.Length!=50 || allItems.Any(i=>ContentId(i)!=blasterId && !addedRecipes.Any(r=>OutputId(r)==ContentId(i))) || addedRecipes.Any(r=>OutputId(r)==blasterId) ||
    !(bool)canGrant.Invoke(null,new[]{contentMod,(object)blasterId,false})! || !(bool)canGrant.Invoke(null,new[]{contentMod,(object)ContentId(netheriteContent),false})!)
    throw new InvalidOperationException($"Release audit count={allItems.Length}; missing={string.Join(",",allItems.Where(i=>ContentId(i)!=blasterId && !addedRecipes.Any(r=>OutputId(r)==ContentId(i))).Select(i=>i.GetType().Name))}; blasterGrant={canGrant.Invoke(null,new[]{contentMod,(object)blasterId,false})}; ingotGrant={canGrant.Invoke(null,new[]{contentMod,(object)ContentId(netheriteContent),false})}");
Console.WriteLine("PASS complete native item audit: 49 craftable gameplay items, catalog keeps netherite and the sole uncraftable debug blaster");
if (RequiredTiles(oakRecipe).Length != 0 || RequiredTiles(tableRecipe).Length != 0 || OutputStack(oakRecipe) != 4 || OutputStack(tableRecipe) != 1)
    throw new InvalidOperationException("Wood 1:4 and table 4:1 recipes must remain available by hand.");
foreach ((object recipe, int amount) in new[] { (oakRecipe, 1), (tableRecipe, 4) })
{
    object[] ingredients = RecipeIngredients(recipe);
    if (ingredients.Length != 1 || (int)itemType.GetField("stack")!.GetValue(ingredients[0])! != amount ||
        !((System.Collections.IEnumerable)recipeType.GetField("acceptedGroups")!.GetValue(recipe)!).Cast<int>().Contains(woodGroupId))
        throw new InvalidOperationException("Bootstrap wood quantities must be exact and accept the common wood group.");
}
object prefabRecipe = addedRecipes.Single(r => OutputId(r) == prefabId);
var prefabIngredients = RecipeIngredients(prefabRecipe).ToDictionary(i => (int)itemType.GetField("type")!.GetValue(i)!, i => (int)itemType.GetField("stack")!.GetValue(i)!);
int registeredIronItemId = (int)ironMaterial.GetType().GetProperty("ItemType")!.GetValue(ironMaterial)!;
int pumpkinId = Convert.ToInt32(itemId.GetField("Pumpkin")!.GetValue(null));
if (prefabIngredients.Count != 2 || prefabIngredients.GetValueOrDefault(registeredIronItemId) != 4 || prefabIngredients.GetValueOrDefault(pumpkinId) != 1)
    throw new InvalidOperationException("Prefab golem must cost exactly four mod iron blocks plus one native pumpkin.");
object boatRecipe = addedRecipes.Single(r => OutputId(r) == ContentId(boatItemContent));
object[] boatIngredients = RecipeIngredients(boatRecipe);
if (OutputStack(boatRecipe) != 1 || boatIngredients.Length != 1 ||
    (int)itemType.GetField("type")!.GetValue(boatIngredients[0])! != oakId || (int)itemType.GetField("stack")!.GetValue(boatIngredients[0])! != 5)
    throw new InvalidOperationException("A boat must cost exactly five Oak Planks at the Minecraft table.");
Console.WriteLine("PASS real recipe registration: 66 recipes, 64 new-table gates, handcraft bootstrap quantities, exact prefab/boat ingredients");
object chestRecipe = addedRecipes.Single(r => OutputId(r) == ContentId(minecraftChestItem));
if (RecipeIngredients(chestRecipe).Length != 1 || (int)itemType.GetField("type")!.GetValue(RecipeIngredients(chestRecipe)[0])! != oakId ||
    (int)itemType.GetField("stack")!.GetValue(RecipeIngredients(chestRecipe)[0])! != 8)
    throw new InvalidOperationException("Minecraft chest must cost exactly eight Oak Planks at the new workbench");
foreach (var expected in new[] {
    (Output: ContentId(redstoneBlockContent), Count: 1, Inputs: new Dictionary<int, int> { [ContentId(redstoneItemContent)] = 9 }),
    (Output: ContentId(redstoneItemContent), Count: 9, Inputs: new Dictionary<int, int> { [ContentId(redstoneBlockContent)] = 1 }),
    (Output: ContentId(pistonItemContent), Count: 1, Inputs: new Dictionary<int, int> { [oakId] = 3, [Convert.ToInt32(itemId.GetField("StoneBlock")!.GetValue(null))] = 4,
        [Convert.ToInt32(itemId.GetField("IronBar")!.GetValue(null))] = 1, [ContentId(redstoneItemContent)] = 1 }),
    (Output: ContentId(stickyItemContent), Count: 1, Inputs: new Dictionary<int, int> { [ContentId(pistonItemContent)] = 1, [Convert.ToInt32(itemId.GetField("Gel")!.GetValue(null))] = 1 }),
    (Output: ContentId(pulserItemContent), Count: 1, Inputs: new Dictionary<int, int> { [Convert.ToInt32(itemId.GetField("StoneBlock")!.GetValue(null))] = 4,
        [ContentId(redstoneItemContent)] = 2, [Convert.ToInt32(itemId.GetField("IronBar")!.GetValue(null))] = 1 }),
    (Output: ContentId(hopperItemContent), Count: 1, Inputs: new Dictionary<int, int> { [Convert.ToInt32(itemId.GetField("IronBar")!.GetValue(null))] = 5,
        [Convert.ToInt32(itemId.GetField("Chest")!.GetValue(null))] = 1 }),
    (Output: ContentId(hopperItemContent), Count: 1, Inputs: new Dictionary<int, int> { [Convert.ToInt32(itemId.GetField("IronBar")!.GetValue(null))] = 5,
        [ContentId(minecraftChestItem)] = 1 }) })
{
    object recipe = addedRecipes.Single(r => OutputId(r) == expected.Output && OutputStack(r) == expected.Count &&
        expected.Inputs.Keys.All(key => RecipeIngredients(r).Any(i => (int)itemType.GetField("type")!.GetValue(i)! == key)));
    var inputs = RecipeIngredients(recipe).ToDictionary(i => (int)itemType.GetField("type")!.GetValue(i)!, i => (int)itemType.GetField("stack")!.GetValue(i)!);
    if (OutputStack(recipe) != expected.Count || !RequiredTiles(recipe).SequenceEqual(new[] { tableTileId }) ||
        inputs.Count != expected.Inputs.Count || expected.Inputs.Any(pair => inputs.GetValueOrDefault(pair.Key) != pair.Value))
        throw new InvalidOperationException("Incorrect redstone/piston recipe, quantity or Minecraft station requirement.");
}
Console.WriteLine("PASS actual mechanism recipes: exact pistons/pulser, hopper accepts either vanilla or Minecraft chest with five Iron Bars, all at the MC table");
foreach (object material in materials)
{
    string key = (string)material.GetType().GetProperty("Key")!.GetValue(material)!;
    int blockId = (int)material.GetType().GetProperty("ItemType")!.GetValue(material)!;
    int ingredientId = expectedStorageIngredients[key];
    object forward = addedRecipes.Single(r => OutputId(r) == blockId);
    object reverse = addedRecipes.Single(r => OutputId(r) == ingredientId);
    object[] inputs = RecipeIngredients(forward), reverseInputs = RecipeIngredients(reverse);
    if (OutputStack(forward) != 1 || inputs.Length != 1 || (int)itemType.GetField("type")!.GetValue(inputs[0])! != ingredientId ||
        (int)itemType.GetField("stack")!.GetValue(inputs[0])! != 9 || OutputStack(reverse) != 9 || reverseInputs.Length != 1 ||
        (int)itemType.GetField("type")!.GetValue(reverseInputs[0])! != blockId || (int)itemType.GetField("stack")!.GetValue(reverseInputs[0])! != 1 ||
        !RequiredTiles(forward).SequenceEqual(new[] { tableTileId }) || !RequiredTiles(reverse).SequenceEqual(new[] { tableTileId }))
        throw new InvalidOperationException("Storage block crafting/decompression must be an exact 9:1 cycle at the MC table: " + key);
}
Console.WriteLine("PASS actual storage recipes: all 27 exact 9:1 forward/reverse cycles use bars or gems and the MC table");

int tileCount = (int)game.GetType("Terraria.ModLoader.TileLoader")!.GetProperty("TileCount")!.GetValue(null)!;
bool[] adjacency = new bool[tileCount];
playerType.GetProperty("adjTile")!.SetValue(attacker, adjacency);
var stationCheck = recipeType.GetMethod("PlayerMeetsTileRequirements", BindingFlags.NonPublic | BindingFlags.Static)!;
bool MeetsStation(object recipe) => (bool)stationCheck.Invoke(null, new[] { attacker, recipe })!;
adjacency[Convert.ToInt32(game.GetType("Terraria.ID.TileID")!.GetField("WorkBenches")!.GetValue(null))] = true;
if (MeetsStation(prefabRecipe) || !MeetsStation(oakRecipe) || !MeetsStation(tableRecipe))
    throw new InvalidOperationException("An ordinary bench cannot replace the MC table; bootstrap recipes need neither.");
adjacency[tableTileId] = true;
if (addedRecipes.Any(r => !MeetsStation(r))) throw new InvalidOperationException("The new station must satisfy every MC recipe.");
adjacency[tableTileId] = false;
if (addedRecipes.Where(r => OutputId(r) != oakId && OutputId(r) != tableId).Any(MeetsStation))
    throw new InvalidOperationException("Leaving or removing the new station must close all restricted recipes.");
Console.WriteLine("PASS native station checks: plain bench rejected, new MC table opens all restricted crafts, leaving closes them");
prefabContent.GetType().GetMethod("SetDefaults")!.Invoke(prefabContent, null);
object prefabItem = prefabContent.GetType().GetProperty("Item")!.GetValue(prefabContent)!;
if (!(bool)itemType.GetField("consumable")!.GetValue(prefabItem)! || (bool)prefabContent.GetType().GetMethod("ConsumeItem")!.Invoke(prefabContent, new[] { attacker })!)
    throw new InvalidOperationException("An unspawned prefab cannot consume itself.");
bool failedUse = (bool)prefabContent.GetType().GetMethod("TrySpawn", BindingFlags.NonPublic | BindingFlags.Instance)!
    .Invoke(prefabContent, new object?[] { attacker, 1, 1, null })!;
if (failedUse || (bool)prefabContent.GetType().GetMethod("ConsumeItem")!.Invoke(prefabContent, new[] { attacker })!)
    throw new InvalidOperationException("A failed ground spawn must preserve the prefab item.");
Console.WriteLine("PASS native prefab defaults and failed placement: consumable spawner, no loss at invalid ground");
tilemapType.GetMethod("ClearEverything")!.Invoke(tilemap, null);
int stoneTile = Convert.ToInt32(game.GetType("Terraria.ID.TileID")!.GetField("Stone")!.GetValue(null));
((bool[])mainType.GetField("tileSolid")!.GetValue(null)!)[stoneTile] = true;
object floorTile = tilemapType.GetProperty("Item", new[] { typeof(int), typeof(int) })!.GetValue(tilemap, new object[] { 30, 50 })!;
tileType.GetMethod("ResetToType")!.Invoke(floorTile, new object[] { (ushort)stoneTile });
object collisionWorld = Activator.CreateInstance(mod.GetType("DimensionalTerraria.World.TerrariaCollisionWorld")!)!;
bool SpawnerFits(object spawner) => (bool)spawner.GetType().GetMethod("SpawnPosition", BindingFlags.NonPublic | BindingFlags.Instance)!
    .Invoke(spawner, new object?[] { collisionWorld, 30, 50, null, null })!;
if (!SpawnerFits(prefabContent) || !SpawnerFits(egg)) throw new InvalidOperationException("Both spawners should fit on open dry ground.");
object ceilingTile = tilemapType.GetProperty("Item", new[] { typeof(int), typeof(int) })!.GetValue(tilemap, new object[] { 30, 45 })!;
tileType.GetMethod("ResetToType")!.Invoke(ceilingTile, new object[] { (ushort)stoneTile });
if (SpawnerFits(prefabContent) || !SpawnerFits(egg)) throw new InvalidOperationException("The prefab must use golem headroom, not sheep headroom.");
if ((bool)prefabContent.GetType().GetMethod("TrySpawn", BindingFlags.NonPublic | BindingFlags.Instance)!
    .Invoke(prefabContent, new object?[] { attacker, 30, 50, null })! ||
    (bool)prefabContent.GetType().GetMethod("ConsumeItem")!.Invoke(prefabContent, new[] { attacker })!)
    throw new InvalidOperationException("Insufficient golem headroom must fail before spawning or consumption.");
Console.WriteLine("PASS actual prefab/sheep ground adapters: open floor accepted, tall headroom enforced, failed use preserves item");
recipeLoader.GetField("setupRecipes", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, false);
recipeLoader.GetProperty("CurrentMod", BindingFlags.NonPublic | BindingFlags.Static)!.SetValue(null, null);
BoatChecks.Run(game, mod, boatTileContent, boatItemContent, boatEntityContent, captiveContent);
RedstoneChecks.Run(game, mod, redstoneTileContent, pulserTileContent, pistonTileContent, stickyTileContent, pistonHeadContent, pistonEntityContent, boatTileContent, boatEntityContent);
HopperChecks.Run(game, mod, hopperTileContent, hopperEntityContent, pistonTileContent, pistonEntityContent, redstoneTileContent);
VillagerChecks.Run(game, mod, villagerContent, golemContent, boatTileContent, boatEntityContent, slimeEggContent);
BoatPersistenceChecks.Run(game, mod, boatTileContent, boatEntityContent, captiveContent, sheepContent, villagerContent, golemContent);
ProjectionEquipmentChecks.Run(game, mod, bedrockContent, bedrockItem, projectionPick, projectionHelmet, projectionChest, projectionLegs);
ConstructionChecks.Run(game, mod, minecraftChestTile, minecraftChestItem, villagerEggContent, villagerContent, hopperTileContent, hopperEntityContent);
PlacementChecks.Run(game, mod, minecraftChestItem, pistonItemContent);
PresentationChecks.Run(game,mod,contentMod);
FlightRegressionChecks.Run(game,mod,fireworkContent,enemyStateContent);
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
    Environment.ExitCode = 1;
}
