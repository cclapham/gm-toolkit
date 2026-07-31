using GmToolkit.Core.Models;
using GmToolkit.Data.Repositories;

namespace GmToolkit.Data.Tests.Repositories;

public class NpcRepositoryTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
    private GmToolkitDatabase _database = null!;
    private NpcRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _database = new GmToolkitDatabase(_dbPath);
        await _database.InitializeAsync();
        _repository = new NpcRepository(_database);
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    [Fact]
    public async Task Add_then_get_round_trips_all_fields()
    {
        var campaignId = Guid.NewGuid();
        var npc = new Npc
        {
            CampaignId = campaignId,
            Name = "Old Marta",
            Role = "Innkeeper",
            Faction = "Neutral",
            Location = "The Wandering Boar",
            Appearance = "Stooped, flour-dusted apron",
            Mannerism = "Hums old sea shanties",
            Motivation = "Keep her regulars fed",
            Secret = "Used to run with pirates",
            KnownToPlayers = true,
            WasGenerated = true,
        };

        await _repository.AddAsync(npc);
        var fetched = await _repository.GetAsync(npc.Id);

        Assert.NotNull(fetched);
        Assert.Equal(campaignId, fetched.CampaignId);
        Assert.Equal("Old Marta", fetched.Name);
        Assert.Equal("Innkeeper", fetched.Role);
        Assert.Equal("The Wandering Boar", fetched.Location);
        Assert.True(fetched.KnownToPlayers);
        Assert.True(fetched.WasGenerated);
    }

    [Fact]
    public async Task Add_then_get_round_trips_stats()
    {
        var npc = new Npc
        {
            CampaignId = Guid.NewGuid(),
            Name = "The Pale Fisherman",
            Stats = new Dictionary<string, string>
            {
                ["HP"] = "45",
                ["AC"] = "14",
                ["Special"] = "Never quite dries",
            },
        };

        await _repository.AddAsync(npc);
        var fetched = await _repository.GetAsync(npc.Id);

        Assert.NotNull(fetched);
        Assert.Equal(npc.Stats, fetched.Stats);
    }

    [Fact]
    public async Task Add_then_get_with_no_stats_returns_an_empty_dictionary()
    {
        var npc = new Npc { CampaignId = Guid.NewGuid(), Name = "Old Marta" };

        await _repository.AddAsync(npc);
        var fetched = await _repository.GetAsync(npc.Id);

        Assert.NotNull(fetched);
        Assert.Empty(fetched.Stats);
    }

    [Fact]
    public async Task GetByCampaign_returns_only_that_campaigns_npcs()
    {
        var campaignId = Guid.NewGuid();
        var otherCampaignId = Guid.NewGuid();
        await _repository.AddAsync(new Npc { CampaignId = campaignId, Name = "Old Marta" });
        await _repository.AddAsync(new Npc { CampaignId = campaignId, Name = "Dock Foreman" });
        await _repository.AddAsync(new Npc { CampaignId = otherCampaignId, Name = "Cultist" });

        var result = await _repository.GetByCampaignAsync(campaignId);

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, n => n.Name == "Cultist");
    }

    [Fact]
    public async Task Update_persists_changes()
    {
        var npc = new Npc { CampaignId = Guid.NewGuid(), Name = "Old Marta", KnownToPlayers = false };
        await _repository.AddAsync(npc);

        npc.KnownToPlayers = true;
        npc.Secret = "Revealed to the party";
        await _repository.UpdateAsync(npc);

        var fetched = await _repository.GetAsync(npc.Id);
        Assert.NotNull(fetched);
        Assert.True(fetched.KnownToPlayers);
        Assert.Equal("Revealed to the party", fetched.Secret);
    }

    [Fact]
    public async Task Delete_removes_the_npc()
    {
        var npc = new Npc { CampaignId = Guid.NewGuid(), Name = "Old Marta" };
        await _repository.AddAsync(npc);

        await _repository.DeleteAsync(npc.Id);

        Assert.Null(await _repository.GetAsync(npc.Id));
    }
}