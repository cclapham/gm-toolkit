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
    public static PlayerCharacter ToModel(PlayerCharacterRow row) => new()
    {
        Id = row.Id,
        CampaignId = row.CampaignId,
        CharacterName = row.CharacterName,
        PlayerName = row.PlayerName,
        Ancestry = row.Ancestry,
        Class = row.Class,
        Level = row.Level,
        Notes = row.Notes,
        Stats = DeserializeStats(row.Id, row.CharacterName, row.StatsJson),
    };

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
        StatsJson = JsonSerializer.Serialize(model.Stats),
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
    private static Dictionary<string, string> DeserializeStats(Guid characterId, string characterName, string? statsJson)
    {
        if (string.IsNullOrEmpty(statsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(statsJson) ?? [];
        }
        catch (JsonException ex)
        {
            LogMalformedStats(characterId, characterName, ex);
            return [];
        }
    }

    private static void LogMalformedStats(Guid characterId, string characterName, JsonException ex)
    {
        Trace.WriteLine(
            $"PlayerCharacterMapper: PlayerCharacter '{characterName}' ({characterId}) has malformed " +
            $"StatsJson and was loaded with empty stats instead. Original error: {ex.Message}");
    }
}