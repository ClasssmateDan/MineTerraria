namespace DimensionalTerraria.Core;

public static class HotbarSelection
{
    public const int SlotCount = 10;

    public static int Scroll(int selected, int wheelSteps)
    {
        int value = (selected - wheelSteps) % SlotCount;
        return value < 0 ? value + SlotCount : value;
    }
}
