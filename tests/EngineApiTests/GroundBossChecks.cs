using System.Reflection;

internal static class GroundBossChecks
{
    const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    internal static void Run(Assembly game, Assembly code, Func<object> createBoat)
    {
        Type npcType = game.GetType("Terraria.NPC")!, idType = game.GetType("Terraria.ID.NPCID")!;
        Type state = code.GetType("DimensionalTerraria.Systems.BoatCaptive")!;
        Array npcs = (Array)game.GetType("Terraria.Main")!.GetField("npc")!.GetValue(null)!;
        Array saved = (Array)npcs.Clone();
        MethodInfo defaults = npcType.GetMethods().Single(m => m.Name == "SetDefaults" && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(int));
        object New(string name)
        {
            object npc = Activator.CreateInstance(npcType)!;
            int type = Convert.ToInt32(idType.GetField(name)!.GetRawConstantValue());
            defaults.Invoke(npc, new[] { (object)type, Activator.CreateInstance(defaults.GetParameters()[1].ParameterType)! });
            npcType.GetField("active")!.SetValue(npc, true); npcType.GetField("target")!.SetValue(npc, 0);
            npcType.GetField("whoAmI")!.SetValue(npc, 0); npcType.GetField("timeLeft")!.SetValue(npc, 1000);
            npcType.GetProperty("Bottom")!.SetValue(npc, Activator.CreateInstance(npcType.GetField("position")!.FieldType, new object[] { 488f, 800f }));
            npcs.SetValue(npc, 0); return npc;
        }
        bool Eligible(object npc) => (bool)state.GetMethod("Eligible", All)!.Invoke(null, new[] { npc })!;
        try
        {
            for (int i = 0; i < npcs.Length; i++) npcs.SetValue(Activator.CreateInstance(npcType), i);
            object deer = New("Deerclops");
            if (!(bool)npcType.GetField("noGravity")!.GetValue(deer)! || !Eligible(deer))
                throw new InvalidOperationException("Deerclops is terrestrial despite its custom-physics gravity flags and must now be capturable.");
            object boat = createBoat();
            object velocity = Activator.CreateInstance(npcType.GetField("velocity")!.FieldType, new object[] { 2f, -3f })!;
            npcType.GetField("velocity")!.SetValue(deer, velocity);
            if (!(bool)boat.GetType().GetMethod("TryCapture", All)!.Invoke(boat, new[] { deer })!) throw new InvalidOperationException("A full native Deerclops must board the boat.");
            boat.GetType().GetMethod("Release", All)!.Invoke(boat, null);
            if (!npcType.GetField("velocity")!.GetValue(deer)!.Equals(velocity)) throw new InvalidOperationException("Generic boss release must preserve its pre-capture movement impulse.");
            boat.GetType().GetMethod("Break", All)!.Invoke(boat, null);
            ((float[])npcType.GetField("ai")!.GetValue(deer)!)[0] = 8;
            if (Eligible(deer)) throw new InvalidOperationException("A departing Deerclops cannot be frozen during disappearance.");
            Console.WriteLine("PASS formal terrestrial boss capture: real Deerclops boards despite custom gravity, resumes its movement and rejects departure states");

            object queen = New("QueenSlimeBoss");
            ((float[])npcType.GetField("localAI")!.GetValue(queen)!)[0] = (int)npcType.GetField("lifeMax")!.GetValue(queen)!;
            if (!Eligible(queen)) throw new InvalidOperationException("Queen Slime's initialized ground phase must be eligible.");
            ((float[])npcType.GetField("ai")!.GetValue(queen)!)[0] = 2;
            if (Eligible(queen)) throw new InvalidOperationException("Queen Slime teleport animation must not be frozen.");
            ((float[])npcType.GetField("ai")!.GetValue(queen)!)[0] = 0;
            npcType.GetField("life")!.SetValue(queen, (int)npcType.GetField("lifeMax")!.GetValue(queen)! / 2);
            if (Eligible(queen)) throw new InvalidOperationException("Queen Slime's airborne second phase must not enter the terrestrial whitelist.");
            foreach (string name in new[] { "Golem", "Plantera", "SkeletronHead", "SkeletronPrime", "WallofFlesh", "MoonLordCore", "BrainofCthulhu", "EyeofCthulhu" })
                if (Eligible(New(name))) throw new InvalidOperationException("A multipart root or flying boss cannot be captured: " + name);
            Console.WriteLine("PASS formal boss state guards: Queen Slime ground phase accepted, teleport/flying phases and multipart roots rejected before attachments spawn");
        }
        finally { Array.Copy(saved, npcs, npcs.Length); }
    }
}
