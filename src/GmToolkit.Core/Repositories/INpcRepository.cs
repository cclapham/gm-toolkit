using GmToolkit.Core.Models;

namespace GmToolkit.Core.Repositories;

public interface INpcRepository
{
    Task<Npc?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Npc>> GetByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default);

    Task AddAsync(Npc npc, CancellationToken cancellationToken = default);

    Task UpdateAsync(Npc npc, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}