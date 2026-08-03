using GmToolkit.Core.Models;

namespace GmToolkit.Core.Import;

/// <summary>Maps between <see cref="PlayerCharacter"/> and its export DTO.</summary>
public static class PlayerCharacterExportMapper
{
    public static PlayerCharacterExportDto ToDto(PlayerCharacter playerCharacter)
    {
        ArgumentNullException.ThrowIfNull(playerCharacter);

        return new PlayerCharacterExportDto
        {
            CharacterName = playerCharacter.CharacterName,
            PlayerName = playerCharacter.PlayerName,
            Ancestry = playerCharacter.Ancestry,
            Class = playerCharacter.Class,
            Level = playerCharacter.Level,
            Notes = playerCharacter.Notes,
            Stats = new Dictionary<string, string>(playerCharacter.Stats),
        };
    }

    /// <summary>
    /// Builds a <see cref="PlayerCharacter"/> from an import DTO under <paramref name="campaignId"/>.
    /// Mints a fresh <see cref="PlayerCharacter.Id"/> unless <paramref name="id"/> is supplied —
    /// callers doing a conflict-resolving overwrite of an already-existing character (see
    /// <c>GmToolkit.Data.Repositories.PlayerCharacterRepository.ImportCharactersAsync</c>) pass the
    /// existing row's id so the update replaces it in place rather than creating a duplicate.
    /// </summary>
    /// <remarks>
    /// Assumes <paramref name="dto"/> already passed <see cref="ImportValidator.ValidatePlayerCharacter"/>
    /// — an invalid <see cref="PlayerCharacterExportDto.CharacterName"/> still throws here, via
    /// <see cref="PlayerCharacter.CharacterName"/>'s own validating setter.
    /// </remarks>
    public static PlayerCharacter ToModel(PlayerCharacterExportDto dto, Guid campaignId, Guid? id = null)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new PlayerCharacter
        {
            Id = id ?? Guid.NewGuid(),
            CampaignId = campaignId,
            CharacterName = dto.CharacterName,
            PlayerName = dto.PlayerName,
            Ancestry = dto.Ancestry,
            Class = dto.Class,
            Level = dto.Level,
            Notes = dto.Notes,
            Stats = new Dictionary<string, string>(dto.Stats),
        };
    }
}