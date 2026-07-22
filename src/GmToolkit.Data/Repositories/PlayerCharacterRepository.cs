using GmToolkit.Core.Models;
using GmToolkit.Core.Repositories;
using GmToolkit.Data.Mapping;
using GmToolkit.Data.Rows;

namespace GmToolkit.Data.Repositories;

public class PlayerCharacterRepository(GmToolkitDatabase database) : IPlayerCharacterRepository
{
    public async Task<PlayerCharacter?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await database.Connection.FindAsync<PlayerCharacterRow>(id);
        return row is null ? null : PlayerCharacterMapper.ToModel(row);
    }

    public async Task<IReadOnlyList<PlayerCharacter>> GetByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        var rows = await database.Connection.Table<PlayerCharacterRow>()
            .Where(p => p.CampaignId == campaignId)
            .ToListAsync();

        return rows.Select(PlayerCharacterMapper.ToModel).ToList();
    }

    public Task AddAsync(PlayerCharacter playerCharacter, CancellationToken cancellationToken = default) =>
        database.Connection.InsertAsync(PlayerCharacterMapper.ToRow(playerCharacter));

    public Task UpdateAsync(PlayerCharacter playerCharacter, CancellationToken cancellationToken = default) =>
        database.Connection.UpdateAsync(PlayerCharacterMapper.ToRow(playerCharacter));

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        database.Connection.ExecuteAsync("DELETE FROM PlayerCharacters WHERE Id = ?", id);
}