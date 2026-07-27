using System.Diagnostics.CodeAnalysis;

namespace GmToolkit.Core.Generator;

/// <summary>
/// Looks up an <see cref="IGenerator{TResult}"/> by category (e.g. "occupation", "names"). This is
/// the extensibility point for adding a new category, such as a hypothetical future "loot" table:
/// drop a new <c>Resources/GeneratorTables/loot.json</c> file with <c>"category": "loot"</c> next
/// to the existing tables, and <see cref="GeneratorRegistry"/> — built from
/// <see cref="GeneratorTableLoader.LoadAll"/>'s output — picks it up automatically. No change to
/// this interface, <see cref="GeneratorRegistry"/>'s implementation, or <see cref="INpcGenerator"/>
/// is needed to make a new single-table category generatable through <see cref="GetGenerator"/>.
/// </summary>
public interface IGeneratorRegistry
{
    /// <summary>Every category with at least one registered generator (e.g. "names", "occupation").</summary>
    IReadOnlyCollection<string> Categories { get; }

    /// <summary>
    /// Gets the generator for <paramref name="category"/>. Throws <see cref="KeyNotFoundException"/>
    /// if no generator is registered for it — matching <see cref="Dictionary{TKey,TValue}"/>'s own
    /// indexer convention, since a category name is effectively a dictionary key here. Callers that
    /// want a non-throwing lookup should use <see cref="TryGetGenerator"/> instead.
    /// </summary>
    IGenerator<string> GetGenerator(string category);

    /// <summary>Non-throwing counterpart to <see cref="GetGenerator"/>.</summary>
    bool TryGetGenerator(string category, [NotNullWhen(true)] out IGenerator<string>? generator);

    /// <summary>
    /// Gets the registry's <see cref="NameGenerator"/> directly (rather than through the narrower
    /// <see cref="IGenerator{TResult}"/> view <see cref="GetGenerator"/> returns), so a caller that
    /// needs the culture-aware overload — #27's explicit culture selection — can reach it. Throws
    /// <see cref="KeyNotFoundException"/> if no "names" category tables are registered.
    /// </summary>
    NameGenerator GetNameGenerator();
}