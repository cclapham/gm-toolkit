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
/// <b>Seam for #27:</b> <see cref="Generate(IRandomSource, string?)"/> accepts an optional
/// <paramref name="culture"/> (matched case-insensitively against <see cref="GeneratorTable.Culture"/>)
/// that, when supplied, skips the random culture pick entirely. #27 can call this overload
/// directly — obtained from <see cref="IGeneratorRegistry.GetNameGenerator"/>, which returns the
/// concrete <see cref="NameGenerator"/> rather than the narrower <see cref="IGenerator{TResult}"/>
/// view — with no changes needed here. An unrecognized culture throws today; #27's own acceptance
/// criteria call for a fallback-with-notice instead, which is #27's concern to add, not a redesign
/// of this method.
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
    /// uniformly at random (today's default behavior, ahead of #27's explicit selection).
    /// Otherwise, the matching culture table is used, or <see cref="ArgumentException"/> is thrown
    /// if no table matches.
    /// </summary>
    public string Generate(IRandomSource random, string? culture)
    {
        ArgumentNullException.ThrowIfNull(random);

        var table = culture is null
            ? _cultureTables[random.NextInt(0, _cultureTables.Count)]
            : FindCultureTable(culture);

        var given = WeightedPicker.Pick(FilterByTag(table, GivenTag), random).Value;
        var surname = WeightedPicker.Pick(FilterByTag(table, SurnameTag), random).Value;

        return $"{given} {surname}";
    }

    private GeneratorTable FindCultureTable(string culture)
    {
        foreach (var table in _cultureTables)
        {
            if (string.Equals(table.Culture, culture, StringComparison.OrdinalIgnoreCase))
            {
                return table;
            }
        }

        throw new ArgumentException($"No name culture table found for culture '{culture}'.", nameof(culture));
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