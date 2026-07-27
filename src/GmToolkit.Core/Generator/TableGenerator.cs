namespace GmToolkit.Core.Generator;

/// <summary>
/// The default <see cref="IGenerator{TResult}"/> for any category backed by a single
/// <see cref="GeneratorTable"/> (every category except "names", which needs
/// <see cref="NameGenerator"/> to compose a given name and surname from separate tagged entries).
/// <see cref="Generate(IRandomSource)"/> is a weighted pick over the whole table's entries with no
/// tag filtering — that's what makes it generic enough to work for any newly-added single-table
/// category with no code changes.
/// </summary>
/// <remarks>
/// <b>#27:</b> <see cref="GenerateWithNotice"/> adds an optional required-tag filter on top of that
/// same generic behavior (matched case-insensitively against <see cref="GeneratorTableEntry.Tags"/>),
/// used today for the "occupation" category's category tags (see
/// <c>Resources/GeneratorTables/occupation.json</c>) via <see cref="IGeneratorRegistry.GetTableGenerator"/>.
/// It isn't occupation-specific — any single-table category whose entries carry tags can be
/// filtered the same way, consistent with this type's existing "no code changes for a new category"
/// design goal.
/// </remarks>
public sealed class TableGenerator : IGenerator<string>
{
    private readonly GeneratorTable _table;

    public TableGenerator(GeneratorTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        _table = table;
    }

    public string Generate(IRandomSource random) => GenerateWithNotice(random, requiredTag: null).Value;

    /// <summary>
    /// Generates a value, same as <see cref="Generate(IRandomSource)"/>, but restricted to entries
    /// tagged with <paramref name="requiredTag"/> when it's non-null. If no entry carries that tag
    /// (an unrecognized tag, or a table whose entries aren't tagged at all), falls back to a pick
    /// over the whole table rather than throwing or returning an empty string, and reports that
    /// fallback via <see cref="GenerationResult.FallbackNotice"/>.
    /// </summary>
    public GenerationResult GenerateWithNotice(IRandomSource random, string? requiredTag)
    {
        ArgumentNullException.ThrowIfNull(random);

        if (requiredTag is null)
        {
            return GenerationResult.Unconstrained(WeightedPicker.Pick(_table.Entries, random).Value);
        }

        var matches = _table.Entries.Where(e => e.Tags.Contains(requiredTag, StringComparer.OrdinalIgnoreCase)).ToList();
        if (matches.Count > 0)
        {
            return GenerationResult.Unconstrained(WeightedPicker.Pick(matches, random).Value);
        }

        var fallbackValue = WeightedPicker.Pick(_table.Entries, random).Value;
        return new GenerationResult(
            fallbackValue,
            $"No '{_table.Id}' entries tagged '{requiredTag}' were found; used an unconstrained pick instead.");
    }
}