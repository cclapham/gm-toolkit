using GmToolkit.Core.Import;
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

    /// <summary>
    /// Stats round-trip through <c>UpdateAsync</c>, not just <c>AddAsync</c> -- sqlite-net-pcl's
    /// update path is a different SQL statement (an <c>UPDATE</c>, not an <c>INSERT</c>) from add,
    /// and the real GM flow for an existing NPC's stats (open an already-saved NPC, add/change a
    /// stat, save) always goes through this path, not add.
    /// </summary>
    [Fact]
    public async Task Update_changes_NpcStats()
    {
        var npc = new Npc { CampaignId = Guid.NewGuid(), Name = "The Pale Fisherman" };
        await _repository.AddAsync(npc);

        npc.Stats["HP"] = "45";
        npc.Stats["AC"] = "14";
        await _repository.UpdateAsync(npc);

        var fetched = await _repository.GetAsync(npc.Id);
        Assert.NotNull(fetched);
        Assert.Equal(npc.Stats, fetched.Stats);
    }

    [Fact]
    public async Task Delete_removes_the_npc()
    {
        var npc = new Npc { CampaignId = Guid.NewGuid(), Name = "Old Marta" };
        await _repository.AddAsync(npc);

        await _repository.DeleteAsync(npc.Id);

        Assert.Null(await _repository.GetAsync(npc.Id));
    }

    /// <summary>
    /// A row with malformed <c>StatsJson</c> (e.g. hand-edited directly in the database file) must
    /// not fail the read for that NPC — it should come back with empty stats instead of throwing a
    /// raw <see cref="System.Text.Json.JsonException"/> up through the repository.
    /// </summary>
    [Fact]
    public async Task Get_with_malformed_StatsJson_returns_the_npc_with_empty_stats_instead_of_throwing()
    {
        var npc = new Npc { CampaignId = Guid.NewGuid(), Name = "Old Marta" };
        await _repository.AddAsync(npc);
        await _database.Connection.ExecuteAsync(
            "UPDATE Npcs SET StatsJson = ? WHERE Id = ?", "{not valid json", npc.Id);

        var exception = await Record.ExceptionAsync(() => _repository.GetAsync(npc.Id));

        Assert.Null(exception);
        var fetched = await _repository.GetAsync(npc.Id);
        Assert.NotNull(fetched);
        Assert.Empty(fetched.Stats);
    }

    /// <summary>
    /// A row with malformed <c>StatsJson</c> must not have that malformed data permanently
    /// destroyed the moment the app happens to load and save it again -- e.g. a GM opening the
    /// campaign, glancing at (but not touching) this NPC's other fields, and saving. Before this
    /// fix, <c>Npc.Stats</c> came back empty on read (correctly, so the read itself doesn't fail —
    /// see the sibling test above) but the write path unconditionally re-serialized that same
    /// empty <c>Stats</c> right back over the original bytes on the very next save, silently
    /// turning a recoverable "one row needs manual attention" problem into permanent data loss.
    /// </summary>
    [Fact]
    public async Task Update_of_an_npc_with_malformed_StatsJson_preserves_the_original_bytes_unchanged()
    {
        var npc = new Npc { CampaignId = Guid.NewGuid(), Name = "Old Marta", Role = "Innkeeper" };
        await _repository.AddAsync(npc);
        const string malformedStatsJson = "{not valid json";
        await _database.Connection.ExecuteAsync(
            "UPDATE Npcs SET StatsJson = ? WHERE Id = ?", malformedStatsJson, npc.Id);

        var fetched = await _repository.GetAsync(npc.Id);
        Assert.NotNull(fetched);
        Assert.True(fetched.HasMalformedStats);
        Assert.Empty(fetched.Stats);

        // Save it back completely unmodified -- e.g. the GM merely opened and re-saved this NPC's
        // other fields, never touching its stats at all.
        await _repository.UpdateAsync(fetched);

        var rawStatsJsonAfterSave = await _database.Connection.ExecuteScalarAsync<string>(
            "SELECT StatsJson FROM Npcs WHERE Id = ?", npc.Id);
        Assert.Equal(malformedStatsJson, rawStatsJsonAfterSave);
    }

    /// <summary>
    /// An NPC loaded with malformed <c>StatsJson</c> whose stats are then actually edited (not just
    /// re-saved untouched, unlike the sibling "preserves the original bytes unchanged" test above)
    /// must persist the GM's new stats, not silently discard them by writing the stale malformed
    /// bytes back over them. Regression test for the bug where <c>HasMalformedStats</c> was a flag
    /// set once at load and never cleared, so <c>NpcMapper.ToRow</c> kept preserving the original
    /// corrupted bytes forever, even after the GM retyped every stat by hand.
    /// </summary>
    [Fact]
    public async Task Update_of_an_npc_with_malformed_StatsJson_after_editing_stats_persists_the_new_stats()
    {
        var npc = new Npc { CampaignId = Guid.NewGuid(), Name = "Old Marta" };
        await _repository.AddAsync(npc);
        const string malformedStatsJson = "{not valid json";
        await _database.Connection.ExecuteAsync(
            "UPDATE Npcs SET StatsJson = ? WHERE Id = ?", malformedStatsJson, npc.Id);

        var fetched = await _repository.GetAsync(npc.Id);
        Assert.NotNull(fetched);
        Assert.True(fetched.HasMalformedStats);

        // The GM notices the empty stats, retypes them by hand, and saves.
        fetched.Stats["HP"] = "45";
        fetched.Stats["AC"] = "14";
        Assert.False(fetched.HasMalformedStats);
        await _repository.UpdateAsync(fetched);

        var reloaded = await _repository.GetAsync(npc.Id);
        Assert.NotNull(reloaded);
        Assert.False(reloaded.HasMalformedStats);
        Assert.Equal("45", reloaded.Stats["HP"]);
        Assert.Equal("14", reloaded.Stats["AC"]);

        var rawStatsJsonAfterSave = await _database.Connection.ExecuteScalarAsync<string>(
            "SELECT StatsJson FROM Npcs WHERE Id = ?", npc.Id);
        Assert.NotEqual(malformedStatsJson, rawStatsJsonAfterSave);
    }

    /// <summary>
    /// One NPC row with malformed <c>StatsJson</c> must not poison the whole campaign's NPC list —
    /// every other NPC in that campaign must still come back normally.
    /// </summary>
    [Fact]
    public async Task GetByCampaign_with_one_npc_having_malformed_StatsJson_still_returns_every_npc()
    {
        var campaignId = Guid.NewGuid();
        var goodNpc = new Npc
        {
            CampaignId = campaignId,
            Name = "Dock Foreman",
            Stats = new Dictionary<string, string> { ["HP"] = "12" },
        };
        var badNpc = new Npc { CampaignId = campaignId, Name = "Old Marta" };
        await _repository.AddAsync(goodNpc);
        await _repository.AddAsync(badNpc);
        await _database.Connection.ExecuteAsync(
            "UPDATE Npcs SET StatsJson = ? WHERE Id = ?", "{not valid json", badNpc.Id);

        var result = await _repository.GetByCampaignAsync(campaignId);

        Assert.Equal(2, result.Count);
        var fetchedGoodNpc = Assert.Single(result, n => n.Id == goodNpc.Id);
        Assert.Equal(goodNpc.Stats, fetchedGoodNpc.Stats);
        var fetchedBadNpc = Assert.Single(result, n => n.Id == badNpc.Id);
        Assert.Empty(fetchedBadNpc.Stats);
    }

    [Fact]
    public async Task ImportCharactersAsync_creates_every_valid_new_npc()
    {
        var campaignId = Guid.NewGuid();
        var dtos = new List<NpcExportDto>
        {
            new() { Name = "Old Marta", Role = "Innkeeper", Stats = new Dictionary<string, string> { ["HP"] = "12" } },
            new() { Name = "Dock Foreman" },
        };

        var result = await _repository.ImportCharactersAsync(campaignId, dtos, overwrite: false);

        Assert.True(result.AllSucceeded);
        Assert.Equal(2, result.Imported.Count);
        var fetched = await _repository.GetByCampaignAsync(campaignId);
        Assert.Equal(2, fetched.Count);
        Assert.Contains(fetched, n => n.Name == "Old Marta" && n.Stats["HP"] == "12");
    }

    [Fact]
    public async Task ImportCharactersAsync_skips_an_invalid_entry_but_still_imports_the_rest()
    {
        var campaignId = Guid.NewGuid();
        var dtos = new List<NpcExportDto>
        {
            new() { Name = string.Empty },
            new() { Name = "Dock Foreman" },
        };

        var result = await _repository.ImportCharactersAsync(campaignId, dtos, overwrite: false);

        Assert.False(result.AllSucceeded);
        Assert.Single(result.Imported);
        Assert.Equal("Dock Foreman", result.Imported[0].Name);
        var error = Assert.Single(result.Errors);
        Assert.Equal(0, error.Index);
    }

    [Fact]
    public async Task ImportCharactersAsync_without_overwrite_skips_a_name_conflict_and_leaves_the_existing_npc_untouched()
    {
        var campaignId = Guid.NewGuid();
        var existing = new Npc { CampaignId = campaignId, Name = "Old Marta", Role = "Innkeeper" };
        await _repository.AddAsync(existing);

        var dtos = new List<NpcExportDto> { new() { Name = "Old Marta", Role = "Cult Leader" } };
        var result = await _repository.ImportCharactersAsync(campaignId, dtos, overwrite: false);

        Assert.Empty(result.Imported);
        Assert.Single(result.Errors);

        var fetched = await _repository.GetAsync(existing.Id);
        Assert.NotNull(fetched);
        Assert.Equal("Innkeeper", fetched.Role);
    }

    [Fact]
    public async Task ImportCharactersAsync_with_overwrite_replaces_the_existing_npc_in_place()
    {
        var campaignId = Guid.NewGuid();
        var existing = new Npc { CampaignId = campaignId, Name = "Old Marta", Role = "Innkeeper" };
        await _repository.AddAsync(existing);

        var dtos = new List<NpcExportDto> { new() { Name = "Old Marta", Role = "Cult Leader" } };
        var result = await _repository.ImportCharactersAsync(campaignId, dtos, overwrite: true);

        Assert.True(result.AllSucceeded);
        var imported = Assert.Single(result.Imported);
        // Overwrite replaces the row in place -- same id, not a duplicate.
        Assert.Equal(existing.Id, imported.Id);
        Assert.Equal("Cult Leader", imported.Role);

        var all = await _repository.GetByCampaignAsync(campaignId);
        Assert.Single(all);
        Assert.Equal("Cult Leader", all[0].Role);
    }
}