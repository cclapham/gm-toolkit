using System.Reflection;
using System.Text.Json;

namespace GmToolkit.Core.Generator;

/// <summary>
/// Loads and validates <see cref="GeneratorTable"/> data from the JSON files embedded from
/// <c>Resources/GeneratorTables/*.json</c> (see the <c>GmToolkit.Core.csproj</c>
/// <c>EmbeddedResource</c> items). Only reads and validates table data — the weighted-random-pick
/// logic that consumes these tables is issue #26 and is deliberately not implemented here.
/// </summary>
public static class GeneratorTableLoader
{
    /// <summary>
    /// The logical resource name prefix assigned to every embedded generator table file (see the
    /// <c>LogicalName</c> metadata on the <c>EmbeddedResource</c> item in the csproj).
    /// </summary>
    public const string ResourceNamePrefix = "GmToolkit.Core.GeneratorTables.";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Loads and validates every embedded generator table found in <paramref name="assembly"/>
    /// (defaults to the assembly containing this loader). Throws
    /// <see cref="GeneratorTableLoadException"/> if no tables are found, or if any table is
    /// malformed or fails validation.
    /// </summary>
    public static IReadOnlyList<GeneratorTable> LoadAll(Assembly? assembly = null)
    {
        assembly ??= typeof(GeneratorTableLoader).Assembly;

        var resourceNames = GetTableResourceNames(assembly);
        if (resourceNames.Count == 0)
        {
            throw new GeneratorTableLoadException(
                $"No embedded generator table resources were found with prefix '{ResourceNamePrefix}' in assembly '{assembly.GetName().Name}'.");
        }

        var tables = new List<GeneratorTable>(resourceNames.Count);
        foreach (var resourceName in resourceNames)
        {
            tables.Add(LoadResource(assembly, resourceName));
        }

        return tables;
    }

    /// <summary>
    /// Returns the logical names of every embedded generator table resource in
    /// <paramref name="assembly"/>, sorted for deterministic ordering.
    /// </summary>
    public static IReadOnlyList<string> GetTableResourceNames(Assembly assembly)
    {
        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourceNamePrefix, StringComparison.Ordinal)
                && name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Loads and validates a single embedded generator table by its logical resource name.
    /// </summary>
    public static GeneratorTable LoadResource(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new GeneratorTableLoadException(
                $"Embedded generator table resource '{resourceName}' was not found in assembly '{assembly.GetName().Name}'.");

        GeneratorTable? table;
        try
        {
            table = JsonSerializer.Deserialize<GeneratorTable>(stream, SerializerOptions);
        }
        catch (JsonException ex)
        {
            throw new GeneratorTableLoadException(
                $"Generator table '{resourceName}' contains malformed JSON: {ex.Message}", ex);
        }

        if (table is null)
        {
            throw new GeneratorTableLoadException($"Generator table '{resourceName}' deserialized to nothing (JSON 'null').");
        }

        Validate(table, resourceName);
        return table;
    }

    /// <summary>Tag required on at least one entry of every "names"-category table (see <see cref="Validate"/>).</summary>
    private const string GivenTag = "given";

    /// <summary>Tag required on at least one entry of every "names"-category table (see <see cref="Validate"/>).</summary>
    private const string SurnameTag = "surname";

    /// <summary>The <see cref="GeneratorTable.Category"/> value that triggers the given/surname tag check.</summary>
    private const string NamesCategory = "names";

    private static void Validate(GeneratorTable table, string resourceName)
    {
        if (string.IsNullOrWhiteSpace(table.Id))
        {
            throw new GeneratorTableLoadException($"Generator table '{resourceName}' is missing a required, non-empty 'id'.");
        }

        if (string.IsNullOrWhiteSpace(table.Category))
        {
            throw new GeneratorTableLoadException($"Generator table '{resourceName}' is missing a required, non-empty 'category'.");
        }

        if (table.Entries.Count == 0)
        {
            throw new GeneratorTableLoadException($"Generator table '{resourceName}' (id '{table.Id}') has no entries.");
        }

        for (var index = 0; index < table.Entries.Count; index++)
        {
            var entry = table.Entries[index];

            if (string.IsNullOrWhiteSpace(entry.Value))
            {
                throw new GeneratorTableLoadException(
                    $"Generator table '{resourceName}' (id '{table.Id}') entry #{index} is missing a required, non-empty 'value'.");
            }

            if (entry.Weight <= 0)
            {
                throw new GeneratorTableLoadException(
                    $"Generator table '{resourceName}' (id '{table.Id}') entry '{entry.Value}' has a non-positive weight ({entry.Weight}); weight must be greater than zero.");
            }
        }

        if (string.Equals(table.Category, NamesCategory, StringComparison.OrdinalIgnoreCase))
        {
            EnsureHasTaggedEntry(table, resourceName, GivenTag);
            EnsureHasTaggedEntry(table, resourceName, SurnameTag);
        }
    }

    /// <summary>
    /// Ensures <paramref name="table"/> has at least one entry tagged <paramref name="tag"/>
    /// (matched case-insensitively, mirroring <c>NameGenerator.FilterByTag</c>). A "names"-category
    /// table with no "given" or no "surname" entries would otherwise only fail deep inside
    /// <c>NameGenerator.GenerateFrom</c>, and only when that specific table happens to be picked.
    /// </summary>
    private static void EnsureHasTaggedEntry(GeneratorTable table, string resourceName, string tag)
    {
        var hasTaggedEntry = table.Entries.Any(entry => entry.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase));
        if (!hasTaggedEntry)
        {
            throw new GeneratorTableLoadException(
                $"Generator table '{resourceName}' (id '{table.Id}') is a 'names' category table but has no entry tagged '{tag}'.");
        }
    }
}