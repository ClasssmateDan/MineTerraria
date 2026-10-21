using Newtonsoft.Json;
using Terraria.ModLoader.Config;

namespace DimensionalTerraria.Configuration;

public sealed class ItemCatalogConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;
    [JsonIgnore, ShowDespiteJsonIgnore, CustomModConfigItem(typeof(ItemCatalogElement))]
    public bool Items { get; set; }
}
