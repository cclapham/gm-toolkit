using System.Globalization;
using System.Text;

using GmToolkit.Core.Import;

namespace GmToolkit.Core.Export;

/// <summary>
/// Flattens a <see cref="CampaignExportDto"/>'s player characters to CSV (issue #131's "CSV
/// (characters only)" format option) -- a spreadsheet-friendly view of the same data
/// <see cref="CampaignExportJsonContext"/> already round-trips at full fidelity, for a GM who just
/// wants to open a roster in Excel/Sheets rather than re-import it.
/// </summary>
/// <remarks>
/// <b>No fixed STR/DEX/CON/INT/WIS/CHA/HP/AC columns.</b> <see cref="Models.PlayerCharacter.Stats"/>
/// is a system-agnostic <see cref="Dictionary{TKey,TValue}"/> the GM (or an attached
/// <see cref="Systems.CharacterSystem"/>) defines the keys for -- baking in a fixed set of
/// D&amp;D-shaped ability-score columns would silently misrepresent (or simply drop) every other
/// system's data (Call of Cthulhu's SAN/Idea/Luck, Blades in the Dark's stress/trauma, any
/// homebrew system's own keys), which is exactly the "system-agnostic, zero code changes" guarantee
/// this app's architecture already commits to elsewhere (see <see cref="Models.PlayerCharacter.Stats"/>'s
/// own remarks). Instead, the stat columns are the union of every key actually used by at least one
/// character in <paramref name="dto"/> (see <see cref="Export"/>), in a stable (ordinal) sort so two
/// exports of the same campaign always produce byte-identical column headers.
/// </remarks>
public static class CampaignCsvExporter
{
    private static readonly string[] FixedColumns = ["Name", "Player", "Ancestry", "Class", "Level", "Notes"];

    /// <summary>
    /// Builds the CSV text (CRLF line endings, RFC 4180 quoting) for every
    /// <see cref="CampaignExportDto.PlayerCharacters"/> entry in <paramref name="dto"/>. NPCs are
    /// out of scope -- issue #131 explicitly limits the CSV format to characters; a GM who wants
    /// NPCs too has the full-fidelity JSON export for that.
    /// </summary>
    public static string Export(CampaignExportDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var statKeys = dto.PlayerCharacters
            .SelectMany(pc => pc.Stats.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        var builder = new StringBuilder();
        WriteRow(builder, FixedColumns.Concat(statKeys));

        foreach (var pc in dto.PlayerCharacters)
        {
            var fields = new List<string>(FixedColumns.Length + statKeys.Count)
            {
                pc.CharacterName,
                pc.PlayerName,
                pc.Ancestry,
                pc.Class,
                pc.Level.ToString(CultureInfo.InvariantCulture),
                pc.Notes,
            };
            fields.AddRange(statKeys.Select(key => pc.Stats.GetValueOrDefault(key, string.Empty)));

            WriteRow(builder, fields);
        }

        return builder.ToString();
    }

    private static void WriteRow(StringBuilder builder, IEnumerable<string> fields)
    {
        builder.AppendJoin(',', fields.Select(EscapeField));
        builder.Append("\r\n");
    }

    /// <summary>RFC 4180 quoting: a field containing a comma, quote, or line break is wrapped in
    /// double quotes with any embedded quote doubled; every other field is written as-is.</summary>
    private static string EscapeField(string value)
    {
        if (value.IndexOfAny([',', '"', '\n', '\r']) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}