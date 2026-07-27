using System.Diagnostics.CodeAnalysis;

namespace GmToolkit.Core.Generator;

/// <summary>
/// Default <see cref="IGeneratorRegistry"/>, built by grouping a set of loaded
/// <see cref="GeneratorTable"/>s by <see cref="GeneratorTable.Category"/>. Every category gets a
/// plain <see cref="TableGenerator"/> wrapping its one table, except "names", which is
/// special-cased into a single <see cref="NameGenerator"/> spanning every culture table found
/// (there can be more than one — see <c>names-highland.json</c>/<c>names-coastal.json</c>).
/// That special case is about the "names" category's data shape (composing given+surname across
/// possibly-several culture tables), not about hardcoding which categories exist — a brand new
/// single-table category needs no change here at all (see <see cref="IGeneratorRegistry"/>'s
/// remarks), and a second multi-table category would need its own equally-small special case here,
/// same as "names" did, rather than a change to callers.
/// </summary>
public sealed class GeneratorRegistry : IGeneratorRegistry
{
    private const string NamesCategory = "names";

    private readonly Dictionary<string, IGenerator<string>> _generators;
    private readonly NameGenerator? _nameGenerator;

    public GeneratorRegistry(IEnumerable<GeneratorTable> tables)
    {
        ArgumentNullException.ThrowIfNull(tables);

        var generators = new Dictionary<string, IGenerator<string>>(StringComparer.OrdinalIgnoreCase);
        NameGenerator? nameGenerator = null;

        foreach (var group in tables.GroupBy(t => t.Category, StringComparer.OrdinalIgnoreCase))
        {
            var tablesInGroup = group.ToList();

            if (string.Equals(group.Key, NamesCategory, StringComparison.OrdinalIgnoreCase))
            {
                nameGenerator = new NameGenerator(tablesInGroup);
                generators[group.Key] = nameGenerator;
                continue;
            }

            if (tablesInGroup.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Category '{group.Key}' has {tablesInGroup.Count} tables; only the 'names' "
                    + "category currently supports more than one table per category (one per culture).");
            }

            generators[group.Key] = new TableGenerator(tablesInGroup[0]);
        }

        _generators = generators;
        _nameGenerator = nameGenerator;
    }

    /// <summary>Builds a registry from every embedded generator table (the app's real startup path).</summary>
    public static GeneratorRegistry FromEmbeddedTables() => new(GeneratorTableLoader.LoadAll());

    public IReadOnlyCollection<string> Categories => _generators.Keys;

    public IGenerator<string> GetGenerator(string category)
    {
        if (!_generators.TryGetValue(category, out var generator))
        {
            throw new KeyNotFoundException($"No generator table registered for category '{category}'.");
        }

        return generator;
    }

    public bool TryGetGenerator(string category, [NotNullWhen(true)] out IGenerator<string>? generator)
        => _generators.TryGetValue(category, out generator);

    public NameGenerator GetNameGenerator()
        => _nameGenerator ?? throw new KeyNotFoundException("No 'names' category tables are registered.");

    public TableGenerator GetTableGenerator(string category)
    {
        var generator = GetGenerator(category);
        if (generator is not TableGenerator tableGenerator)
        {
            throw new InvalidOperationException(
                $"Category '{category}' is not backed by a single-table {nameof(TableGenerator)} "
                + $"(e.g. \"names\" is a {nameof(NameGenerator)} — use {nameof(GetNameGenerator)} instead).");
        }

        return tableGenerator;
    }
}