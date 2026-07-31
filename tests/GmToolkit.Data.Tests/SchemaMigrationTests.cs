using GmToolkit.Data.Repositories;
using GmToolkit.Data.Rows;

using SQLite;

namespace GmToolkit.Data.Tests;

/// <summary>
/// #88's other exit criterion: a pre-existing (v0.1.0-era, schema v1) database — with no
/// <c>Campaigns.CharacterSystemId</c> or <c>Npcs.StatsJson</c> columns at all — opens cleanly
/// through <see cref="GmToolkitDatabase.InitializeAsync"/> and gets migrated forward, with every
/// pre-existing row's data intact.
/// </summary>
public sealed class SchemaMigrationTests : IAsyncLifetime
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
    public async Task Pre_migration_database_opens_cleanly_and_migrates_with_no_data_loss()
    {
        var campaignId = Guid.NewGuid();
        var npcId = Guid.NewGuid();

        // Build a schema-v1 database by hand: the old row shapes, with PRAGMA user_version left
        // at 1 (i.e. exactly what a v0.1.0 install would have on disk today), and no
        // CharacterSystemId/StatsJson columns at all.
        var v1Connection = new SQLiteAsyncConnection(_dbPath);
        try
        {
            await v1Connection.CreateTableAsync<V1CampaignRow>();
            await v1Connection.CreateTableAsync<V1NpcRow>();

            await v1Connection.InsertAsync(new V1CampaignRow
            {
                Id = campaignId,
                Name = "Wandering Souls",
                GameSystem = "D&D 5e",
                Description = "A haunted coastal town.",
                CreatedUtc = new DateTime(2024, 3, 1, 9, 0, 0, DateTimeKind.Utc),
                LastOpenedUtc = new DateTime(2024, 6, 15, 21, 30, 0, DateTimeKind.Utc),
            });

            await v1Connection.InsertAsync(new V1NpcRow
            {
                Id = npcId,
                CampaignId = campaignId,
                Name = "Old Marta",
                Role = "Innkeeper",
                KnownToPlayers = true,
                CreatedUtc = new DateTime(2024, 3, 2, 10, 0, 0, DateTimeKind.Utc),
                WasGenerated = false,
            });

            await v1Connection.ExecuteAsync("PRAGMA user_version = 1");
        }
        finally
        {
            await v1Connection.CloseAsync();
        }

        // Sanity check the fixture: the new columns genuinely don't exist yet on disk.
        var checkConnection = new SQLiteAsyncConnection(_dbPath);
        try
        {
            var campaignColumns = await checkConnection.GetTableInfoAsync("Campaigns");
            Assert.DoesNotContain(campaignColumns, c => c.Name == "CharacterSystemId");

            var npcColumns = await checkConnection.GetTableInfoAsync("Npcs");
            Assert.DoesNotContain(npcColumns, c => c.Name == "StatsJson");

            var versionBefore = await checkConnection.ExecuteScalarAsync<int>("PRAGMA user_version");
            Assert.Equal(1, versionBefore);
        }
        finally
        {
            await checkConnection.CloseAsync();
        }

        // Open it through the real production type — this is what actually happens on app
        // startup against an upgraded user's existing database file.
        await using var database = new GmToolkitDatabase(_dbPath);
        var exception = await Record.ExceptionAsync(database.InitializeAsync);
        Assert.Null(exception);

        var versionAfter = await database.Connection.ExecuteScalarAsync<int>("PRAGMA user_version");
        Assert.Equal(GmToolkitDatabase.SchemaVersion, versionAfter);

        var campaignColumnsAfter = await database.Connection.GetTableInfoAsync("Campaigns");
        Assert.Contains(campaignColumnsAfter, c => c.Name == "CharacterSystemId");

        var npcColumnsAfter = await database.Connection.GetTableInfoAsync("Npcs");
        Assert.Contains(npcColumnsAfter, c => c.Name == "StatsJson");

        // Pre-existing data is intact through the real repositories.
        var campaignRepository = new CampaignRepository(database);
        var npcRepository = new NpcRepository(database);

        var fetchedCampaign = await campaignRepository.GetAsync(campaignId);
        Assert.NotNull(fetchedCampaign);
        Assert.Equal("Wandering Souls", fetchedCampaign.Name);
        Assert.Equal("D&D 5e", fetchedCampaign.GameSystem);
        Assert.Equal("A haunted coastal town.", fetchedCampaign.Description);
        // The new column on a pre-existing row has no value to migrate forward — null, i.e. no
        // system attached, is exactly today's unchanged freeform behavior.
        Assert.Null(fetchedCampaign.CharacterSystemId);

        var fetchedNpc = await npcRepository.GetAsync(npcId);
        Assert.NotNull(fetchedNpc);
        Assert.Equal("Old Marta", fetchedNpc.Name);
        Assert.Equal("Innkeeper", fetchedNpc.Role);
        Assert.True(fetchedNpc.KnownToPlayers);
        // Backfilled by the v1->v2 migration step to valid (empty) JSON, not left as SQL NULL.
        Assert.Empty(fetchedNpc.Stats);

        var rawStatsJson = await database.Connection.ExecuteScalarAsync<string>(
            "SELECT StatsJson FROM Npcs WHERE Id = ?", npcId);
        Assert.Equal("{}", rawStatsJson);
    }

    [Table("Campaigns")]
    private sealed class V1CampaignRow
    {
        [PrimaryKey]
        public Guid Id { get; set; }

        [NotNull]
        public string Name { get; set; } = string.Empty;

        public string GameSystem { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedUtc { get; set; }
        public DateTime LastOpenedUtc { get; set; }
    }

    [Table("Npcs")]
    private sealed class V1NpcRow
    {
        [PrimaryKey]
        public Guid Id { get; set; }

        [Indexed]
        public Guid CampaignId { get; set; }

        [Indexed]
        [NotNull]
        public string Name { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;
        public string Faction { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Appearance { get; set; } = string.Empty;
        public string Mannerism { get; set; } = string.Empty;
        public string Motivation { get; set; } = string.Empty;
        public string Secret { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public bool KnownToPlayers { get; set; }
        public DateTime CreatedUtc { get; set; }
        public bool WasGenerated { get; set; }
    }
}