using GmToolkit.Core.Import;
using GmToolkit.Core.Models;

namespace GmToolkit.Core.Repositories;

public interface IPlayerCharacterRepository
{
    Task<PlayerCharacter?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlayerCharacter>> GetByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default);

    Task AddAsync(PlayerCharacter playerCharacter, CancellationToken cancellationToken = default);

    Task UpdateAsync(PlayerCharacter playerCharacter, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk-imports <paramref name="dtos"/> into the already-existing campaign
    /// <paramref name="campaignId"/>, best-effort per entry (see <see cref="BulkImportResult{T}"/>'s
    /// remarks): an entry whose <see cref="Import.PlayerCharacterExportDto.CharacterName"/> matches an
    /// existing character in this campaign is overwritten in place when <paramref name="overwrite"/>
    /// is <c>true</c>, or skipped (reported in the result's errors) when <c>false</c>; an entry that
    /// fails <see cref="ImportValidator.ValidatePlayerCharacter"/> is always skipped regardless of
    /// <paramref name="overwrite"/>.
    /// </summary>
    Task<BulkImportResult<PlayerCharacter>> ImportCharactersAsync(
        Guid campaignId, IReadOnlyList<PlayerCharacterExportDto> dtos, bool overwrite, CancellationToken cancellationToken = default);
}