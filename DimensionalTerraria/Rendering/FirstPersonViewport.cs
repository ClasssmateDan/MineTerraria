using DimensionalTerraria.Core;
using Terraria;
using Terraria.GameInput;

namespace DimensionalTerraria.Rendering;

internal static class FirstPersonViewport
{
    // Main.screenWidth/Height are temporarily divided by UIScale inside UI layers.
    public static ViewLayout Layout => new(PlayerInput.RealScreenWidth, PlayerInput.RealScreenHeight, Main.UIScale);
}
