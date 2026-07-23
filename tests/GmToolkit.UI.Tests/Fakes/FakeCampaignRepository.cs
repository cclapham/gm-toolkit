using GmToolkit.Core.Models;
using GmToolkit.Core.Repositories;

namespace GmToolkit.UI.Tests.Fakes;

/// <summary>In-memory <see cref="ICampaignRepository"/> for constructing a real
/// <see cref="GmToolkit.Core.Services.ActiveCampaignContext"/> in tests without SQLite.</summary>
internal sealed class FakeCampaignRepository(params Campaign[] campaigns) : ICampaignRepository
{
    private readonly List<Campaign> _campaigns = [.. campaigns];

    public List<Campaign> UpdatedCampaigns { get; } = [];

    public Task<Campaign?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_campaigns.FirstOrDefault(c => c.Id == id));

    public Task<IReadOnlyList<Campaign>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Campaign>>([.. _campaigns]);

    public Task AddAsync(Campaign campaign, CancellationToken cancellationToken = default)
    {
        _campaigns.Add(campaign);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Campaign campaign, CancellationToken cancellationToken = default)
    {
        UpdatedCampaigns.Add(campaign);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _campaigns.RemoveAll(c => c.Id == id);
        return Task.CompletedTask;
    }
}