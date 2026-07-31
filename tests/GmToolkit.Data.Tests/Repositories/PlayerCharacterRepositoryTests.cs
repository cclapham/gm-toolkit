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
}