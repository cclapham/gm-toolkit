namespace GmToolkit.Core.Generator;

/// <summary>
/// Generates a full name by composing one "given"-tagged and one "surname"-tagged entry from a
/// single culture's <see cref="GeneratorTable"/> (see <c>names-highland.json</c>/
/// <c>names-coastal.json</c>) — never mixing a given name from one culture with a surname from
/// another. This is intrinsic to what "generate a name" means, not a caller-supplied constraint,
/// so it's built in here rather than left to issue #27.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Generate(IRandomSource)"/> (the plain <see cref="IGenerator{TResult}"/> member)
/// picks a culture uniformly at random among whichever culture tables were supplied, since #27
/// (explicit GM-facing culture selection) doesn't exist yet.
/// </para>
/// <para>
/// <b>#27:</b> <see cref="Generate(IRandomSource, string?)"/> accepts an optional
/// <paramref name="culture"/> (matched case-insensitively against <see cref="GeneratorTable.Culture"/>)
/// that, when supplied, skips the random culture pick entirely. It's obtained from
/// <see cref="IGeneratorRegistry.GetNameGenerator"/>, which returns the concrete
/// <see cref="NameGenerator"/> rather than the narrower <see cref="IGenerator{TResult}"/> view.
/// An unrecognized culture no longer throws (it did prior to #27): it falls back to a randomly
/// chosen culture, same as passing <c>culture: null</c>, so a GM requesting a typo'd or
/// no-longer-configured culture still gets a usable name instead of an exception. Callers that need
/// to know a fallback happened — #27's "visible notice" acceptance criterion — should call
/// <see cref="GenerateWithNotice"/> instead, which returns the same value alongside a
/// <see cref="GenerationResult.FallbackNotice"/>.
/// </para>
/// </remarks>
public sealed class NameGenerator : IGenerator<string>
{
    private const string GivenTag = "given";
    private const string SurnameTag = "surname";

    private readonly IReadOnlyList<GeneratorTable> _cultureTables;

    public NameGenerator(IReadOnlyList<GeneratorTable> cultureTables)
    {
        ArgumentNullException.ThrowIfNull(cultureTables);
        if (cultureTables.Count == 0)
        {
            throw new ArgumentException("At least one name culture table is required.", nameof(cultureTables));
        }

        _cultureTables = cultureTables;
    }

    /// <summary>The cultures available to draw from, e.g. "highland", "coastal".</summary>
    public IReadOnlyList<string> Cultures => _cultureTables.Select(t => t.Culture ?? string.Empty).ToList();

    /// <summary>Generates a name from a culture chosen uniformly at random.</summary>
    public string Generate(IRandomSource random) => Generate(random, culture: null);

    /// <summary>
    /// Generates a name. When <paramref name="culture"/> is null, a culture table is chosen
    /// uniformly at random. When <paramref name="culture"/> is supplied but doesn't match any
    /// known culture, falls back to a randomly chosen culture rather than throwing (see this
    /// type's remarks) — that fallback is silent from this overload's point of view; use
    /// <see cref="GenerateWithNotice"/> if the caller needs to know it happened.
    /// </summary>
    public string Generate(IRandomSource random, string? culture) => GenerateWithNotice(random, culture).Value;

    /// <summary>
    /// Generates a name, same as <see cref="Generate(IRandomSource, string?)"/>, but returns a
    /// <see cref="GenerationResult"/> that also reports whether the requested
    /// <paramref name="culture"/> could be honored. <see cref="GenerationResult.FallbackNotice"/>
    /// is null when <paramref name="culture"/> is null (no preference was expressed) or when it
    /// matched a known culture table; it's set when <paramref name="culture"/> was non-null but
    /// didn't match anything, describing the randomly chosen culture that was used instead.
    /// </summary>
    public GenerationResult GenerateWithNotice(IRandomSource random, string? culture)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (culture is null)
        {
            return GenerationResult.Unconstrained(GenerateFrom(_cultureTables[random.NextInt(0, _cultureTables.Count)], random));
        }

        var table = TryFindCultureTable(culture);
        if (table is not null)
        {
            return GenerationResult.Unconstrained(GenerateFrom(table, random));
        }

        var fallbackTable = _cultureTables[random.NextInt(0, _cultureTables.Count)];
        var value = GenerateFrom(fallbackTable, random);
        return new GenerationResult(
            value,
            $"No name culture matching '{culture}' was found; used the '{fallbackTable.Culture}' culture instead.");
    }

    private static string GenerateFrom(GeneratorTable table, IRandomSource random)
    {
        var given = WeightedPicker.Pick(FilterByTag(table, GivenTag), random).Value;
        var surname = WeightedPicker.Pick(FilterByTag(table, SurnameTag), random).Value;

        return $"{given} {surname}";
    }

    private GeneratorTable? TryFindCultureTable(string culture)
    {
        foreach (var table in _cultureTables)
        {
            if (string.Equals(table.Culture, culture, StringComparison.OrdinalIgnoreCase))
            {
                return table;
            }
        }

        return null;
    }

    private static IReadOnlyList<GeneratorTableEntry> FilterByTag(GeneratorTable table, string tag)
    {
        var matches = table.Entries.Where(e => e.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)).ToList();
        if (matches.Count == 0)
        {
            throw new InvalidOperationException(
                $"Name culture table '{table.Id}' has no entries tagged '{tag}'.");
        }

        return matches;
    }
}