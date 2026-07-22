using GmToolkit.Core.Models;

namespace GmToolkit.Core.Repositories;

public interface IPlayerCharacterRepository
{
    Task<PlayerCharacter?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlayerCharacter>> GetByCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default);

    Task AddAsync(PlayerCharacter playerCharacter, CancellationToken cancellationToken = default);

    Task UpdateAsync(PlayerCharacter playerCharacter, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}