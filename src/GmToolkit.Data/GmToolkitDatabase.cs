using System.Diagnostics;

using GmToolkit.Core.Repositories;
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
    /// <c>ALTER TABLE ... ADD COLUMN</c> for anything missing) — the version-gated step in
    /// <see cref="InitializeAsync"/> only needs to backfill existing <c>Npcs</c> rows' brand-new
    /// <c>StatsJson</c> column, which <c>ALTER TABLE ADD COLUMN</c> leaves as SQL <c>NULL</c>, to
    /// the same <c>"{}"</c> empty-but-valid-JSON default a freshly-inserted row gets. See
    /// <see cref="InitializeAsync"/>'s remarks for how failures during a migration are handled —
    /// that's the template future migrations should follow too.
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

    /// <summary>
    /// Creates the schema (or brings an existing database up to it) and, if the database was
    /// behind <see cref="SchemaVersion"/>, migrates it forward.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>CreateTableAsync</c> calls below are themselves part of the migration for existing
    /// databases, not just first-run schema creation: sqlite-net-pcl's <c>CreateTable</c> diffs
    /// the existing schema and runs <c>ALTER TABLE ... ADD COLUMN</c> for anything missing, which
    /// is how a pre-existing database picks up new columns like v2's <c>Npcs.StatsJson</c>. Any of
    /// those calls, or the version-gated backfill/<c>PRAGMA user_version</c> step below, can throw
    /// -- e.g. the disk is full (<see cref="SQLite3.Result.Full"/>), the file is temporarily
    /// read-only (<see cref="SQLite3.Result.ReadOnly"/>), or another process/handle has it briefly
    /// locked (<see cref="SQLite3.Result.Busy"/>) -- and this method does not itself distinguish
    /// those from genuine file corruption or retry anything; <see cref="CreateAndInitializeAsync"/>
    /// is the one place that decides what a failure here means (see its remarks).
    /// </para>
    /// <para>
    /// The version-gated backfill and the <c>PRAGMA user_version</c> bump that records it succeeded
    /// run inside a single <see cref="SQLiteAsyncConnection.RunInTransactionAsync"/> call, so the
    /// two can never observably diverge -- a process crash or failure partway through can't leave
    /// data backfilled but the version still reading "not yet migrated" (or vice versa). This is the
    /// template future schema migrations should follow.
    /// </para>
    /// </remarks>
    public async Task InitializeAsync()
    {
        await Connection.CreateTableAsync<CampaignRow>();
        await Connection.CreateTableAsync<PlayerCharacterRow>();
        await Connection.CreateTableAsync<NpcRow>();

        var currentVersion = await Connection.ExecuteScalarAsync<int>("PRAGMA user_version");

        if (currentVersion < SchemaVersion)
        {
            await Connection.RunInTransactionAsync(connection =>
            {
                if (currentVersion < 2)
                {
                    // v1 -> v2 (#88): the CreateTableAsync calls above already added the new
                    // Npcs.StatsJson column (via ALTER TABLE ADD COLUMN) to a pre-existing
                    // database, but that leaves it SQL NULL on every pre-existing row. Backfill to
                    // "{}" so every Npc row holds valid JSON, matching the default a newly-inserted
                    // row gets, rather than relying on NpcMapper's null-tolerant read path to paper
                    // over it forever.
                    connection.Execute("UPDATE Npcs SET StatsJson = '{}' WHERE StatsJson IS NULL");
                }

                connection.Execute($"PRAGMA user_version = {SchemaVersion}");
            });
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
    /// <see cref="GmToolkitDatabase"/>. If <see cref="InitializeAsync"/> throws with a genuinely
    /// corrupt file (<see cref="DatabaseExceptionTranslator.IsCorruption"/>), the offending file
    /// (and any <c>-journal</c>, <c>-wal</c>, or <c>-shm</c> sidecar files that exist alongside it)
    /// is renamed aside with a <c>.corrupt-{timestamp}</c> suffix (never deleted, in case the user
    /// wants to recover data from it later) and a fresh database is created and initialized at the
    /// original path. If that second attempt also throws, the exception (always a
    /// <see cref="DataAccessException"/>, per <see cref="DatabaseExceptionTranslator.ToFriendly"/>)
    /// propagates -- see this method's remarks for what the caller is expected to do with it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Any other failure -- e.g. the disk is full (<see cref="SQLite3.Result.Full"/>), the file is
    /// temporarily read-only (<see cref="SQLite3.Result.ReadOnly"/>), or another process/handle has
    /// it briefly locked (<see cref="SQLite3.Result.Busy"/>) -- is <b>not</b> treated as grounds to
    /// move the file aside, whether it happened in <see cref="InitializeAsync"/>'s
    /// <c>CreateTableAsync</c> calls or its version-gated migration step. The existing file might be
    /// perfectly healthy; deleting/relocating it on a guess would destroy a GM's campaign over what
    /// may just be a full disk or a lock held for a moment too long. Instead it's translated to a
    /// friendly <see cref="DataAccessException"/> and thrown, leaving the file exactly as it was.
    /// </para>
    /// <para>
    /// <b>The caller is expected to catch <see cref="DataAccessException"/> and display
    /// <see cref="Exception.Message"/> to the user, not let it propagate as an unhandled
    /// exception.</b> Both heads' composition roots (<c>GmToolkit.Desktop/Program.cs</c> and
    /// <c>GmToolkit.Android/Application.cs</c>) call this before any window/view exists, so there is
    /// no view model or DI container yet to route the error through the app's normal
    /// error-display paths (<c>INotificationService</c> toasts, inline <c>SaveError</c> text, etc.)
    /// -- both catch this specific exception and set <c>GmToolkit.UI.App.StartupError</c> (see that
    /// property's remarks), which makes Avalonia's normal startup path show a dedicated friendly
    /// screen carrying the message instead of the usual splash/shell. There is nothing to
    /// automatically retry in-process: the failure is an external, transient condition (free disk
    /// space, restore write access, close another copy holding the lock, etc.), so the screen's
    /// only action today is to close, and the user relaunches once the condition is cleared --
    /// tracked as a follow-up in issue #123 (retry-in-place instead of a full relaunch).
    /// </para>
    /// </remarks>
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
        catch (Exception ex)
        {
            await database.DisposeAsync();

            if (!DatabaseExceptionTranslator.IsCorruption(ex))
            {
                throw new DataAccessException(DatabaseExceptionTranslator.ToFriendly(ex).Message, ex);
            }

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