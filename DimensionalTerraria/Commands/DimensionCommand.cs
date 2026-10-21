using DimensionalTerraria.Systems;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace DimensionalTerraria.Commands;

public sealed class DimensionCommand : ModCommand
{
    public override CommandType Type => CommandType.Chat;
    public override string Command => "dimension";
    public override string Usage => "/dimension [back]";
    public override string Description => "Switch between 2D and 3D; back returns to departure.";

    public override void Action(CommandCaller caller, string input, string[] args)
    {
        if (Main.netMode != NetmodeID.SinglePlayer) { caller.Reply("MineTerraria supports single-player only."); return; }
        if (args.Length > 1 || args.Length == 1 && !args[0].Equals("back", System.StringComparison.OrdinalIgnoreCase)) { caller.Reply(Usage); return; }
        DimensionSystem system = DimensionSystem.Instance;
        if (args.Length > 0 && args[0].Equals("back", System.StringComparison.OrdinalIgnoreCase))
            system.RecoverToDeparture();
        else
            system.RequestToggle("chat command");
    }
}
