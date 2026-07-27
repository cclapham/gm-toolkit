namespace GmToolkit.Core.Generator;

/// <summary>
/// Standard cumulative-weight random selection over a list of <see cref="GeneratorTableEntry"/>.
/// Sums the entries' weights, draws a point uniformly in [0, totalWeight), then scans the
/// cumulative weight to find which entry's range the point fell in. An entry with 10x another's
/// weight is 10x as likely to be picked — this is deliberately not a uniform index pick.
/// </summary>
public static class WeightedPicker
{
    /// <summary>
    /// Picks one entry from <paramref name="entries"/> with probability proportional to its
    /// <see cref="GeneratorTableEntry.Weight"/>. Throws <see cref="ArgumentException"/> if
    /// <paramref name="entries"/> is empty. A single-entry list always returns that entry.
    /// </summary>
    public static GeneratorTableEntry Pick(IReadOnlyList<GeneratorTableEntry> entries, IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(random);

        if (entries.Count == 0)
        {
            throw new ArgumentException("Cannot pick from an empty list of entries.", nameof(entries));
        }

        if (entries.Count == 1)
        {
            return entries[0];
        }

        var totalWeight = 0.0;
        foreach (var entry in entries)
        {
            totalWeight += entry.Weight;
        }

        // GeneratorTableLoader validation guarantees every entry's weight is > 0, so
        // totalWeight > 0 here whenever entries is non-empty.
        var roll = random.NextDouble() * totalWeight;

        var cumulative = 0.0;
        foreach (var entry in entries)
        {
            cumulative += entry.Weight;
            if (roll < cumulative)
            {
                return entry;
            }
        }

        // In exact arithmetic this line is unreachable -- both loops above sum the same weights in
        // the same order, so `cumulative` ends up bit-identical to `totalWeight`, and
        // NextDouble() < 1 guarantees `roll < totalWeight`. The one edge case that's not exact
        // arithmetic: rounding `random.NextDouble() * totalWeight` can, in a vanishingly rare case,
        // round the product up to (or past) that same `totalWeight`/`cumulative` value. Falling
        // through to the last entry here keeps this method total instead of ever throwing on that
        // razor-thin edge case.
        return entries[^1];
    }
}