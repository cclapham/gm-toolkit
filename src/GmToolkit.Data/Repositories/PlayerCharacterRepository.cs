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
}