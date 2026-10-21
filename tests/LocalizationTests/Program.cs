using Hjson;

string directory = args[0];
string[] files = Directory.GetFiles(directory, "*.hjson");
if (files.Length == 0) throw new InvalidOperationException("No localization files were found.");
foreach (string file in files)
{
    var root = HjsonValue.Parse(File.ReadAllText(file));
    var mod = root["Mods"]["DimensionalTerraria"];
    foreach (string config in new[] { "DimensionConfig", "ItemCatalogConfig", "VanillaItemCatalogConfig" })
        if (string.IsNullOrWhiteSpace(mod["Configs"][config]["DisplayName"])) throw new InvalidOperationException("Missing config label: " + config);
    foreach (string item in new[] { "CopperStorageBlock", "DiamondStorageBlock", "SheepSpawnEgg", "OakPlanks", "MinecraftCraftingTable", "PrefabIronGolem", "OakBoat", "Firework", "NetheriteIngot" })
        if (string.IsNullOrWhiteSpace(mod["Items"][item]["DisplayName"])) throw new InvalidOperationException("Missing item label: " + item);
    foreach (string key in new[] { "SearchHint", "Clear", "Previous", "Next", "Page", "NoResults", "Unavailable" })
        if (string.IsNullOrWhiteSpace(mod["Catalog"][key])) throw new InvalidOperationException("Missing catalog text: " + key);
    foreach (string key in new[] { "Summoned", "NoSpace", "Protected", "NoNpcSlot" })
        if (string.IsNullOrWhiteSpace(mod["GolemSummoning"][key])) throw new InvalidOperationException("Missing golem summoning text: " + key);
    if(mod["Configs"].ContainsKey("DebugConfig"))throw new InvalidOperationException("Debug page must not appear in release localization");
    if(string.IsNullOrWhiteSpace(mod["Catalog"]["InGameOnly"]))throw new InvalidOperationException("Missing catalog availability text");
    foreach(KeyValuePair<string,JsonValue> entry in mod["Items"])
    {
        var item=entry.Value;
        foreach(string key in new[]{"Tooltip","SetBonus"})
            if(item.ContainsKey(key) && ((string)item[key]).Contains(@"\n"))
                throw new InvalidOperationException("Literal newline escape in item text: "+entry.Key+"."+key);
        if(((string)item["Tooltip"]).Length>220)throw new InvalidOperationException("Overlong item tooltip: "+entry.Key);
    }
    string firework=mod["Items"]["Firework"]["Tooltip"];
    if(!firework.Contains('\n'))throw new InvalidOperationException("Multiline tooltips must contain actual line breaks");
    Console.WriteLine("PASS localization syntax and concise real multiline item descriptions: " + Path.GetFileName(file));
}
