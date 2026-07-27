namespace GmToolkit.Core.Generator;

/// <summary>
/// <see cref="IRandomSource"/> backed by <see cref="System.Random"/>. Use the default constructor
/// for real generation (non-deterministic) and the seeded constructor in tests, or anywhere else
/// reproducibility matters (e.g. a "reroll all with the same seed" debug feature).
/// </summary>
public sealed class SystemRandomSource : IRandomSource
{
    private readonly Random _random;

    /// <summary>Creates a non-deterministic source for real generation.</summary>
    public SystemRandomSource()
        : this(new Random())
    {
    }

    /// <summary>Creates a deterministic source seeded with <paramref name="seed"/>, for tests.</summary>
    public SystemRandomSource(int seed)
        : this(new Random(seed))
    {
    }

    private SystemRandomSource(Random random)
    {
        _random = random;
    }

    public double NextDouble() => _random.NextDouble();

    public int NextInt(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);
}