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
    /// <see cref="InitializeAsync"/> to bring existing databases forward.
    /// </summary>
    /// <remarks>
    /// v2 (#88): added <c>CampaignRow.CharacterSystemId</c> and <c>NpcRow.StatsJson</c>. Both are
    /// new nullable/defaulted columns, so <see cref="InitializeAsync"/>'s unconditional
    /// <c>CreateTableAsync</c> calls already add them to an existing table on their own
    /// (sqlite-net-pcl's <c>CreateTable</c> diffs the existing schema and runs
    /// <c>ALTER TABLE ... ADD COLUMN</c> for anything missing) — the version-gated step below only
    /// needs to backfill existing <c>Npcs</c> rows' brand-new <c>StatsJson</c> column, which
    /// <c>ALTER TABLE ADD COLUMN</c> leaves as SQL <c>NULL</c>, to the same <c>"{}"</c> empty-but-
    /// valid-JSON default a freshly-inserted row gets.
    /// </remarks>
    public const int SchemaVersion = 2;

    public SQLiteAsyncConnection Connection { get; }

    /// <summary>
    /// The path this database was opened at. Used by <see cref="DatabaseExceptionTranslator"/>
    /// (issue #32) to proactively detect the file disappearing out from under a live connection --
    /// see that type's remarks for why an existence check, not just catching whatever SQLite
    /// itself throws, is necessary to reliably surface that specific failure.
    /// </summary>
    public string DatabasePath { get; }

    public GmToolkitDatabase(string databasePath)
    {
        DatabasePath = databasePath;
        Batteries_V2.Init();
        Connection = new SQLiteAsyncConnection(databasePath);
    }

    public async Task InitializeAsync()
    {
        await Connection.CreateTableAsync<CampaignRow>();
        await Connection.CreateTableAsync<PlayerCharacterRow>();
        await Connection.CreateTableAsync<NpcRow>();

        var currentVersion = await Connection.ExecuteScalarAsync<int>("PRAGMA user_version");

        if (currentVersion < 2)
        {
            // v1 -> v2 (#88): the CreateTableAsync calls above already added the new
            // Npcs.StatsJson column (via ALTER TABLE ADD COLUMN) to a pre-existing database, but
            // that leaves it SQL NULL on every pre-existing row. Backfill to "{}" so every Npc row
            // holds valid JSON, matching the default a newly-inserted row gets, rather than relying
            // on NpcMapper's null-tolerant read path to paper over it forever.
            await Connection.ExecuteAsync("UPDATE Npcs SET StatsJson = '{}' WHERE StatsJson IS NULL");
        }

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