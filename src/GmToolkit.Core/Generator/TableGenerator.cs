namespace GmToolkit.Core.Generator;

/// <summary>
/// The default <see cref="IGenerator{TResult}"/> for any category backed by a single
/// <see cref="GeneratorTable"/> (every category except "names", which needs
/// <see cref="NameGenerator"/> to compose a given name and surname from separate tagged entries).
/// A weighted pick over the whole table's entries, with no tag filtering — that's what makes it
/// generic enough to work for any newly-added single-table category with no code changes.
/// </summary>
public sealed class TableGenerator : IGenerator<string>
{
    private readonly GeneratorTable _table;

    public TableGenerator(GeneratorTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        _table = table;
    }

    public string Generate(IRandomSource random) => WeightedPicker.Pick(_table.Entries, random).Value;
}