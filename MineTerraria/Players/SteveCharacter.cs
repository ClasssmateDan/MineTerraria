using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace DimensionalTerraria.Players;

public sealed class SteveCharacter : ModPlayer
{
    public bool IsSteve { get; private set; }
    public override void Initialize() => IsSteve = false;
    internal void ChooseSteve() => IsSteve = true;

    public override void SaveData(TagCompound tag)
    {
        if (IsSteve) tag["SteveCharacter"] = (byte)1;
    }

    public override void LoadData(TagCompound tag) => IsSteve = tag.GetByte("SteveCharacter") != 0;
}
