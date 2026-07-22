using System.Text.Json;

using GmToolkit.Core.Models;
using GmToolkit.Data.Rows;

namespace GmToolkit.Data.Mapping;

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
        Stats = DeserializeStats(row.StatsJson),
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

    private static Dictionary<string, string> DeserializeStats(string statsJson)
    {
        if (string.IsNullOrEmpty(statsJson))
        {
            return [];
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(statsJson) ?? [];
    }
}