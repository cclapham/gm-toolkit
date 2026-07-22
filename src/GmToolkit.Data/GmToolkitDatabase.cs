using GmToolkit.Data.Rows;

using SQLite;

using SQLitePCL;

namespace GmToolkit.Data;

/// <summary>
/// Owns the SQLite connection and schema for the app's single database file.
/// </summary>
/// <remarks>
/// Cascade delete (Campaign → PCs/NPCs) is done explicitly by the repository (#11), not via
/// SQL foreign keys. sqlite-net-pcl's attribute-driven <c>CreateTableAsync</c> doesn't emit
/// FOREIGN KEY/ON DELETE CASCADE clauses — getting that would mean hand-writing CREATE TABLE
/// SQL instead of using the library's automatic schema generation. For a data model this small,
/// deleting children explicitly in the repository is simpler to reason about than mixing raw
/// schema SQL with attribute-based table creation.
/// </remarks>
public sealed class GmToolkitDatabase : IAsyncDisposable
{
    /// <summary>
    /// Bumped whenever the schema changes, tracked via SQLite's built-in <c>PRAGMA
    /// user_version</c> — sqlite-net-pcl has no migrations tooling of its own. A future schema
    /// change should bump this and add an <c>if (currentVersion &lt; N)</c> step in
    /// <see cref="InitializeAsync"/> to bring existing databases forward. Nothing to do yet;
    /// this is the first version.
    /// </summary>
    public const int SchemaVersion = 1;

    public SQLiteAsyncConnection Connection { get; }

    public GmToolkitDatabase(string databasePath)
    {
        Batteries_V2.Init();
        Connection = new SQLiteAsyncConnection(databasePath);
    }

    public async Task InitializeAsync()
    {
        await Connection.CreateTableAsync<CampaignRow>();
        await Connection.CreateTableAsync<PlayerCharacterRow>();
        await Connection.CreateTableAsync<NpcRow>();

        var currentVersion = await Connection.ExecuteScalarAsync<int>("PRAGMA user_version");
        if (currentVersion < SchemaVersion)
        {
            await Connection.ExecuteAsync($"PRAGMA user_version = {SchemaVersion}");
        }
    }

    public Task CloseAsync() => Connection.CloseAsync();

    public ValueTask DisposeAsync() => new(CloseAsync());
}