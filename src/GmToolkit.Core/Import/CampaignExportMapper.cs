using GmToolkit.Core.Models;

namespace GmToolkit.Core.Import;

/// <summary>
/// Maps between <see cref="Campaign"/> and its export DTO. Mirrors
/// <c>GmToolkit.Data.Mapping.CampaignMapper</c>'s row-mapping convention, one layer up.
/// </summary>
public static class CampaignExportMapper
{
    /// <summary>Builds an export DTO from a fully-loaded campaign aggregate (PCs/NPCs included).</summary>
    public static CampaignExportDto ToDto(Campaign campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        return new CampaignExportDto
        {
            Name = campaign.Name,
            GameSystem = campaign.GameSystem,
            CharacterSystemId = campaign.CharacterSystemId,
            Description = campaign.Description,
            ExportedUtc = DateTime.UtcNow,
            PlayerCharacters = campaign.PlayerCharacters.Select(PlayerCharacterExportMapper.ToDto).ToList(),
            Npcs = campaign.Npcs.Select(NpcExportMapper.ToDto).ToList(),
        };
    }

    /// <summary>
    /// Builds a brand-new <see cref="Campaign"/> (fresh <see cref="Campaign.Id"/>,
    /// <see cref="Campaign.CreatedUtc"/>, <see cref="Campaign.LastOpenedUtc"/>) from an import DTO —
    /// never reuses an id from a previous import or export, per <see cref="CampaignExportDto"/>'s
    /// remarks. Does not populate <see cref="Campaign.PlayerCharacters"/>/<see cref="Campaign.Npcs"/>;
    /// map those separately with <see cref="PlayerCharacterExportMapper.ToModel"/>/
    /// <see cref="NpcExportMapper.ToModel"/> once this campaign's new <see cref="Campaign.Id"/> is
    /// known.
    /// </summary>
    /// <remarks>
    /// Assumes <paramref name="dto"/> already passed <see cref="ImportValidator.ValidateCampaign"/> —
    /// an invalid <see cref="CampaignExportDto.Name"/> (empty, too long) still throws here, via
    /// <see cref="Campaign.Name"/>'s own validating setter, exactly like any other invalid
    /// <see cref="Campaign"/> construction.
    /// </remarks>
    public static Campaign ToModel(CampaignExportDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new Campaign
        {
            Name = dto.Name,
            GameSystem = dto.GameSystem,
            CharacterSystemId = dto.CharacterSystemId,
            Description = dto.Description,
        };
    }
}