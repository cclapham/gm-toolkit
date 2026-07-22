using GmToolkit.Core.Models;
using GmToolkit.Data.Rows;

namespace GmToolkit.Data.Mapping;

internal static class CampaignMapper
{
    /// <summary>Maps the flat row plus already-loaded children into the aggregate model.</summary>
    public static Campaign ToModel(CampaignRow row, List<PlayerCharacter> playerCharacters, List<Npc> npcs) => new()
    {
        Id = row.Id,
        Name = row.Name,
        GameSystem = row.GameSystem,
        Description = row.Description,
        CreatedUtc = row.CreatedUtc,
        LastOpenedUtc = row.LastOpenedUtc,
        PlayerCharacters = playerCharacters,
        Npcs = npcs,
    };

    public static CampaignRow ToRow(Campaign campaign) => new()
    {
        Id = campaign.Id,
        Name = campaign.Name,
        GameSystem = campaign.GameSystem,
        Description = campaign.Description,
        CreatedUtc = campaign.CreatedUtc,
        LastOpenedUtc = campaign.LastOpenedUtc,
    };
}