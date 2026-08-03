using GmToolkit.Core.Import;
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
    public Task<Campaign?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        DatabaseExceptionTranslator.RunAsync(database, async () =>
        {
            var row = await database.Connection.FindAsync<CampaignRow>(id);
            return row is null ? null : await ToModelAsync(row);
        });

    public Task<IReadOnlyList<Campaign>> GetAllAsync(CancellationToken cancellationToken = default) =>
        DatabaseExceptionTranslator.RunAsync(database, async () =>
        {
            var rows = await database.Connection.Table<CampaignRow>().ToListAsync();
            var campaigns = new List<Campaign>(rows.Count);
            foreach (var row in rows)
            {
                campaigns.Add(await ToModelAsync(row));
            }

            return (IReadOnlyList<Campaign>)campaigns;
        });

    public Task AddAsync(Campaign campaign, CancellationToken cancellationToken = default) =>
        DatabaseExceptionTranslator.RunAsync(database, () =>
            database.Connection.InsertAsync(CampaignMapper.ToRow(campaign)));

    public Task UpdateAsync(Campaign campaign, CancellationToken cancellationToken = default) =>
        DatabaseExceptionTranslator.RunAsync(database, () =>
            database.Connection.UpdateAsync(CampaignMapper.ToRow(campaign)));

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        DatabaseExceptionTranslator.RunAsync(database, async () =>
        {
            // Explicit cascade — sqlite-net-pcl's attribute-driven schema creation doesn't emit
            // FOREIGN KEY/ON DELETE CASCADE clauses (see #10's notes).
            await database.Connection.ExecuteAsync("DELETE FROM PlayerCharacters WHERE CampaignId = ?", id);
            await database.Connection.ExecuteAsync("DELETE FROM Npcs WHERE CampaignId = ?", id);
            await database.Connection.ExecuteAsync("DELETE FROM Campaigns WHERE Id = ?", id);
        });

    public Task<CampaignExportDto?> ExportCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        DatabaseExceptionTranslator.RunAsync(database, async () =>
        {
            var row = await database.Connection.FindAsync<CampaignRow>(campaignId);
            if (row is null)
            {
                return null;
            }

            var campaign = await ToModelAsync(row);
            return CampaignExportMapper.ToDto(campaign);
        });

    /// <remarks>
    /// All-or-nothing (see <see cref="ICampaignRepository.ImportCampaignAsync"/>'s remarks):
    /// <paramref name="dto"/> is fully validated, and any existing campaign of the same name is
    /// resolved, before the transaction that actually writes anything opens. Deleting an existing
    /// campaign (when <paramref name="overwrite"/> is <c>true</c>) and inserting the new one happen
    /// inside the same <see cref="SQLite.SQLiteAsyncConnection.RunInTransactionAsync"/> call, so a
    /// failure partway through can never leave the database with neither the old nor the new
    /// campaign.
    /// </remarks>
    public Task<CampaignImportResult> ImportCampaignAsync(CampaignExportDto dto, bool overwrite, CancellationToken cancellationToken = default) =>
        DatabaseExceptionTranslator.RunAsync(database, async () =>
        {
            ArgumentNullException.ThrowIfNull(dto);

            var validation = ImportValidator.ValidateCampaign(dto);
            if (!validation.IsValid)
            {
                return CampaignImportResult.Failure(validation);
            }

            var existing = await database.Connection.Table<CampaignRow>()
                .Where(c => c.Name == dto.Name)
                .FirstOrDefaultAsync();

            if (existing is not null && !overwrite)
            {
                return CampaignImportResult.Failure(
                    $"A campaign named '{dto.Name}' already exists. Re-import with overwrite to replace it.");
            }

            var campaign = CampaignExportMapper.ToModel(dto);
            var playerCharacters = dto.PlayerCharacters
                .Select(pc => PlayerCharacterExportMapper.ToModel(pc, campaign.Id))
                .ToList();
            var npcs = dto.Npcs
                .Select(npc => NpcExportMapper.ToModel(npc, campaign.Id))
                .ToList();

            await database.Connection.RunInTransactionAsync(connection =>
            {
                if (existing is not null)
                {
                    // Explicit cascade, matching DeleteAsync -- see its remarks.
                    connection.Execute("DELETE FROM PlayerCharacters WHERE CampaignId = ?", existing.Id);
                    connection.Execute("DELETE FROM Npcs WHERE CampaignId = ?", existing.Id);
                    connection.Execute("DELETE FROM Campaigns WHERE Id = ?", existing.Id);
                }

                connection.Insert(CampaignMapper.ToRow(campaign));
                foreach (var playerCharacter in playerCharacters)
                {
                    connection.Insert(PlayerCharacterMapper.ToRow(playerCharacter));
                }

                foreach (var npc in npcs)
                {
                    connection.Insert(NpcMapper.ToRow(npc));
                }
            });

            campaign.PlayerCharacters.AddRange(playerCharacters);
            campaign.Npcs.AddRange(npcs);
            return CampaignImportResult.Success(campaign);
        });

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