using Newtonsoft.Json;
using Terraria.ModLoader.Config;

namespace DimensionalTerraria.Configuration;

public sealed class VanillaItemCatalogConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;
    [JsonIgnore, ShowDespiteJsonIgnore, CustomModConfigItem(typeof(VanillaItemCatalogElement))]
    public bool Items { get; set; }
}
