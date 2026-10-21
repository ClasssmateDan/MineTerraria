using System;

namespace DimensionalTerraria.Systems;

internal static class BedrockMining
{
    [ThreadStatic] private static Permit? current;
    internal static bool Allows(int x, int y) => current is { } permit && permit.X == x && permit.Y == y;
    internal static IDisposable Allow(int x, int y) => new Permit(x, y);

    private sealed class Permit : IDisposable
    {
        internal readonly int X, Y;
        private readonly Permit? previous;
        internal Permit(int x, int y) { X = x; Y = y; previous = current; current = this; }
        public void Dispose() => current = previous;
    }
}
