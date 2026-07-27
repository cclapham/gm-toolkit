namespace GmToolkit.Core.Generator;

/// <summary>
/// Abstraction over a source of randomness for the generator engine. Exists so tests can inject a
/// seeded, reproducible source (see <see cref="SystemRandomSource"/>'s seeded constructor) while
/// production code uses an unseeded one — nothing in the generator ever touches
/// <see cref="System.Random"/> directly.
/// </summary>
public interface IRandomSource
{
    /// <summary>
    /// Returns a random double in the half-open range [0, 1). Backs the weighted-selection
    /// algorithm in <see cref="WeightedPicker"/> (draw a point in [0, totalWeight) by scaling
    /// this value) and any other proportional pick.
    /// </summary>
    double NextDouble();

    /// <summary>
    /// Returns a random integer in the half-open range [<paramref name="minInclusive"/>,
    /// <paramref name="maxExclusive"/>). Used for uniform picks over a small fixed set (e.g.
    /// choosing which name-culture table to draw from) where expressing the pick as an index is
    /// clearer than scaling <see cref="NextDouble"/> by hand.
    /// </summary>
    int NextInt(int minInclusive, int maxExclusive);
}