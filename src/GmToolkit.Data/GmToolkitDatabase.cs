using System.Diagnostics;

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

    /// <summary>
    /// Suffixes of sqlite-net-pcl/SQLite sidecar files that can accompany the main database file:
    /// the rollback journal (default journal mode) and the WAL/shared-memory files (if WAL mode
    /// were ever used). These are exactly the files most likely to be left in a stale state when
    /// the main file is corrupt due to an interrupted write, so they're moved aside alongside it.
    /// </summary>
    private static readonly string[] SidecarSuffixes = ["-journal", "-wal", "-shm"];

    /// <summary>
    /// Bootstraps the database at <paramref name="databasePath"/> for first run or ongoing use:
    /// ensures the containing directory exists, then constructs and initializes a
    /// <see cref="GmToolkitDatabase"/>. If the existing file is corrupt or otherwise unreadable
    /// (<see cref="InitializeAsync"/> throws), the offending file (and any <c>-journal</c>,
    /// <c>-wal</c>, or <c>-shm</c> sidecar files that exist alongside it) is renamed aside with a
    /// <c>.corrupt-{timestamp}</c> suffix (never deleted, in case the user wants to recover data
    /// from it later) and a fresh database is created and initialized at the original path. If
    /// that second attempt also throws, the exception propagates — there's nothing else
    /// reasonable to do without an error-display UI (a later milestone).
    /// </summary>
    public static async Task<GmToolkitDatabase> CreateAndInitializeAsync(string databasePath)
    {
        LogResolvedPath(databasePath);

        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var database = new GmToolkitDatabase(databasePath);
        try
        {
            await database.InitializeAsync();
            return database;
        }
        catch
        {
            await database.DisposeAsync();

            if (File.Exists(databasePath))
            {
                var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
                var corruptSuffix = $".corrupt-{timestamp}";
                File.Move(databasePath, $"{databasePath}{corruptSuffix}");

                foreach (var sidecarSuffix in SidecarSuffixes)
                {
                    var sidecarPath = $"{databasePath}{sidecarSuffix}";
                    if (File.Exists(sidecarPath))
                    {
                        File.Move(sidecarPath, $"{sidecarPath}{corruptSuffix}");
                    }
                }
            }

            var recreated = new GmToolkitDatabase(databasePath);
            try
            {
                await recreated.InitializeAsync();
                return recreated;
            }
            catch
            {
                await recreated.DisposeAsync();
                throw;
            }
        }
    }

    [Conditional("DEBUG")]
    private static void LogResolvedPath(string databasePath)
    {
        Debug.WriteLine($"GmToolkitDatabase: resolved database path '{databasePath}'.");
    }
}