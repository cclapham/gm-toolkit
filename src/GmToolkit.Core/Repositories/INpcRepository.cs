using GmToolkit.Core.Import;
using GmToolkit.Core.Models;

namespace GmToolkit.Core.Repositories;

public interface INpcRepository
{
    Task<Npc?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Npc>> GetByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default);

    Task AddAsync(Npc npc, CancellationToken cancellationToken = default);

    Task UpdateAsync(Npc npc, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk-imports <paramref name="dtos"/> into the already-existing campaign
    /// <paramref name="campaignId"/>, best-effort per entry — mirrors
    /// <see cref="IPlayerCharacterRepository.ImportCharactersAsync"/>'s conflict-resolution and
    /// validation rules, matched against <see cref="Import.NpcExportDto.Name"/> instead of
    /// <c>CharacterName</c>.
    /// </summary>
    Task<BulkImportResult<Npc>> ImportCharactersAsync(
        Guid campaignId, IReadOnlyList<NpcExportDto> dtos, bool overwrite, CancellationToken cancellationToken = default);
}