using GmToolkit.Core.Import;
using GmToolkit.Core.Models;
using GmToolkit.Core.Repositories;
using GmToolkit.Data.Mapping;
using GmToolkit.Data.Rows;

namespace GmToolkit.Data.Repositories;

public class NpcRepository(GmToolkitDatabase database) : INpcRepository
{
    public Task<Npc?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        DatabaseExceptionTranslator.RunAsync(database, async () =>
        {
            var row = await database.Connection.FindAsync<NpcRow>(id);
            return row is null ? null : NpcMapper.ToModel(row);
        });

    public Task<IReadOnlyList<Npc>> GetByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
        DatabaseExceptionTranslator.RunAsync(database, async () =>
        {
            var rows = await database.Connection.Table<NpcRow>()
                .Where(n => n.CampaignId == campaignId)
                .ToListAsync();

            return (IReadOnlyList<Npc>)rows.Select(NpcMapper.ToModel).ToList();
        });

    public Task AddAsync(Npc npc, CancellationToken cancellationToken = default) =>
        DatabaseExceptionTranslator.RunAsync(database, () =>
            database.Connection.InsertAsync(NpcMapper.ToRow(npc)));

    public Task UpdateAsync(Npc npc, CancellationToken cancellationToken = default) =>
        DatabaseExceptionTranslator.RunAsync(database, () =>
            database.Connection.UpdateAsync(NpcMapper.ToRow(npc)));

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        DatabaseExceptionTranslator.RunAsync(database, () =>
            database.Connection.ExecuteAsync("DELETE FROM Npcs WHERE Id = ?", id));

    /// <remarks>
    /// Best-effort per entry -- mirrors <see cref="PlayerCharacterRepository.ImportCharactersAsync"/>'s
    /// remarks, matched against <see cref="Rows.NpcRow.Name"/> instead of <c>CharacterName</c>.
    /// </remarks>
    public Task<BulkImportResult<Npc>> ImportCharactersAsync(
        Guid campaignId, IReadOnlyList<NpcExportDto> dtos, bool overwrite, CancellationToken cancellationToken = default) =>
        DatabaseExceptionTranslator.RunAsync(database, async () =>
        {
            ArgumentNullException.ThrowIfNull(dtos);

            var existingRows = await database.Connection.Table<NpcRow>()
                .Where(n => n.CampaignId == campaignId)
                .ToListAsync();
            var existingByName = new Dictionary<string, NpcRow>(StringComparer.Ordinal);
            foreach (var row in existingRows)
            {
                existingByName[row.Name] = row;
            }

            var imported = new List<Npc>();
            var errors = new List<ImportItemError>();

            for (var index = 0; index < dtos.Count; index++)
            {
                var dto = dtos[index];
                var validation = ImportValidator.ValidateNpc(dto);
                if (!validation.IsValid)
                {
                    errors.Add(new ImportItemError(index, dto.Name, validation.Errors));
                    continue;
                }

                if (existingByName.TryGetValue(dto.Name, out var existingRow))
                {
                    if (!overwrite)
                    {
                        errors.Add(new ImportItemError(
                            index, dto.Name, [$"An NPC named '{dto.Name}' already exists in this campaign."]));
                        continue;
                    }

                    var updated = NpcExportMapper.ToModel(dto, campaignId, existingRow.Id);
                    await database.Connection.UpdateAsync(NpcMapper.ToRow(updated));
                    imported.Add(updated);
                }
                else
                {
                    var created = NpcExportMapper.ToModel(dto, campaignId);
                    await database.Connection.InsertAsync(NpcMapper.ToRow(created));
                    imported.Add(created);
                }
            }

            return new BulkImportResult<Npc>(imported, errors);
        });
}