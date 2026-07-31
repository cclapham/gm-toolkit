using GmToolkit.Core.Repositories;
using GmToolkit.Data.Rows;

namespace GmToolkit.Data.Tests;

public class GmToolkitDatabaseBootstrapTests : IAsyncLifetime
{
    private readonly string _rootDirectory = Path.Combine(Path.GetTempPath(), $"gmtoolkit-bootstrap-tests-{Guid.NewGuid()}");

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_rootDirectory);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task Fresh_path_with_missing_parent_directory_creates_a_working_database()
    {
        var dbPath = Path.Combine(_rootDirectory, "nested", "does-not-exist-yet", "gmtoolkit.db");
        Assert.False(Directory.Exists(Path.GetDirectoryName(dbPath)));

        await using var database = await GmToolkitDatabase.CreateAndInitializeAsync(dbPath);

        Assert.True(Directory.Exists(Path.GetDirectoryName(dbPath)));
        Assert.True(File.Exists(dbPath));

        var version = await database.Connection.ExecuteScalarAsync<int>("PRAGMA user_version");
        Assert.Equal(GmToolkitDatabase.SchemaVersion, version);

        var campaignTableCount = await database.Connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = ?", "Campaigns");
        Assert.Equal(1, campaignTableCount);
    }

    [Fact]
    public async Task Corrupt_file_is_moved_aside_and_a_fresh_working_database_is_created()
    {
        var dbPath = Path.Combine(_rootDirectory, "gmtoolkit.db");
        var garbage = "this is not a valid sqlite file"u8.ToArray();
        await File.WriteAllBytesAsync(dbPath, garbage);

        await using var database = await GmToolkitDatabase.CreateAndInitializeAsync(dbPath);

        // The resulting database is usable: schema exists and we can round-trip a row.
        var campaign = new CampaignRow
        {
            Id = Guid.NewGuid(),
            Name = "Test Campaign",
            GameSystem = "Generic",
        };
        await database.Connection.InsertAsync(campaign);
        var fetched = await database.Connection.Table<CampaignRow>().Where(c => c.Id == campaign.Id).FirstOrDefaultAsync();
        Assert.NotNull(fetched);
        Assert.Equal("Test Campaign", fetched!.Name);

        // The original (corrupt) file was renamed aside, not deleted, so it can be recovered.
        var siblingFiles = Directory.GetFiles(_rootDirectory, "gmtoolkit.db.corrupt-*");
        Assert.Single(siblingFiles);
        var corruptBytes = await File.ReadAllBytesAsync(siblingFiles[0]);
        Assert.Equal(garbage, corruptBytes);
    }

    /// <summary>
    /// Reaches <see cref="GmToolkitDatabase.CreateAndInitializeAsync"/>'s sidecar move-aside loop
    /// with a genuinely stale sidecar still present next to a corrupt main file -- the one scenario
    /// the loop exists for, and the one the <c>chmod 000</c> variant of this test used to exercise
    /// before <see cref="SQLite.SQLite3.Result.CannotOpen"/> was reclassified as non-corruption
    /// (see the sibling <see cref="Entirely_inaccessible_main_file_is_left_untouched_and_surfaces_a_friendly_error"/>
    /// test).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses a stale <c>-shm</c> sidecar on its own (no accompanying <c>-journal</c>/<c>-wal</c>),
    /// not any of the other two suffixes -- confirmed empirically (a throwaway console app driving
    /// this exact code path) while writing this test that the other two never actually reach the
    /// loop: a stale <c>-journal</c> sibling is already deleted by SQLite's own open() as a side
    /// effect of attempting (and failing) to open a corrupt (<c>NOTADB</c>) main file, before this
    /// method's <c>catch</c> block even runs; a stale <c>-wal</c> sibling survives that failed
    /// open, but is then deleted as a side effect of this method's own
    /// <c>await database.DisposeAsync()</c> a few lines into that same <c>catch</c> block (SQLite's
    /// <c>sqlite3_close</c> cleans up a connection's WAL file on close, even for a connection whose
    /// open never actually succeeded) -- in both cases, by the time the sidecar loop runs, the file
    /// is already gone and the loop's <c>File.Exists</c> check is <c>false</c>.
    /// </para>
    /// <para>
    /// A stale <c>-shm</c> sidecar with no accompanying <c>-wal</c>, however, survives both of
    /// those -- SQLite's WAL-related cleanup on a failed open/close only kicks in when it also sees
    /// a <c>-wal</c> file for the same connection, so an orphaned <c>-shm</c> with nothing else next
    /// to it is genuinely still sitting there when the loop runs. This is an admittedly narrow
    /// window (some prior process would have to have died leaving only a <c>-shm</c> behind, not
    /// the more common <c>-wal</c>+<c>-shm</c> pair), but it's a real one, not a hypothetical --
    /// hence this test rather than deleting the loop as dead code.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Corrupt_file_with_a_stale_shm_sidecar_moves_both_aside()
    {
        var dbPath = Path.Combine(_rootDirectory, "gmtoolkit.db");
        var garbage = "this is not a valid sqlite file"u8.ToArray();
        await File.WriteAllBytesAsync(dbPath, garbage);

        var shmPath = $"{dbPath}-shm";
        var shmBytes = "stale shm"u8.ToArray();
        await File.WriteAllBytesAsync(shmPath, shmBytes);

        await using var database = await GmToolkitDatabase.CreateAndInitializeAsync(dbPath);

        // The resulting database is usable: schema exists and we can round-trip a row.
        var version = await database.Connection.ExecuteScalarAsync<int>("PRAGMA user_version");
        Assert.Equal(GmToolkitDatabase.SchemaVersion, version);

        // The main corrupt file was moved aside, same as the sidecar-free case above.
        var corruptMainFiles = Directory.GetFiles(_rootDirectory, "gmtoolkit.db.corrupt-*");
        Assert.Single(corruptMainFiles);
        var corruptSuffix = Path.GetFileName(corruptMainFiles[0])["gmtoolkit.db".Length..];

        // The stale -shm sidecar was moved aside too, with the same corrupt-suffix/timestamp, not
        // deleted and not left behind next to the fresh database.
        var corruptShmPath = $"{shmPath}{corruptSuffix}";
        Assert.True(File.Exists(corruptShmPath));
        Assert.Equal(shmBytes, await File.ReadAllBytesAsync(corruptShmPath));
        Assert.False(File.Exists(shmPath));
    }

    /// <summary>
    /// A main database file that's entirely inaccessible (e.g. every permission bit revoked) fails
    /// with <see cref="SQLite.SQLite3.Result.CannotOpen"/> -- a result <see
    /// cref="DatabaseExceptionTranslator.IsCorruption"/> deliberately does <b>not</b> classify as
    /// file corruption (its own bytes might be perfectly healthy; the app just can't currently read
    /// them). So, like a transient busy/full/read-only failure, this must not move the file aside
    /// and replace it with an empty database -- it must surface as a friendly, actionable error and
    /// leave the file (and any sidecars next to it) exactly as they were, so restoring access to the
    /// file (e.g. fixing its permissions) is enough to recover it on the next launch.
    /// </summary>
    [Fact]
    public async Task Entirely_inaccessible_main_file_is_left_untouched_and_surfaces_a_friendly_error()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var dbPath = Path.Combine(_rootDirectory, "gmtoolkit.db");
        var garbage = "this is not a valid sqlite file"u8.ToArray();
        await File.WriteAllBytesAsync(dbPath, garbage);

        var journalPath = $"{dbPath}-journal";
        var journalBytes = "stale journal"u8.ToArray();
        await File.WriteAllBytesAsync(journalPath, journalBytes);

        File.SetUnixFileMode(dbPath, UnixFileMode.None);

        try
        {
            var exception = await Record.ExceptionAsync(() => GmToolkitDatabase.CreateAndInitializeAsync(dbPath));

            Assert.IsType<DataAccessException>(exception);

            // Nothing was moved aside -- neither the main file nor its sidecar.
            var corruptSiblings = Directory.GetFiles(_rootDirectory, "*.corrupt-*");
            Assert.Empty(corruptSiblings);
            Assert.True(File.Exists(journalPath));
            Assert.Equal(journalBytes, await File.ReadAllBytesAsync(journalPath));
        }
        finally
        {
            File.SetUnixFileMode(dbPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Fact]
    public async Task Re_running_against_an_already_initialized_valid_file_works_fine()
    {
        var dbPath = Path.Combine(_rootDirectory, "gmtoolkit.db");

        await using (var first = await GmToolkitDatabase.CreateAndInitializeAsync(dbPath))
        {
            var version = await first.Connection.ExecuteScalarAsync<int>("PRAGMA user_version");
            Assert.Equal(GmToolkitDatabase.SchemaVersion, version);
        }

        await using var second = await GmToolkitDatabase.CreateAndInitializeAsync(dbPath);

        var secondVersion = await second.Connection.ExecuteScalarAsync<int>("PRAGMA user_version");
        Assert.Equal(GmToolkitDatabase.SchemaVersion, secondVersion);

        var campaignTableCount = await second.Connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = ?", "Campaigns");
        Assert.Equal(1, campaignTableCount);

        // No corrupt-file sidecar should have been created for a healthy re-open.
        var siblingFiles = Directory.GetFiles(_rootDirectory, "*.corrupt-*");
        Assert.Empty(siblingFiles);
    }
}