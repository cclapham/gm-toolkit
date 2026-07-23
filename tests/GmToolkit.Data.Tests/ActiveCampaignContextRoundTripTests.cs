using GmToolkit.Core.Models;
using GmToolkit.Core.Services;
using GmToolkit.Data.Repositories;

namespace GmToolkit.Data.Tests;

/// <summary>
/// The #16 acceptance criterion, written as a test: selecting a campaign, "closing the app"
/// (disposing the connection), and "reopening" (a fresh connection against the same file, a fresh
/// <see cref="ActiveCampaignContext"/> instance) restores the same campaign as active — proven
/// against a real on-disk SQLite file, not an in-memory fake.
/// </summary>
public sealed class ActiveCampaignContextRoundTripTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Selecting_a_campaign_survives_a_dispose_and_reconnect_and_is_restored_as_active()
    {
        Guid selectedCampaignId;

        await using (var writeDatabase = new GmToolkitDatabase(_dbPath))
        {
            await writeDatabase.InitializeAsync();
            var campaignRepository = new CampaignRepository(writeDatabase);

            // Wandering Souls starts with a deliberately old LastOpenedUtc so the assertion below
            // doesn't rely on wall-clock timing between this construction and the SelectCampaignAsync
            // stamp below to land Shadows Over Blackmoor strictly later.
            var wanderingSouls = new Campaign { Name = "Wandering Souls", LastOpenedUtc = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
            var shadowsOverBlackmoor = new Campaign { Name = "Shadows Over Blackmoor" };
            await campaignRepository.AddAsync(wanderingSouls);
            await campaignRepository.AddAsync(shadowsOverBlackmoor);

            var context = new ActiveCampaignContext(campaignRepository);
            await context.SelectCampaignAsync(shadowsOverBlackmoor);

            selectedCampaignId = shadowsOverBlackmoor.Id;
        }

        // The `await using` block above disposed the connection entirely — "closing the app".
        // A fresh connection, repository and context against the same file — "reopening it" —
        // proves the selection actually persisted to disk rather than only living in memory.
        await using var readDatabase = new GmToolkitDatabase(_dbPath);
        await readDatabase.InitializeAsync();
        var freshCampaignRepository = new CampaignRepository(readDatabase);
        var freshContext = new ActiveCampaignContext(freshCampaignRepository);

        await freshContext.RestoreLastOpenedAsync();

        Assert.NotNull(freshContext.ActiveCampaign);
        Assert.Equal(selectedCampaignId, freshContext.ActiveCampaign.Id);
        Assert.Equal("Shadows Over Blackmoor", freshContext.ActiveCampaign.Name);
    }

    [Fact]
    public async Task Restoring_with_zero_campaigns_leaves_the_active_campaign_null()
    {
        await using var database = new GmToolkitDatabase(_dbPath);
        await database.InitializeAsync();
        var campaignRepository = new CampaignRepository(database);
        var context = new ActiveCampaignContext(campaignRepository);

        var exception = await Record.ExceptionAsync(() => context.RestoreLastOpenedAsync());

        Assert.Null(exception);
        Assert.Null(context.ActiveCampaign);
    }

    [Fact]
    public async Task Restoring_with_multiple_campaigns_picks_the_one_with_the_latest_LastOpenedUtc()
    {
        await using (var writeDatabase = new GmToolkitDatabase(_dbPath))
        {
            await writeDatabase.InitializeAsync();
            var campaignRepository = new CampaignRepository(writeDatabase);

            await campaignRepository.AddAsync(new Campaign
            {
                Name = "Shadows Over Blackmoor",
                LastOpenedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            });
            await campaignRepository.AddAsync(new Campaign
            {
                Name = "The Rustbelt Job",
                LastOpenedUtc = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            });
            await campaignRepository.AddAsync(new Campaign
            {
                Name = "Wandering Souls",
                LastOpenedUtc = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            });
        }

        await using var readDatabase = new GmToolkitDatabase(_dbPath);
        await readDatabase.InitializeAsync();
        var freshCampaignRepository = new CampaignRepository(readDatabase);
        var freshContext = new ActiveCampaignContext(freshCampaignRepository);

        await freshContext.RestoreLastOpenedAsync();

        Assert.NotNull(freshContext.ActiveCampaign);
        Assert.Equal("Wandering Souls", freshContext.ActiveCampaign.Name);
    }
}