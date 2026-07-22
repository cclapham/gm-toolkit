using GmToolkit.Data;

namespace GmToolkit.Data.Tests;

public class GmToolkitDatabaseTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
    private GmToolkitDatabase _database = null!;

    public async Task InitializeAsync()
    {
        _database = new GmToolkitDatabase(_dbPath);
        await _database.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    [Theory]
    [InlineData("Campaigns")]
    [InlineData("PlayerCharacters")]
    [InlineData("Npcs")]
    public async Task All_tables_exist(string tableName)
    {
        var count = await _database.Connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = ?", tableName);

        Assert.Equal(1, count);
    }

    [Theory]
    [InlineData("PlayerCharacters", "CampaignId")]
    [InlineData("Npcs", "CampaignId")]
    [InlineData("Npcs", "Name")]
    public async Task Expected_indexes_exist(string tableName, string columnName)
    {
        var indexSql = await _database.Connection.QueryAsync<IndexSqlRow>(
            "SELECT sql AS Sql FROM sqlite_master WHERE type = 'index' AND tbl_name = ?", tableName);

        Assert.Contains(indexSql, row => row.Sql != null && row.Sql.Contains(columnName, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Schema_version_pragma_is_set_after_initialization()
    {
        var version = await _database.Connection.ExecuteScalarAsync<int>("PRAGMA user_version");

        Assert.Equal(GmToolkitDatabase.SchemaVersion, version);
    }

    [Fact]
    public async Task Initializing_twice_against_the_same_file_does_not_throw()
    {
        await _database.InitializeAsync();
    }

    private class IndexSqlRow
    {
        public string? Sql { get; set; }
    }
}