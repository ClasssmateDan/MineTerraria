using DimensionalTerraria.Core;
using Microsoft.Xna.Framework;
using Terraria;

namespace DimensionalTerraria.World;

internal sealed class TerrariaAggroMemory(NPC npc) : IAggroMemory
{
    public int Target { get => npc.target; set => npc.target = value; }
    public int PreviousTarget { get => npc.oldTarget; set => npc.oldTarget = value; }
    public void ClearTargetArea() => npc.targetRect = Rectangle.Empty;
}
