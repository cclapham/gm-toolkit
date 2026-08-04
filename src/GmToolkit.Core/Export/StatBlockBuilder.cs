using GmToolkit.Core.Systems;

namespace GmToolkit.Core.Export;

/// <summary>
/// Renders a system-agnostic <c>Stats</c> bag (<see cref="Models.PlayerCharacter.Stats"/>/
/// <see cref="Models.Npc.Stats"/>) to <see cref="PdfBlock"/>s, shared by
/// <see cref="PlayerCharacterPdfExporter"/> and <see cref="CampaignSummaryPdfExporter"/> (its NPC
/// stat-block section) so the two exporters can't drift on how a field set is presented.
/// </summary>
public static class StatBlockBuilder
{
    /// <summary>
    /// One line per <paramref name="fields"/> entry (in schema order, using its
    /// <see cref="StatFieldDefinition.Label"/>) that <paramref name="stats"/> actually has a
    /// non-blank value for -- a field the schema defines but this particular character/NPC never
    /// filled in is simply omitted, not rendered as an empty line. A <c>repeating-group</c> field
    /// (skills, spells, equipment, ...) expands to a sub-heading plus one line per row via
    /// <see cref="RepeatingGroupCodec.Deserialize"/>, each row's own values joined
    /// <c>"Label: value"</c>-style in that row's item-field order.
    /// </summary>
    /// <remarks>
    /// When <paramref name="fields"/> is <c>null</c> or empty (no <see cref="CharacterSystem"/>
    /// attached, or one whose pack is no longer installed -- see
    /// <see cref="PlayerCharacterPdfExporter"/>'s remarks), falls back to every
    /// <paramref name="stats"/> entry sorted by key, unlabeled -- still readable, just without a
    /// schema to name or order them.
    /// </remarks>
    public static IReadOnlyList<PdfBlock> Build(IReadOnlyDictionary<string, string> stats, IReadOnlyList<StatFieldDefinition>? fields)
    {
        ArgumentNullException.ThrowIfNull(stats);

        if (fields is null || fields.Count == 0)
        {
            return stats
                .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
                .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
                .Select(kvp => new PdfBlock($"{kvp.Key}: {kvp.Value}", PdfBlockStyle.Body))
                .ToList();
        }

        var blocks = new List<PdfBlock>();
        foreach (var field in fields)
        {
            if (field.Type == StatFieldTypes.RepeatingGroup)
            {
                AppendRepeatingGroup(blocks, field, stats);
                continue;
            }

            if (stats.TryGetValue(field.Key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                blocks.Add(new PdfBlock($"{field.Label}: {value}", PdfBlockStyle.Body));
            }
        }

        return blocks;
    }

    private static void AppendRepeatingGroup(List<PdfBlock> blocks, StatFieldDefinition field, IReadOnlyDictionary<string, string> stats)
    {
        if (!stats.TryGetValue(field.Key, out var json))
        {
            return;
        }

        var rows = RepeatingGroupCodec.Deserialize(json);
        if (rows.Count == 0)
        {
            return;
        }

        blocks.Add(new PdfBlock(field.Label, PdfBlockStyle.SubHeading));

        var itemFields = field.ItemFields ?? [];
        foreach (var row in rows)
        {
            var parts = itemFields
                .Where(itemField => row.TryGetValue(itemField.Key, out var value) && !string.IsNullOrWhiteSpace(value))
                .Select(itemField => $"{itemField.Label}: {row[itemField.Key]}");

            var line = string.Join("  |  ", parts);
            if (line.Length > 0)
            {
                blocks.Add(new PdfBlock(line, PdfBlockStyle.Body));
            }
        }
    }
}