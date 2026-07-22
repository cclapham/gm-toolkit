using GmToolkit.Core.Models;
using GmToolkit.Core.Repositories;
using GmToolkit.Data.Mapping;
using GmToolkit.Data.Rows;

namespace GmToolkit.Data.Repositories;

public class NpcRepository(GmToolkitDatabase database) : INpcRepository
{
    public async Task<Npc?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await database.Connection.FindAsync<NpcRow>(id);
        return row is null ? null : NpcMapper.ToModel(row);
    }

    public async Task<IReadOnlyList<Npc>> GetByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default)
    {
        var rows = await database.Connection.Table<NpcRow>()
            .Where(n => n.CampaignId == campaignId)
            .ToListAsync();

        return rows.Select(NpcMapper.ToModel).ToList();
    }

    public Task AddAsync(Npc npc, CancellationToken cancellationToken = default) =>
        database.Connection.InsertAsync(NpcMapper.ToRow(npc));

    public Task UpdateAsync(Npc npc, CancellationToken cancellationToken = default) =>
        database.Connection.UpdateAsync(NpcMapper.ToRow(npc));

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        database.Connection.ExecuteAsync("DELETE FROM Npcs WHERE Id = ?", id);
}