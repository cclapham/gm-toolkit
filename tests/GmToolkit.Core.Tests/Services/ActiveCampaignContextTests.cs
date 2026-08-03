using GmToolkit.Core.Import;
using GmToolkit.Core.Models;
using GmToolkit.Core.Repositories;
using GmToolkit.Core.Services;

namespace GmToolkit.Core.Tests.Services;

public class ActiveCampaignContextTests
{
    [Fact]
    public async Task SelectCampaignAsync_sets_the_active_campaign_and_stamps_LastOpenedUtc()
    {
        var repository = new FakeCampaignRepository();
        var context = new ActiveCampaignContext(repository);
        var campaign = new Campaign
        {
            Name = "Wandering Souls",
            LastOpenedUtc = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        var before = DateTime.UtcNow;

        await context.SelectCampaignAsync(campaign);

        Assert.Same(campaign, context.ActiveCampaign);
        Assert.True(campaign.LastOpenedUtc >= before);
    }

    [Fact]
    public async Task SelectCampaignAsync_persists_the_campaign_via_the_repository()
    {
        var repository = new FakeCampaignRepository();
        var context = new ActiveCampaignContext(repository);
        var campaign = new Campaign { Name = "Wandering Souls" };

        await context.SelectCampaignAsync(campaign);

        Assert.Single(repository.UpdatedCampaigns);
        Assert.Same(campaign, repository.UpdatedCampaigns[0]);
    }

    [Fact]
    public async Task SelectCampaignAsync_raises_ActiveCampaignChanged()
    {
        var repository = new FakeCampaignRepository();
        var context = new ActiveCampaignContext(repository);
        var campaign = new Campaign { Name = "Wandering Souls" };
        var raiseCount = 0;
        context.ActiveCampaignChanged += () => raiseCount++;

        await context.SelectCampaignAsync(campaign);

        Assert.Equal(1, raiseCount);
    }

    [Fact]
    public void Clear_sets_ActiveCampaign_to_null_and_raises_ActiveCampaignChanged()
    {
        var repository = new FakeCampaignRepository();
        var context = new ActiveCampaignContext(repository);
        var raiseCount = 0;
        context.ActiveCampaignChanged += () => raiseCount++;

        context.Clear();

        Assert.Null(context.ActiveCampaign);
        Assert.Equal(1, raiseCount);
    }

    [Fact]
    public async Task Clear_after_a_selection_removes_it_and_does_not_touch_persistence()
    {
        var repository = new FakeCampaignRepository();
        var context = new ActiveCampaignContext(repository);
        var campaign = new Campaign { Name = "Wandering Souls" };
        await context.SelectCampaignAsync(campaign);
        var updateCountBeforeClear = repository.UpdatedCampaigns.Count;

        context.Clear();

        Assert.Null(context.ActiveCampaign);
        Assert.Equal(updateCountBeforeClear, repository.UpdatedCampaigns.Count);
    }

    [Fact]
    public async Task RestoreLastOpenedAsync_with_no_campaigns_leaves_ActiveCampaign_null()
    {
        var repository = new FakeCampaignRepository();
        var context = new ActiveCampaignContext(repository);

        await context.RestoreLastOpenedAsync();

        Assert.Null(context.ActiveCampaign);
    }

    [Fact]
    public async Task RestoreLastOpenedAsync_picks_the_campaign_with_the_latest_LastOpenedUtc()
    {
        var older = new Campaign { Name = "Shadows Over Blackmoor", LastOpenedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
        var newest = new Campaign { Name = "Wandering Souls", LastOpenedUtc = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc) };
        var middle = new Campaign { Name = "The Rustbelt Job", LastOpenedUtc = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc) };
        var repository = new FakeCampaignRepository(older, newest, middle);
        var context = new ActiveCampaignContext(repository);

        await context.RestoreLastOpenedAsync();

        Assert.Same(newest, context.ActiveCampaign);
    }

    [Fact]
    public async Task RestoreLastOpenedAsync_does_not_persist_anything()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        var repository = new FakeCampaignRepository(campaign);
        var context = new ActiveCampaignContext(repository);

        await context.RestoreLastOpenedAsync();

        Assert.Empty(repository.UpdatedCampaigns);
    }

    private sealed class FakeCampaignRepository(params Campaign[] campaigns) : ICampaignRepository
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

        public Task<CampaignExportDto?> ExportCampaignAsync(Guid campaignId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not needed by ActiveCampaignContext.");

        public Task<CampaignImportResult> ImportCampaignAsync(CampaignExportDto dto, bool overwrite, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not needed by ActiveCampaignContext.");
    }
}