using GmToolkit.Core.Models;
using GmToolkit.Core.Repositories;
using GmToolkit.Data.Mapping;
using GmToolkit.Data.Rows;

namespace GmToolkit.Data.Repositories;

/// <remarks>
/// CancellationToken parameters exist for API consistency but aren't passed through to
/// sqlite-net-pcl — its async API has no CancellationToken overloads, so an in-flight query
/// can't actually be cancelled with this library.
/// </remarks>
public class CampaignRepository(GmToolkitDatabase database) : ICampaignRepository
{
    public async Task<Campaign?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await database.Connection.FindAsync<CampaignRow>(id);
        return row is null ? null : await ToModelAsync(row);
    }

    public async Task<IReadOnlyList<Campaign>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await database.Connection.Table<CampaignRow>().ToListAsync();
        var campaigns = new List<Campaign>(rows.Count);
        foreach (var row in rows)
        {
            campaigns.Add(await ToModelAsync(row));
        }

        return campaigns;
    }

    public Task AddAsync(Campaign campaign, CancellationToken cancellationToken = default) =>
        database.Connection.InsertAsync(CampaignMapper.ToRow(campaign));

    public Task UpdateAsync(Campaign campaign, CancellationToken cancellationToken = default) =>
        database.Connection.UpdateAsync(CampaignMapper.ToRow(campaign));

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Explicit cascade — sqlite-net-pcl's attribute-driven schema creation doesn't emit
        // FOREIGN KEY/ON DELETE CASCADE clauses (see #10's notes).
        await database.Connection.ExecuteAsync("DELETE FROM PlayerCharacters WHERE CampaignId = ?", id);
        await database.Connection.ExecuteAsync("DELETE FROM Npcs WHERE CampaignId = ?", id);
        await database.Connection.ExecuteAsync("DELETE FROM Campaigns WHERE Id = ?", id);
    }

    private async Task<Campaign> ToModelAsync(CampaignRow row)
    {
        var pcRows = await database.Connection.Table<PlayerCharacterRow>()
            .Where(p => p.CampaignId == row.Id)
            .ToListAsync();
        var npcRows = await database.Connection.Table<NpcRow>()
            .Where(n => n.CampaignId == row.Id)
            .ToListAsync();

        return CampaignMapper.ToModel(
            row,
            pcRows.Select(PlayerCharacterMapper.ToModel).ToList(),
            npcRows.Select(NpcMapper.ToModel).ToList());
    }
}