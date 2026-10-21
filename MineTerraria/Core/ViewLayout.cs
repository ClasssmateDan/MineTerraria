using System.Numerics;

namespace DimensionalTerraria.Core;

// Dimensions come from the physical viewport, before Terraria applies its UI zoom.
public readonly record struct ViewLayout(int PixelWidth, int PixelHeight, float UiScale)
{
    public float UiWidth => PixelWidth / UiScale;
    public float UiHeight => PixelHeight / UiScale;
    public Vector2 PixelCenter => new(PixelWidth / 2, PixelHeight / 2);
    public Vector2 ToUi(Vector2 pixel) => pixel / UiScale;
}
