using GmToolkit.Core.Import;
using GmToolkit.Core.Models;
using GmToolkit.Data.Repositories;

namespace GmToolkit.Data.Tests;

/// <summary>
/// Real-SQLite coverage of <see cref="CampaignImportOrchestrator"/>'s three
/// <see cref="ImportConflictResolution"/> branches (issue #130) -- mirrors
/// <see cref="CampaignExportImportRoundTripTests"/>'s pattern (a real temp-file database, not
/// mocks/fakes -- see CONTRIBUTING.md), since the orchestrator's whole job is choosing which of the
/// real <see cref="CampaignRepository"/>/<see cref="PlayerCharacterRepository"/>/<see cref="NpcRepository"/>
/// calls to make.
/// </summary>
public sealed class CampaignImportOrchestratorTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
    private GmToolkitDatabase _database = null!;
    private CampaignRepository _campaignRepository = null!;
    private PlayerCharacterRepository _pcRepository = null!;
    private NpcRepository _npcRepository = null!;
    private CampaignImportOrchestrator _orchestrator = null!;

    public async Task InitializeAsync()
    {
        _database = new GmToolkitDatabase(_dbPath);
        await _database.InitializeAsync();
        _campaignRepository = new CampaignRepository(_database);
        _pcRepository = new PlayerCharacterRepository(_database);
        _npcRepository = new NpcRepository(_database);
        _orchestrator = new CampaignImportOrchestrator(_campaignRepository, _pcRepository, _npcRepository);
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
    public async Task ImportAsync_with_no_existing_campaign_of_that_name_creates_it_regardless_of_resolution()
    {
        var dto = new CampaignExportDto
        {
            Name = "Wandering Souls",
            PlayerCharacters = [new PlayerCharacterExportDto { CharacterName = "Brannigan" }],
        };

        var outcome = await _orchestrator.ImportAsync(dto, ImportConflictResolution.Skip);

        Assert.True(outcome.Succeeded);
        Assert.False(outcome.WasSkipped);
        Assert.NotNull(outcome.Campaign);
        Assert.Equal(1, outcome.CharactersImported);
        Assert.Single(await _campaignRepository.GetAllAsync());
    }

    [Fact]
    public async Task ImportAsync_with_an_invalid_dto_writes_nothing()
    {
        var outcome = await _orchestrator.ImportAsync(new CampaignExportDto { Name = string.Empty }, ImportConflictResolution.Overwrite);

        Assert.False(outcome.Succeeded);
        Assert.NotEmpty(outcome.Validation.Errors);
        Assert.Empty(await _campaignRepository.GetAllAsync());
    }

    [Fact]
    public async Task ImportAsync_skip_on_a_conflict_leaves_the_existing_campaign_completely_untouched()
    {
        var existing = new Campaign { Name = "Wandering Souls", Description = "Original." };
        await _campaignRepository.AddAsync(existing);
        await _pcRepository.AddAsync(new PlayerCharacter { CampaignId = existing.Id, CharacterName = "Old PC" });

        var dto = new CampaignExportDto
        {
            Name = "Wandering Souls",
            Description = "Conflicting import.",
            PlayerCharacters = [new PlayerCharacterExportDto { CharacterName = "New PC" }],
        };

        var outcome = await _orchestrator.ImportAsync(dto, ImportConflictResolution.Skip);

        Assert.False(outcome.Succeeded);
        Assert.True(outcome.WasSkipped);
        Assert.Null(outcome.Campaign);

        var only = Assert.Single(await _campaignRepository.GetAllAsync());
        Assert.Equal(existing.Id, only.Id);
        Assert.Equal("Original.", only.Description);
        Assert.Single(only.PlayerCharacters, pc => pc.CharacterName == "Old PC");
    }

    [Fact]
    public async Task ImportAsync_overwrite_on_a_conflict_replaces_the_whole_campaign_and_its_children()
    {
        var existing = new Campaign { Name = "Wandering Souls", Description = "Original." };
        await _campaignRepository.AddAsync(existing);
        await _pcRepository.AddAsync(new PlayerCharacter { CampaignId = existing.Id, CharacterName = "Old PC" });
        await _npcRepository.AddAsync(new Npc { CampaignId = existing.Id, Name = "Old NPC" });

        var dto = new CampaignExportDto
        {
            Name = "Wandering Souls",
            Description = "Replacement.",
            PlayerCharacters = [new PlayerCharacterExportDto { CharacterName = "New PC" }],
            Npcs = [new NpcExportDto { Name = "New NPC" }],
        };

        var outcome = await _orchestrator.ImportAsync(dto, ImportConflictResolution.Overwrite);

        Assert.True(outcome.Succeeded);
        Assert.NotNull(outcome.Campaign);
        Assert.NotEqual(existing.Id, outcome.Campaign.Id);
        Assert.Equal(1, outcome.CharactersImported);
        Assert.Equal(1, outcome.NpcsImported);

        var only = Assert.Single(await _campaignRepository.GetAllAsync());
        Assert.Equal("Replacement.", only.Description);
        Assert.Single(only.PlayerCharacters, pc => pc.CharacterName == "New PC");
        Assert.Empty(await _pcRepository.GetByCampaignAsync(existing.Id));
    }

    [Fact]
    public async Task ImportAsync_merge_on_a_conflict_keeps_the_existing_campaign_and_adds_new_characters_and_npcs()
    {
        var existing = new Campaign { Name = "Wandering Souls", Description = "Original." };
        await _campaignRepository.AddAsync(existing);
        await _pcRepository.AddAsync(new PlayerCharacter { CampaignId = existing.Id, CharacterName = "Brannigan", Level = 3 });
        await _npcRepository.AddAsync(new Npc { CampaignId = existing.Id, Name = "Old Marta" });

        var dto = new CampaignExportDto
        {
            Name = "Wandering Souls",
            Description = "Ignored -- merge keeps the existing campaign's own metadata.",
            PlayerCharacters =
            [
                new PlayerCharacterExportDto { CharacterName = "Elowen" }, // New.
            ],
            Npcs =
            [
                new NpcExportDto { Name = "Guard Captain" }, // New.
            ],
        };

        var outcome = await _orchestrator.ImportAsync(dto, ImportConflictResolution.Merge);

        Assert.True(outcome.Succeeded);
        Assert.NotNull(outcome.Campaign);
        Assert.Equal(existing.Id, outcome.Campaign.Id); // Same campaign, not a replacement.
        Assert.Equal("Original.", outcome.Campaign.Description); // Untouched by the merge.
        Assert.Equal(1, outcome.CharactersImported);
        Assert.Equal(1, outcome.NpcsImported);
        Assert.Empty(outcome.CharacterErrors);
        Assert.Empty(outcome.NpcErrors);

        var characters = await _pcRepository.GetByCampaignAsync(existing.Id);
        Assert.Equal(2, characters.Count);
        Assert.Contains(characters, pc => pc.CharacterName == "Brannigan" && pc.Level == 3);
        Assert.Contains(characters, pc => pc.CharacterName == "Elowen");

        var npcs = await _npcRepository.GetByCampaignAsync(existing.Id);
        Assert.Equal(2, npcs.Count);
        Assert.Contains(npcs, npc => npc.Name == "Old Marta");
        Assert.Contains(npcs, npc => npc.Name == "Guard Captain");
    }

    [Fact]
    public async Task ImportAsync_merge_replaces_a_same_named_character_or_npc_in_place()
    {
        var existing = new Campaign { Name = "Wandering Souls" };
        await _campaignRepository.AddAsync(existing);
        await _pcRepository.AddAsync(new PlayerCharacter { CampaignId = existing.Id, CharacterName = "Brannigan", Level = 3 });

        var dto = new CampaignExportDto
        {
            Name = "Wandering Souls",
            PlayerCharacters = [new PlayerCharacterExportDto { CharacterName = "Brannigan", Level = 5 }],
        };

        var outcome = await _orchestrator.ImportAsync(dto, ImportConflictResolution.Merge);

        Assert.True(outcome.Succeeded);
        var characters = await _pcRepository.GetByCampaignAsync(existing.Id);
        var only = Assert.Single(characters);
        Assert.Equal(5, only.Level);
    }
}