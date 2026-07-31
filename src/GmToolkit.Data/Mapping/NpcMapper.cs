using System.Text.Json;

using GmToolkit.Core.Models;
using GmToolkit.Data.Rows;

namespace GmToolkit.Data.Mapping;

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
        Stats = DeserializeStats(row.StatsJson),
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

    private static Dictionary<string, string> DeserializeStats(string? statsJson)
    {
        if (string.IsNullOrEmpty(statsJson))
        {
            return [];
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(statsJson) ?? [];
    }
}