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
}