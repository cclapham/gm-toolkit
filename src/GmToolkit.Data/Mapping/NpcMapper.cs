using System.Diagnostics;
using System.Text.Json;

using GmToolkit.Core.Models;
using GmToolkit.Data.Rows;

namespace GmToolkit.Data.Mapping;

/// <summary>
/// Maps between the persisted <see cref="NpcRow"/> shape and the domain <see cref="Npc"/> model.
/// </summary>
/// <remarks>
/// <see cref="DeserializeStats"/> is deliberately tolerant of a single row's malformed
/// <see cref="NpcRow.StatsJson"/>: <see cref="Repositories.NpcRepository.GetByCampaignAsync"/>
/// maps every row for a campaign in one <c>Select</c>, so an unhandled
/// <see cref="JsonException"/> from one bad row would otherwise fail that whole call and make
/// every NPC in the campaign inaccessible, not just the corrupted one -- see this method's
/// remarks for why it can't be worth risking that.
/// </remarks>
internal static class NpcMapper
{
    public static Npc ToModel(NpcRow row) => new()
    {
        Id = row.Id,
        CampaignId = row.CampaignId,
        Name = row.Name,
        Role = row.Role,
        Faction = row.Faction,
        Location = row.Location,
        Appearance = row.Appearance,
        Mannerism = row.Mannerism,
        Motivation = row.Motivation,
        Secret = row.Secret,
        Notes = row.Notes,
        KnownToPlayers = row.KnownToPlayers,
        CreatedUtc = row.CreatedUtc,
        WasGenerated = row.WasGenerated,
        Stats = DeserializeStats(row.Id, row.Name, row.StatsJson),
    };

    public static NpcRow ToRow(Npc model) => new()
    {
        Id = model.Id,
        CampaignId = model.CampaignId,
        Name = model.Name,
        Role = model.Role,
        Faction = model.Faction,
        Location = model.Location,
        Appearance = model.Appearance,
        Mannerism = model.Mannerism,
        Motivation = model.Motivation,
        Secret = model.Secret,
        Notes = model.Notes,
        KnownToPlayers = model.KnownToPlayers,
        CreatedUtc = model.CreatedUtc,
        WasGenerated = model.WasGenerated,
        StatsJson = JsonSerializer.Serialize(model.Stats),
    };

    /// <summary>
    /// Deserializes a row's <see cref="NpcRow.StatsJson"/>, tolerating a single malformed row
    /// rather than letting it fail every NPC in the campaign (see this type's remarks). A row
    /// whose <c>StatsJson</c> isn't valid JSON -- e.g. hand-edited directly in the database file,
    /// or truncated by some other failure -- logs a warning identifying the offending NPC (by id
    /// and name, so a GM/developer can find and manually fix that specific row) and is treated as
    /// having no stats, exactly like a brand-new NPC, rather than propagating the
    /// <see cref="JsonException"/> up through <see cref="ToModel"/> and poisoning the whole
    /// campaign's NPC list.
    /// </summary>
    private static Dictionary<string, string> DeserializeStats(Guid npcId, string npcName, string? statsJson)
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
            LogMalformedStats(npcId, npcName, ex);
            return [];
        }
    }

    [Conditional("DEBUG")]
    private static void LogMalformedStats(Guid npcId, string npcName, JsonException ex)
    {
        Debug.WriteLine(
            $"NpcMapper: Npc '{npcName}' ({npcId}) has malformed StatsJson and was loaded with " +
            $"empty stats instead. Original error: {ex.Message}");
    }
}