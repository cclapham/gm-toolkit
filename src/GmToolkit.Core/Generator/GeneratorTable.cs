namespace GmToolkit.Core.Generator;

/// <summary>
/// A named, weighted table of <see cref="GeneratorTableEntry"/> values loaded from an embedded
/// JSON resource under <c>Resources/GeneratorTables/</c>. This type only describes the loaded
/// data; the weighted-random-pick logic that consumes it is issue #26, not implemented here.
/// </summary>
public sealed class GeneratorTable
{
    /// <summary>Stable identifier for the table, e.g. "names-highland" or "occupation".</summary>
    public required string Id { get; init; }

    /// <summary>The kind of table, e.g. "names", "appearance", "occupation".</summary>
    public required string Category { get; init; }

    /// <summary>
    /// For tables in the "names" category, the naming culture this table represents (e.g.
    /// "highland", "coastal"). Null for non-names tables.
    /// </summary>
    public string? Culture { get; init; }

    /// <summary>Optional human-readable description of the table's content.</summary>
    public string? Description { get; init; }

    /// <summary>The weighted entries in this table. Must contain at least one entry.</summary>
    public required IReadOnlyList<GeneratorTableEntry> Entries { get; init; }
}