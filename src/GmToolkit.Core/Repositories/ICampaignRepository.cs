using GmToolkit.Core.Import;
using GmToolkit.Core.Models;

namespace GmToolkit.Core.Repositories;

public interface ICampaignRepository
{
    Task<Campaign?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Campaign>> GetAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Campaign campaign, CancellationToken cancellationToken = default);

    Task UpdateAsync(Campaign campaign, CancellationToken cancellationToken = default);

    /// <summary>Also deletes the campaign's PlayerCharacters and Npcs.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds a full-fidelity <see cref="CampaignExportDto"/> (campaign metadata plus every player
    /// character and NPC) for <paramref name="campaignId"/>, or <c>null</c> if no campaign exists
    /// with that id — mirrors <see cref="GetAsync"/>'s not-found convention.
    /// </summary>
    Task<CampaignExportDto?> ExportCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports a whole campaign export as a brand-new campaign (fresh ids throughout — see
    /// <see cref="CampaignExportDto"/>'s remarks). All-or-nothing: <paramref name="dto"/> is
    /// validated first, and if an existing campaign shares <paramref name="dto"/>'s
    /// <see cref="CampaignExportDto.Name"/>, <paramref name="overwrite"/> decides whether that
    /// existing campaign (and its player characters/NPCs) is replaced or the import is refused —
    /// either way, nothing is written unless the whole operation can succeed.
    /// </summary>
    Task<CampaignImportResult> ImportCampaignAsync(CampaignExportDto dto, bool overwrite, CancellationToken cancellationToken = default);
}