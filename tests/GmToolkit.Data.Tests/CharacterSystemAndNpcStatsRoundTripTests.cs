using GmToolkit.Core.Models;
using GmToolkit.Data.Repositories;

namespace GmToolkit.Data.Tests;

/// <summary>
/// #88's exit criterion, written as a test: a campaign with a <see cref="Campaign.CharacterSystemId"/>
/// attached and an NPC with a typed <see cref="Npc.Stats"/> bag both survive a genuine
/// dispose-and-reconnect cycle against a real on-disk SQLite file.
/// </summary>
public sealed class CharacterSystemAndNpcStatsRoundTripTests : IAsyncLifetime
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
    public async Task Campaign_system_attachment_and_npc_stats_survive_close_and_reopen()
    {
        var campaign = new Campaign
        {
            Name = "Wandering Souls",
            CharacterSystemId = "dnd5e-2024",
        };

        var npc = new Npc
        {
            CampaignId = campaign.Id,
            Name = "The Pale Fisherman",
            Role = "Cult Leader",
            Stats = new Dictionary<string, string>
            {
                ["HP"] = "45",
                ["AC"] = "14",
                ["CR"] = "5",
            },
        };

        await using (var writeDatabase = new GmToolkitDatabase(_dbPath))
        {
            await writeDatabase.InitializeAsync();

            var campaignRepository = new CampaignRepository(writeDatabase);
            var npcRepository = new NpcRepository(writeDatabase);

            await campaignRepository.AddAsync(campaign);
            await npcRepository.AddAsync(npc);
        }

        // The `await using` block above disposed the connection entirely. A fresh connection
        // against the same file path proves the data actually persisted to disk rather than
        // merely living in the first connection's cache.
        await using var readDatabase = new GmToolkitDatabase(_dbPath);
        await readDatabase.InitializeAsync();

        var freshCampaignRepository = new CampaignRepository(readDatabase);
        var freshNpcRepository = new NpcRepository(readDatabase);

        var fetchedCampaign = await freshCampaignRepository.GetAsync(campaign.Id);
        Assert.NotNull(fetchedCampaign);
        Assert.Equal("dnd5e-2024", fetchedCampaign.CharacterSystemId);

        var fetchedNpc = await freshNpcRepository.GetAsync(npc.Id);
        Assert.NotNull(fetchedNpc);
        Assert.Equal(npc.Stats, fetchedNpc.Stats);

        // Also reachable through the campaign aggregate's own child-loading.
        Assert.Single(fetchedCampaign.Npcs);
        Assert.Equal(npc.Stats, fetchedCampaign.Npcs[0].Stats);
    }

    [Fact]
    public async Task Campaign_with_no_system_attached_keeps_freeform_behavior_after_reopen()
    {
        var campaign = new Campaign { Name = "Freeform Homebrew Campaign" };
        var npc = new Npc { CampaignId = campaign.Id, Name = "Old Marta" };

        await using (var writeDatabase = new GmToolkitDatabase(_dbPath))
        {
            await writeDatabase.InitializeAsync();

            var campaignRepository = new CampaignRepository(writeDatabase);
            var npcRepository = new NpcRepository(writeDatabase);

            await campaignRepository.AddAsync(campaign);
            await npcRepository.AddAsync(npc);
        }

        await using var readDatabase = new GmToolkitDatabase(_dbPath);
        await readDatabase.InitializeAsync();

        var fetchedCampaign = await new CampaignRepository(readDatabase).GetAsync(campaign.Id);
        var fetchedNpc = await new NpcRepository(readDatabase).GetAsync(npc.Id);

        Assert.NotNull(fetchedCampaign);
        Assert.Null(fetchedCampaign.CharacterSystemId);

        Assert.NotNull(fetchedNpc);
        Assert.Empty(fetchedNpc.Stats);
    }
}