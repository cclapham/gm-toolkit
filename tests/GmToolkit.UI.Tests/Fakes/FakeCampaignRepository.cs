using GmToolkit.Core.Models;
using GmToolkit.Core.Repositories;

namespace GmToolkit.UI.Tests.Fakes;

/// <summary>In-memory <see cref="ICampaignRepository"/> for constructing a real
/// <see cref="GmToolkit.Core.Services.ActiveCampaignContext"/> in tests without SQLite.</summary>
internal sealed class FakeCampaignRepository(params Campaign[] campaigns) : ICampaignRepository
{
    private readonly List<Campaign> _campaigns = [.. campaigns];

    public List<Campaign> UpdatedCampaigns { get; } = [];

    /// <summary>When set, <see cref="GetAllAsync"/> throws this instead of returning -- for
    /// exercising a load failure (e.g. <c>CampaignsViewModel</c>'s error state) without needing a
    /// real broken database.</summary>
    public Exception? ThrowOnGetAll { get; set; }

    /// <summary>When set, <see cref="AddAsync"/> throws this instead of adding -- for exercising a
    /// save failure (issue #32, e.g. <c>CampaignFormViewModel.SaveAsync</c>'s
    /// <c>catch (Exception ex)</c> path) without needing a real broken database. Mirrors
    /// <see cref="ThrowOnGetAll"/>.</summary>
    public Exception? ThrowOnAdd { get; set; }

    public Task<Campaign?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_campaigns.FirstOrDefault(c => c.Id == id));

    public Task<IReadOnlyList<Campaign>> GetAllAsync(CancellationToken cancellationToken = default) =>
        ThrowOnGetAll is not null
            ? Task.FromException<IReadOnlyList<Campaign>>(ThrowOnGetAll)
            : Task.FromResult<IReadOnlyList<Campaign>>([.. _campaigns]);

    public Task AddAsync(Campaign campaign, CancellationToken cancellationToken = default)
    {
        if (ThrowOnAdd is not null)
        {
            return Task.FromException(ThrowOnAdd);
        }

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