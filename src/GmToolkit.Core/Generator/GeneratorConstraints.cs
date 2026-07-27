namespace GmToolkit.Core.Generator;

/// <summary>
/// Optional constraints a caller can supply when generating an NPC field (issue #27). Scoped
/// tightly to the two constraints the issue names — a requested name culture and a requested
/// occupation category tag — rather than a fully generic "required tag per field" system that
/// tries to cover all six <see cref="NpcField"/> values. Every field other than
/// <see cref="NpcField.Name"/>/<see cref="NpcField.Role"/> simply ignores this type today; a future
/// issue that wants to constrain a third field can add a third property here without this becoming
/// a generalized field→tag dictionary.
/// </summary>
/// <remarks>
/// A single shared record (rather than one constraint type per field) because #27's own examples —
/// name culture, occupation category — are both "an optional string filter, matched
/// case-insensitively, with a documented fallback when nothing matches" and a caller assembling
/// constraints for a whole NPC only ever wants to set a subset of these at once (e.g. only
/// <see cref="NameCulture"/>, leaving <see cref="OccupationCategory"/> unset).
/// </remarks>
public sealed record GeneratorConstraints
{
    /// <summary>No constraints — every field is generated exactly as issue #26 already did.</summary>
    public static readonly GeneratorConstraints None = new();

    /// <summary>
    /// Requested name culture, matched case-insensitively against <see cref="GeneratorTable.Culture"/>
    /// by <see cref="NameGenerator.GenerateWithNotice"/>. Null means "no preference" (a culture is
    /// chosen at random, as before #27). Only consulted when generating <see cref="NpcField.Name"/>.
    /// </summary>
    public string? NameCulture { get; init; }

    /// <summary>
    /// Requested occupation category tag, matched case-insensitively against
    /// <see cref="GeneratorTableEntry.Tags"/> on the "occupation" table's entries by
    /// <see cref="TableGenerator.GenerateWithNotice"/>. Null means "no preference". Only consulted
    /// when generating <see cref="NpcField.Role"/>.
    /// </summary>
    public string? OccupationCategory { get; init; }
}