using Microsoft.Xna.Framework;

namespace DimensionalTerraria.Rendering;

internal interface IPixelCreature
{
    bool SpriteReady { get; }
    float SpriteScale { get; }
    Rectangle SpriteFrame { get; }
    Vector2 SpriteOrigin { get; }
}
