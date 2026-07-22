using GmToolkit.Core.Models;
using GmToolkit.Data.Repositories;

namespace GmToolkit.Data.Tests.Repositories;

public class CampaignRepositoryTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
    private GmToolkitDatabase _database = null!;
    private CampaignRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _database = new GmToolkitDatabase(_dbPath);
        await _database.InitializeAsync();
        _repository = new CampaignRepository(_database);
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
        var campaign = new Campaign
        {
            Name = "Wandering Souls",
            GameSystem = "D&D 5e",
            Description = "A haunted coastal town.",
        };

        await _repository.AddAsync(campaign);
        var fetched = await _repository.GetAsync(campaign.Id);

        Assert.NotNull(fetched);
        Assert.Equal(campaign.Name, fetched.Name);
        Assert.Equal(campaign.GameSystem, fetched.GameSystem);
        Assert.Equal(campaign.Description, fetched.Description);
        Assert.Empty(fetched.PlayerCharacters);
        Assert.Empty(fetched.Npcs);
    }

    [Fact]
    public async Task Get_of_nonexistent_id_returns_null()
    {
        var fetched = await _repository.GetAsync(Guid.NewGuid());

        Assert.Null(fetched);
    }

    [Fact]
    public async Task GetAll_returns_every_added_campaign()
    {
        await _repository.AddAsync(new Campaign { Name = "Wandering Souls" });
        await _repository.AddAsync(new Campaign { Name = "Shadows Over Blackmoor" });

        var all = await _repository.GetAllAsync();

        Assert.Equal(2, all.Count);
        Assert.Contains(all, c => c.Name == "Wandering Souls");
        Assert.Contains(all, c => c.Name == "Shadows Over Blackmoor");
    }

    [Fact]
    public async Task Update_persists_changes()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        await _repository.AddAsync(campaign);

        campaign.Name = "The Rustbelt Job";
        campaign.GameSystem = "Blades in the Dark";
        await _repository.UpdateAsync(campaign);

        var fetched = await _repository.GetAsync(campaign.Id);
        Assert.NotNull(fetched);
        Assert.Equal("The Rustbelt Job", fetched.Name);
        Assert.Equal("Blades in the Dark", fetched.GameSystem);
    }

    [Fact]
    public async Task Delete_removes_the_campaign()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        await _repository.AddAsync(campaign);

        await _repository.DeleteAsync(campaign.Id);

        Assert.Null(await _repository.GetAsync(campaign.Id));
    }

    [Fact]
    public async Task Delete_cascades_to_player_characters_and_npcs()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        await _repository.AddAsync(campaign);

        var pcRepository = new PlayerCharacterRepository(_database);
        var npcRepository = new NpcRepository(_database);
        await pcRepository.AddAsync(new PlayerCharacter { CampaignId = campaign.Id, CharacterName = "Brannigan" });
        await npcRepository.AddAsync(new Npc { CampaignId = campaign.Id, Name = "Old Marta" });

        await _repository.DeleteAsync(campaign.Id);

        Assert.Empty(await pcRepository.GetByCampaignAsync(campaign.Id));
        Assert.Empty(await npcRepository.GetByCampaignAsync(campaign.Id));
    }

    [Fact]
    public async Task Get_includes_player_characters_and_npcs_belonging_to_the_campaign()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        await _repository.AddAsync(campaign);

        var otherCampaign = new Campaign { Name = "Shadows Over Blackmoor" };
        await _repository.AddAsync(otherCampaign);

        var pcRepository = new PlayerCharacterRepository(_database);
        var npcRepository = new NpcRepository(_database);
        await pcRepository.AddAsync(new PlayerCharacter { CampaignId = campaign.Id, CharacterName = "Brannigan" });
        await npcRepository.AddAsync(new Npc { CampaignId = campaign.Id, Name = "Old Marta" });
        // Belongs to the other campaign — must not leak into this one's aggregate.
        await pcRepository.AddAsync(new PlayerCharacter { CampaignId = otherCampaign.Id, CharacterName = "Eleanor" });

        var fetched = await _repository.GetAsync(campaign.Id);

        Assert.NotNull(fetched);
        Assert.Single(fetched.PlayerCharacters);
        Assert.Equal("Brannigan", fetched.PlayerCharacters[0].CharacterName);
        Assert.Single(fetched.Npcs);
        Assert.Equal("Old Marta", fetched.Npcs[0].Name);
    }
}