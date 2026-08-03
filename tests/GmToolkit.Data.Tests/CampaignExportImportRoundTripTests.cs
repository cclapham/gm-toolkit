using System.Text.Json;

using GmToolkit.Core.Import;
using GmToolkit.Core.Models;
using GmToolkit.Data.Repositories;

namespace GmToolkit.Data.Tests;

/// <summary>
/// The M5 exit criterion, written as a test: a campaign with player characters and NPCs survives
/// a real export → JSON file → import round trip, including a genuine dispose-and-reconnect cycle
/// against the on-disk SQLite file — mirrors <see cref="CampaignRoundTripTests"/>'s pattern for the
/// M1 exit criterion.
/// </summary>
public sealed class CampaignExportImportRoundTripTests : IAsyncLifetime
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
    public async Task Export_then_reimport_through_real_json_text_round_trips_the_whole_campaign_through_a_fresh_connection()
    {
        var originalCampaignId = Guid.NewGuid();

        await using (var writeDatabase = new GmToolkitDatabase(_dbPath))
        {
            await writeDatabase.InitializeAsync();
            var campaignRepository = new CampaignRepository(writeDatabase);
            var pcRepository = new PlayerCharacterRepository(writeDatabase);
            var npcRepository = new NpcRepository(writeDatabase);

            var campaign = new Campaign
            {
                Id = originalCampaignId,
                Name = "Wandering Souls",
                GameSystem = "D&D 5e",
                Description = "A haunted coastal town.",
            };
            await campaignRepository.AddAsync(campaign);
            await pcRepository.AddAsync(new PlayerCharacter
            {
                CampaignId = originalCampaignId,
                CharacterName = "Brannigan Thistlewood",
                PlayerName = "Alex",
                Ancestry = "Half-Elf",
                Class = "Ranger",
                Level = 4,
                Notes = "Afraid of boats.",
                Stats = new Dictionary<string, string> { ["STR"] = "14", ["Passive Perception"] = "16" },
            });
            await npcRepository.AddAsync(new Npc
            {
                CampaignId = originalCampaignId,
                Name = "Old Marta",
                Role = "Innkeeper",
                KnownToPlayers = true,
                Stats = new Dictionary<string, string> { ["HP"] = "12" },
            });

            // The actual "export a real campaign as JSON, parse it, re-import" path: DTO -> JSON
            // text -> back to a DTO, exactly as a file written to and read back from disk would.
            var exportedDto = await campaignRepository.ExportCampaignAsync(originalCampaignId);
            Assert.NotNull(exportedDto);

            var json = JsonSerializer.Serialize(exportedDto, CampaignExportJsonContext.Default.CampaignExportDto);
            var reparsedDto = JsonSerializer.Deserialize(json, CampaignExportJsonContext.Default.CampaignExportDto);
            Assert.NotNull(reparsedDto);

            // Re-importing the campaign's own export shares its own name, so this is the
            // real-world "restore/duplicate my own export" flow -- exercised with overwrite: true.
            // (The no-overwrite conflict path has its own dedicated test below.)
            var importResult = await campaignRepository.ImportCampaignAsync(reparsedDto, overwrite: true);

            Assert.True(importResult.Succeeded);
            Assert.NotNull(importResult.Campaign);
            // Import always mints a fresh id -- never the original campaign's.
            Assert.NotEqual(originalCampaignId, importResult.Campaign.Id);
        }

        // A fresh connection against the same file proves the imported campaign actually persisted
        // to disk, not just the first connection's in-memory cache.
        await using var readDatabase = new GmToolkitDatabase(_dbPath);
        await readDatabase.InitializeAsync();
        var freshCampaignRepository = new CampaignRepository(readDatabase);

        var allCampaigns = await freshCampaignRepository.GetAllAsync();
        // overwrite: true replaced the original campaign in place (by name) with the imported one.
        var imported = Assert.Single(allCampaigns);
        Assert.NotEqual(originalCampaignId, imported.Id);
        Assert.Equal("Wandering Souls", imported.Name);
        Assert.Equal("D&D 5e", imported.GameSystem);
        Assert.Equal("A haunted coastal town.", imported.Description);

        var importedPc = Assert.Single(imported.PlayerCharacters);
        Assert.Equal("Brannigan Thistlewood", importedPc.CharacterName);
        Assert.Equal("Alex", importedPc.PlayerName);
        Assert.Equal(4, importedPc.Level);
        Assert.Equal("14", importedPc.Stats["STR"]);
        Assert.Equal("16", importedPc.Stats["Passive Perception"]);

        var importedNpc = Assert.Single(imported.Npcs);
        Assert.Equal("Old Marta", importedNpc.Name);
        Assert.True(importedNpc.KnownToPlayers);
        Assert.Equal("12", importedNpc.Stats["HP"]);
    }

    [Fact]
    public async Task ExportCampaignAsync_of_a_nonexistent_campaign_returns_null()
    {
        await using var database = new GmToolkitDatabase(_dbPath);
        await database.InitializeAsync();
        var repository = new CampaignRepository(database);

        var result = await repository.ExportCampaignAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task ImportCampaignAsync_with_an_invalid_dto_writes_nothing_and_returns_validation_errors()
    {
        await using var database = new GmToolkitDatabase(_dbPath);
        await database.InitializeAsync();
        var repository = new CampaignRepository(database);

        var invalidDto = new CampaignExportDto { Name = string.Empty };

        var result = await repository.ImportCampaignAsync(invalidDto, overwrite: false);

        Assert.False(result.Succeeded);
        Assert.False(result.Validation.IsValid);
        Assert.NotEmpty(result.Validation.Errors);
        Assert.Empty(await repository.GetAllAsync());
    }

    [Fact]
    public async Task ImportCampaignAsync_without_overwrite_on_a_name_conflict_refuses_and_leaves_the_existing_campaign_untouched()
    {
        await using var database = new GmToolkitDatabase(_dbPath);
        await database.InitializeAsync();
        var repository = new CampaignRepository(database);

        var existing = new Campaign { Name = "Wandering Souls", Description = "Original." };
        await repository.AddAsync(existing);

        var dto = new CampaignExportDto { Name = "Wandering Souls", Description = "Conflicting import." };
        var result = await repository.ImportCampaignAsync(dto, overwrite: false);

        Assert.False(result.Succeeded);
        Assert.False(result.Validation.IsValid);

        var all = await repository.GetAllAsync();
        var only = Assert.Single(all);
        Assert.Equal(existing.Id, only.Id);
        Assert.Equal("Original.", only.Description);
    }

    [Fact]
    public async Task ImportCampaignAsync_with_overwrite_replaces_the_existing_campaign_and_its_children()
    {
        await using var database = new GmToolkitDatabase(_dbPath);
        await database.InitializeAsync();
        var campaignRepository = new CampaignRepository(database);
        var pcRepository = new PlayerCharacterRepository(database);
        var npcRepository = new NpcRepository(database);

        var existing = new Campaign { Name = "Wandering Souls", Description = "Original." };
        await campaignRepository.AddAsync(existing);
        await pcRepository.AddAsync(new PlayerCharacter { CampaignId = existing.Id, CharacterName = "Old PC" });
        await npcRepository.AddAsync(new Npc { CampaignId = existing.Id, Name = "Old NPC" });

        var dto = new CampaignExportDto
        {
            Name = "Wandering Souls",
            Description = "Replacement.",
            PlayerCharacters = [new PlayerCharacterExportDto { CharacterName = "New PC" }],
            Npcs = [new NpcExportDto { Name = "New NPC" }],
        };

        var result = await campaignRepository.ImportCampaignAsync(dto, overwrite: true);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Campaign);
        Assert.NotEqual(existing.Id, result.Campaign.Id);

        var all = await campaignRepository.GetAllAsync();
        var only = Assert.Single(all);
        Assert.Equal("Replacement.", only.Description);
        Assert.Single(only.PlayerCharacters, pc => pc.CharacterName == "New PC");
        Assert.Single(only.Npcs, npc => npc.Name == "New NPC");

        // The old campaign's own children are gone, not just orphaned or shadowed.
        Assert.Empty(await pcRepository.GetByCampaignAsync(existing.Id));
        Assert.Empty(await npcRepository.GetByCampaignAsync(existing.Id));
    }
}