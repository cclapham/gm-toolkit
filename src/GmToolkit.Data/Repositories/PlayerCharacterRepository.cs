using GmToolkit.Core.Import;
using GmToolkit.Core.Models;
using GmToolkit.Core.Repositories;
using GmToolkit.Data.Mapping;
using GmToolkit.Data.Rows;

namespace GmToolkit.Data.Repositories;

public class PlayerCharacterRepository(GmToolkitDatabase database) : IPlayerCharacterRepository
{
    public Task<PlayerCharacter?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        DatabaseExceptionTranslator.RunAsync(database, async () =>
        {
            var row = await database.Connection.FindAsync<PlayerCharacterRow>(id);
            return row is null ? null : PlayerCharacterMapper.ToModel(row);
        });

    public Task<IReadOnlyList<PlayerCharacter>> GetByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        DatabaseExceptionTranslator.RunAsync(database, async () =>
        {
            var rows = await database.Connection.Table<PlayerCharacterRow>()
                .Where(p => p.CampaignId == campaignId)
                .ToListAsync();

            return (IReadOnlyList<PlayerCharacter>)rows.Select(PlayerCharacterMapper.ToModel).ToList();
        });

    public Task AddAsync(PlayerCharacter playerCharacter, CancellationToken cancellationToken = default) =>
        DatabaseExceptionTranslator.RunAsync(database, () =>
            database.Connection.InsertAsync(PlayerCharacterMapper.ToRow(playerCharacter)));

    public Task UpdateAsync(PlayerCharacter playerCharacter, CancellationToken cancellationToken = default) =>
        DatabaseExceptionTranslator.RunAsync(database, () =>
            database.Connection.UpdateAsync(PlayerCharacterMapper.ToRow(playerCharacter)));

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        DatabaseExceptionTranslator.RunAsync(database, () =>
            database.Connection.ExecuteAsync("DELETE FROM PlayerCharacters WHERE Id = ?", id));

    /// <remarks>
    /// Best-effort per entry -- see <see cref="BulkImportResult{T}"/>'s remarks. Existing rows are
    /// looked up by <see cref="Rows.PlayerCharacterRow.CharacterName"/> once up front; nothing about
    /// this method is transactional across the whole batch (unlike
    /// <see cref="CampaignRepository.ImportCampaignAsync"/>'s all-or-nothing whole-campaign import),
    /// matching this method's "skip the bad ones, keep the rest" contract.
    /// </remarks>
    public Task<BulkImportResult<PlayerCharacter>> ImportCharactersAsync(
        Guid campaignId, IReadOnlyList<PlayerCharacterExportDto> dtos, bool overwrite, CancellationToken cancellationToken = default) =>
        DatabaseExceptionTranslator.RunAsync(database, async () =>
        {
            ArgumentNullException.ThrowIfNull(dtos);

            var existingRows = await database.Connection.Table<PlayerCharacterRow>()
                .Where(p => p.CampaignId == campaignId)
                .ToListAsync();
            var existingByName = new Dictionary<string, PlayerCharacterRow>(StringComparer.Ordinal);
            foreach (var row in existingRows)
            {
                existingByName[row.CharacterName] = row;
            }

            var imported = new List<PlayerCharacter>();
            var errors = new List<ImportItemError>();

            for (var index = 0; index < dtos.Count; index++)
            {
                var dto = dtos[index];
                var validation = ImportValidator.ValidatePlayerCharacter(dto);
                if (!validation.IsValid)
                {
                    errors.Add(new ImportItemError(index, dto.CharacterName, validation.Errors));
                    continue;
                }

                if (existingByName.TryGetValue(dto.CharacterName, out var existingRow))
                {
                    if (!overwrite)
                    {
                        errors.Add(new ImportItemError(
                            index, dto.CharacterName,
                            [$"A player character named '{dto.CharacterName}' already exists in this campaign."]));
                        continue;
                    }

                    var updated = PlayerCharacterExportMapper.ToModel(dto, campaignId, existingRow.Id);
                    await database.Connection.UpdateAsync(PlayerCharacterMapper.ToRow(updated));
                    imported.Add(updated);
                }
                else
                {
                    var created = PlayerCharacterExportMapper.ToModel(dto, campaignId);
                    await database.Connection.InsertAsync(PlayerCharacterMapper.ToRow(created));
                    imported.Add(created);
                }
            }

            return new BulkImportResult<PlayerCharacter>(imported, errors);
        });
}