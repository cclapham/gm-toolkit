using System.Diagnostics;
using System.Text.Json;

using GmToolkit.Core.Models;
using GmToolkit.Data.Rows;

namespace GmToolkit.Data.Mapping;

/// <summary>
/// Maps between the persisted <see cref="PlayerCharacterRow"/> shape and the domain
/// <see cref="PlayerCharacter"/> model.
/// </summary>
/// <remarks>
/// <see cref="DeserializeStats"/> is deliberately tolerant of a single row's malformed
/// <see cref="PlayerCharacterRow.StatsJson"/>: <see cref="Repositories.PlayerCharacterRepository.GetByCampaignAsync"/>
/// maps every row for a campaign in one <c>Select</c>, so an unhandled <see cref="JsonException"/>
/// from one bad row would otherwise fail that whole call and make every PC in the campaign
/// inaccessible, not just the corrupted one -- see <see cref="Mapping.NpcMapper"/>'s remarks, which
/// this mirrors.
/// </remarks>
internal static class PlayerCharacterMapper
{
    public static PlayerCharacter ToModel(PlayerCharacterRow row)
    {
        var (stats, malformedStatsJson) = DeserializeStats(row.Id, row.CharacterName, row.StatsJson);

        return new()
        {
            Id = row.Id,
            CampaignId = row.CampaignId,
            CharacterName = row.CharacterName,
            PlayerName = row.PlayerName,
            Ancestry = row.Ancestry,
            Class = row.Class,
            Level = row.Level,
            Notes = row.Notes,
            Stats = stats,
            MalformedStatsJson = malformedStatsJson,
        };
    }

    public static PlayerCharacterRow ToRow(PlayerCharacter model) => new()
    {
        Id = model.Id,
        CampaignId = model.CampaignId,
        CharacterName = model.CharacterName,
        PlayerName = model.PlayerName,
        Ancestry = model.Ancestry,
        Class = model.Class,
        Level = model.Level,
        Notes = model.Notes,
        // HasMalformedStats means Stats is just an empty placeholder (see that property's
        // remarks), not this PC's real data -- serializing it would permanently overwrite the
        // original, still-possibly-recoverable bytes the moment this PC is next saved, even if
        // nothing about its stats was ever touched. Write the original bytes back verbatim instead.
        StatsJson = model.HasMalformedStats ? model.MalformedStatsJson! : JsonSerializer.Serialize(model.Stats),
    };

    /// <summary>
    /// Deserializes a row's <see cref="PlayerCharacterRow.StatsJson"/>, tolerating a single
    /// malformed row rather than letting it fail every PC in the campaign (see this type's
    /// remarks). A row whose <c>StatsJson</c> isn't valid JSON -- e.g. hand-edited directly in the
    /// database file, or truncated by some other failure -- logs a warning identifying the
    /// offending PC (by id and name, so a GM/developer can find and manually fix that specific row)
    /// and is treated as having no stats, exactly like a brand-new PC, rather than propagating the
    /// <see cref="JsonException"/> up through <see cref="ToModel"/> and poisoning the whole
    /// campaign's PC list.
    /// </summary>
    /// <returns>
    /// The parsed stats (empty on failure) and, on failure, the original malformed JSON text so
    /// <see cref="ToRow"/> can write it back unchanged instead of destroying it -- <c>null</c> on
    /// success, since there is nothing to preserve.
    /// </returns>
    private static (Dictionary<string, string> Stats, string? MalformedStatsJson) DeserializeStats(
        Guid characterId, string characterName, string? statsJson)
    {
        if (string.IsNullOrEmpty(statsJson))
        {
            return ([], null);
        }

        try
        {
            return (JsonSerializer.Deserialize<Dictionary<string, string>>(statsJson) ?? [], null);
        }
        catch (JsonException ex)
        {
            LogMalformedStats(characterId, characterName, statsJson, ex);
            return ([], statsJson);
        }
    }

    /// <summary>
    /// Cap on how much of the offending <c>StatsJson</c> gets logged. The full text is still
    /// preserved verbatim in <see cref="PlayerCharacter.MalformedStatsJson"/> for the write-back
    /// path -- this only bounds what one bad row can dump into the trace log on every load.
    /// </summary>
    private const int MaxLoggedStatsJsonLength = 200;

    private static void LogMalformedStats(Guid characterId, string characterName, string originalStatsJson, JsonException ex)
    {
        Trace.WriteLine(
            $"PlayerCharacterMapper: PlayerCharacter '{characterName}' ({characterId}) has malformed " +
            $"StatsJson and was loaded with empty stats instead. Original error: {ex.Message}. " +
            $"Original StatsJson (preserved verbatim on next save; truncated here to " +
            $"{MaxLoggedStatsJsonLength} chars): {Truncate(originalStatsJson, MaxLoggedStatsJsonLength)}");
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength), "...");
}