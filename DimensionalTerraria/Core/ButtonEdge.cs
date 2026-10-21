namespace DimensionalTerraria.Core;

// Input is still sampled while the Terraria world update is suspended.
public sealed class ButtonEdge
{
    private bool wasHeld;

    public bool Sample(bool held)
    {
        bool pressed = held && !wasHeld;
        wasHeld = held;
        return pressed;
    }
}
