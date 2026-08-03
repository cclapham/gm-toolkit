using GmToolkit.Core.Import;
using GmToolkit.Core.Models;
using GmToolkit.Data.Repositories;

namespace GmToolkit.Data.Tests.Repositories;

public class PlayerCharacterRepositoryTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
    private GmToolkitDatabase _database = null!;
    private PlayerCharacterRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _database = new GmToolkitDatabase(_dbPath);
        await _database.InitializeAsync();
        _repository = new PlayerCharacterRepository(_database);
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
    public async Task Add_then_get_round_trips_all_fields_including_stats()
    {
        var campaignId = Guid.NewGuid();
        var pc = new PlayerCharacter
        {
            CampaignId = campaignId,
            CharacterName = "Brannigan Thistlewood",
            PlayerName = "Alex",
            Ancestry = "Half-Elf",
            Class = "Ranger",
            Level = 4,
            Notes = "Afraid of boats.",
            Stats = new Dictionary<string, string>
            {
                ["STR"] = "14",
                ["Passive Perception"] = "16",
            },
        };

        await _repository.AddAsync(pc);
        var fetched = await _repository.GetAsync(pc.Id);

        Assert.NotNull(fetched);
        Assert.Equal(campaignId, fetched.CampaignId);
        Assert.Equal("Brannigan Thistlewood", fetched.CharacterName);
        Assert.Equal("Alex", fetched.PlayerName);
        Assert.Equal("Half-Elf", fetched.Ancestry);
        Assert.Equal("Ranger", fetched.Class);
        Assert.Equal(4, fetched.Level);
        Assert.Equal("Afraid of boats.", fetched.Notes);
        Assert.Equal("14", fetched.Stats["STR"]);
        Assert.Equal("16", fetched.Stats["Passive Perception"]);
    }

    [Fact]
    public async Task GetByCampaign_returns_only_that_campaigns_characters()
    {
        var campaignId = Guid.NewGuid();
        var otherCampaignId = Guid.NewGuid();
        await _repository.AddAsync(new PlayerCharacter { CampaignId = campaignId, CharacterName = "Brannigan" });
        await _repository.AddAsync(new PlayerCharacter { CampaignId = campaignId, CharacterName = "Eleanor" });
        await _repository.AddAsync(new PlayerCharacter { CampaignId = otherCampaignId, CharacterName = "Zix-9" });

        var result = await _repository.GetByCampaignAsync(campaignId);

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, pc => pc.CharacterName == "Zix-9");
    }

    [Fact]
    public async Task Update_persists_changes()
    {
        var pc = new PlayerCharacter { CampaignId = Guid.NewGuid(), CharacterName = "Brannigan", Level = 1 };
        await _repository.AddAsync(pc);

        pc.Level = 5;
        pc.Stats["STR"] = "16";
        await _repository.UpdateAsync(pc);

        var fetched = await _repository.GetAsync(pc.Id);
        Assert.NotNull(fetched);
        Assert.Equal(5, fetched.Level);
        Assert.Equal("16", fetched.Stats["STR"]);
    }

    [Fact]
    public async Task Delete_removes_the_character()
    {
        var pc = new PlayerCharacter { CampaignId = Guid.NewGuid(), CharacterName = "Brannigan" };
        await _repository.AddAsync(pc);

        await _repository.DeleteAsync(pc.Id);

        Assert.Null(await _repository.GetAsync(pc.Id));
    }

    /// <summary>
    /// A row with malformed <c>StatsJson</c> (e.g. hand-edited directly in the database file) must
    /// not fail the read for that PC — it should come back with empty stats instead of throwing a
    /// raw <see cref="System.Text.Json.JsonException"/> up through the repository.
    /// </summary>
    [Fact]
    public async Task Get_with_malformed_StatsJson_returns_the_character_with_empty_stats_instead_of_throwing()
    {
        var pc = new PlayerCharacter { CampaignId = Guid.NewGuid(), CharacterName = "Brannigan" };
        await _repository.AddAsync(pc);
        await _database.Connection.ExecuteAsync(
            "UPDATE PlayerCharacters SET StatsJson = ? WHERE Id = ?", "{not valid json", pc.Id);

        var exception = await Record.ExceptionAsync(() => _repository.GetAsync(pc.Id));

        Assert.Null(exception);
        var fetched = await _repository.GetAsync(pc.Id);
        Assert.NotNull(fetched);
        Assert.Empty(fetched.Stats);
    }

    /// <summary>
    /// A row with malformed <c>StatsJson</c> must not have that malformed data permanently
    /// destroyed the moment the app happens to load and save it again -- e.g. a GM opening the
    /// campaign, glancing at (but not touching) this PC's other fields, and saving. Before this
    /// fix, <c>PlayerCharacter.Stats</c> came back empty on read (correctly, so the read itself
    /// doesn't fail — see the sibling test above) but the write path unconditionally re-serialized
    /// that same empty <c>Stats</c> right back over the original bytes on the very next save,
    /// silently turning a recoverable "one row needs manual attention" problem into permanent data
    /// loss. This is specifically a regression risk for PCs -- reading a malformed
    /// <c>PlayerCharacter.StatsJson</c> used to throw outright, before this same fix made it
    /// tolerant to match <c>NpcMapper</c>.
    /// </summary>
    [Fact]
    public async Task Update_of_a_character_with_malformed_StatsJson_preserves_the_original_bytes_unchanged()
    {
        var pc = new PlayerCharacter { CampaignId = Guid.NewGuid(), CharacterName = "Brannigan" };
        await _repository.AddAsync(pc);
        const string malformedStatsJson = "{not valid json";
        await _database.Connection.ExecuteAsync(
            "UPDATE PlayerCharacters SET StatsJson = ? WHERE Id = ?", malformedStatsJson, pc.Id);

        var fetched = await _repository.GetAsync(pc.Id);
        Assert.NotNull(fetched);
        Assert.True(fetched.HasMalformedStats);
        Assert.Empty(fetched.Stats);

        // Save it back completely unmodified -- e.g. the GM merely opened and re-saved this PC's
        // other fields, never touching its stats at all.
        await _repository.UpdateAsync(fetched);

        var rawStatsJsonAfterSave = await _database.Connection.ExecuteScalarAsync<string>(
            "SELECT StatsJson FROM PlayerCharacters WHERE Id = ?", pc.Id);
        Assert.Equal(malformedStatsJson, rawStatsJsonAfterSave);
    }

    /// <summary>
    /// A PC loaded with malformed <c>StatsJson</c> whose stats are then actually edited (not just
    /// re-saved untouched, unlike the sibling "preserves the original bytes unchanged" test above)
    /// must persist the GM's new stats, not silently discard them by writing the stale malformed
    /// bytes back over them. Regression test for the bug where <c>HasMalformedStats</c> was a flag
    /// set once at load and never cleared, so <c>PlayerCharacterMapper.ToRow</c> kept preserving the
    /// original corrupted bytes forever, even after the GM retyped every stat by hand.
    /// </summary>
    [Fact]
    public async Task Update_of_a_character_with_malformed_StatsJson_after_editing_stats_persists_the_new_stats()
    {
        var pc = new PlayerCharacter { CampaignId = Guid.NewGuid(), CharacterName = "Brannigan" };
        await _repository.AddAsync(pc);
        const string malformedStatsJson = "{not valid json";
        await _database.Connection.ExecuteAsync(
            "UPDATE PlayerCharacters SET StatsJson = ? WHERE Id = ?", malformedStatsJson, pc.Id);

        var fetched = await _repository.GetAsync(pc.Id);
        Assert.NotNull(fetched);
        Assert.True(fetched.HasMalformedStats);

        // The GM notices the empty stats, retypes them by hand, and saves.
        fetched.Stats["HP"] = "45";
        fetched.Stats["AC"] = "16";
        Assert.False(fetched.HasMalformedStats);
        await _repository.UpdateAsync(fetched);

        var reloaded = await _repository.GetAsync(pc.Id);
        Assert.NotNull(reloaded);
        Assert.False(reloaded.HasMalformedStats);
        Assert.Equal("45", reloaded.Stats["HP"]);
        Assert.Equal("16", reloaded.Stats["AC"]);

        var rawStatsJsonAfterSave = await _database.Connection.ExecuteScalarAsync<string>(
            "SELECT StatsJson FROM PlayerCharacters WHERE Id = ?", pc.Id);
        Assert.NotEqual(malformedStatsJson, rawStatsJsonAfterSave);
    }

    /// <summary>
    /// One PC row with malformed <c>StatsJson</c> must not poison the whole campaign's PC list —
    /// every other PC in that campaign must still come back normally.
    /// </summary>
    [Fact]
    public async Task GetByCampaign_with_one_character_having_malformed_StatsJson_still_returns_every_character()
    {
        var campaignId = Guid.NewGuid();
        var goodPc = new PlayerCharacter
        {
            CampaignId = campaignId,
            CharacterName = "Eleanor",
            Stats = new Dictionary<string, string> { ["STR"] = "12" },
        };
        var badPc = new PlayerCharacter { CampaignId = campaignId, CharacterName = "Brannigan" };
        await _repository.AddAsync(goodPc);
        await _repository.AddAsync(badPc);
        await _database.Connection.ExecuteAsync(
            "UPDATE PlayerCharacters SET StatsJson = ? WHERE Id = ?", "{not valid json", badPc.Id);

        var result = await _repository.GetByCampaignAsync(campaignId);

        Assert.Equal(2, result.Count);
        var fetchedGoodPc = Assert.Single(result, pc => pc.Id == goodPc.Id);
        Assert.Equal(goodPc.Stats, fetchedGoodPc.Stats);
        var fetchedBadPc = Assert.Single(result, pc => pc.Id == badPc.Id);
        Assert.Empty(fetchedBadPc.Stats);
    }

    [Fact]
    public async Task ImportCharactersAsync_creates_every_valid_new_character()
    {
        var campaignId = Guid.NewGuid();
        var dtos = new List<PlayerCharacterExportDto>
        {
            new() { CharacterName = "Brannigan", Level = 4, Stats = new Dictionary<string, string> { ["STR"] = "14" } },
            new() { CharacterName = "Eleanor", Level = 2 },
        };

        var result = await _repository.ImportCharactersAsync(campaignId, dtos, overwrite: false);

        Assert.True(result.AllSucceeded);
        Assert.Equal(2, result.Imported.Count);
        var fetched = await _repository.GetByCampaignAsync(campaignId);
        Assert.Equal(2, fetched.Count);
        Assert.Contains(fetched, pc => pc.CharacterName == "Brannigan" && pc.Stats["STR"] == "14");
    }

    [Fact]
    public async Task ImportCharactersAsync_skips_an_invalid_entry_but_still_imports_the_rest()
    {
        var campaignId = Guid.NewGuid();
        var dtos = new List<PlayerCharacterExportDto>
        {
            new() { CharacterName = string.Empty },
            new() { CharacterName = "Eleanor" },
        };

        var result = await _repository.ImportCharactersAsync(campaignId, dtos, overwrite: false);

        Assert.False(result.AllSucceeded);
        Assert.Single(result.Imported);
        Assert.Equal("Eleanor", result.Imported[0].CharacterName);
        var error = Assert.Single(result.Errors);
        Assert.Equal(0, error.Index);
    }

    [Fact]
    public async Task ImportCharactersAsync_without_overwrite_skips_a_name_conflict_and_leaves_the_existing_character_untouched()
    {
        var campaignId = Guid.NewGuid();
        var existing = new PlayerCharacter { CampaignId = campaignId, CharacterName = "Brannigan", Level = 1 };
        await _repository.AddAsync(existing);

        var dtos = new List<PlayerCharacterExportDto> { new() { CharacterName = "Brannigan", Level = 99 } };
        var result = await _repository.ImportCharactersAsync(campaignId, dtos, overwrite: false);

        Assert.Empty(result.Imported);
        Assert.Single(result.Errors);

        var fetched = await _repository.GetAsync(existing.Id);
        Assert.NotNull(fetched);
        Assert.Equal(1, fetched.Level);
    }

    [Fact]
    public async Task ImportCharactersAsync_with_overwrite_replaces_the_existing_character_in_place()
    {
        var campaignId = Guid.NewGuid();
        var existing = new PlayerCharacter { CampaignId = campaignId, CharacterName = "Brannigan", Level = 1 };
        await _repository.AddAsync(existing);

        var dtos = new List<PlayerCharacterExportDto> { new() { CharacterName = "Brannigan", Level = 99 } };
        var result = await _repository.ImportCharactersAsync(campaignId, dtos, overwrite: true);

        Assert.True(result.AllSucceeded);
        var imported = Assert.Single(result.Imported);
        // Overwrite replaces the row in place -- same id, not a duplicate.
        Assert.Equal(existing.Id, imported.Id);
        Assert.Equal(99, imported.Level);

        var all = await _repository.GetByCampaignAsync(campaignId);
        Assert.Single(all);
        Assert.Equal(99, all[0].Level);
    }
}