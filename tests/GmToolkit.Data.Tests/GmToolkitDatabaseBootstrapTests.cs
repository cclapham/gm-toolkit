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

    [Fact]
    public async Task Stale_sidecar_files_are_moved_aside_alongside_an_unreadable_main_file()
    {
        // This test only runs on Unix-like platforms: it needs a way to make SQLite's own file
        // open fail *before* SQLite gets a chance to run its normal hot-journal-rollback recovery
        // (which, on its own, already deletes a stale "-journal" sibling as an unrelated side
        // effect of successfully opening a merely-corrupt-content file — see the sibling test
        // above, where the main file's bytes are invalid but still readable). Revoking read/write
        // permission on the main file via chmod reproduces the "otherwise unreadable" case from
        // this method's doc comment: SQLite's open() fails immediately, before touching the
        // journal at all, which is exactly the scenario where a stale "-journal"/"-wal"/"-shm"
        // sibling would genuinely still be sitting there for this fix's recovery code to move.
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var dbPath = Path.Combine(_rootDirectory, "gmtoolkit.db");
        var garbage = "this is not a valid sqlite file"u8.ToArray();
        await File.WriteAllBytesAsync(dbPath, garbage);

        var journalPath = $"{dbPath}-journal";
        var walPath = $"{dbPath}-wal";
        var shmPath = $"{dbPath}-shm";
        var journalBytes = "stale journal"u8.ToArray();
        var walBytes = "stale wal"u8.ToArray();
        var shmBytes = "stale shm"u8.ToArray();
        await File.WriteAllBytesAsync(journalPath, journalBytes);
        await File.WriteAllBytesAsync(walPath, walBytes);
        await File.WriteAllBytesAsync(shmPath, shmBytes);

        File.SetUnixFileMode(dbPath, UnixFileMode.None);

        await using var database = await GmToolkitDatabase.CreateAndInitializeAsync(dbPath);

        // The resulting database is usable.
        var version = await database.Connection.ExecuteScalarAsync<int>("PRAGMA user_version");
        Assert.Equal(GmToolkitDatabase.SchemaVersion, version);

        // None of the stale sidecar files were left behind next to the fresh database.
        Assert.False(File.Exists(journalPath));
        Assert.False(File.Exists(walPath));
        Assert.False(File.Exists(shmPath));

        // The main corrupt file was moved aside.
        var corruptMainFiles = Directory.GetFiles(_rootDirectory, "gmtoolkit.db.corrupt-*");
        Assert.Single(corruptMainFiles);
        var corruptSuffix = Path.GetFileName(corruptMainFiles[0])["gmtoolkit.db".Length..];

        // Each sidecar file was moved aside with the same corrupt-suffix/timestamp, not deleted.
        var corruptJournalPath = $"{journalPath}{corruptSuffix}";
        var corruptWalPath = $"{walPath}{corruptSuffix}";
        var corruptShmPath = $"{shmPath}{corruptSuffix}";
        Assert.True(File.Exists(corruptJournalPath));
        Assert.True(File.Exists(corruptWalPath));
        Assert.True(File.Exists(corruptShmPath));

        Assert.Equal(journalBytes, await File.ReadAllBytesAsync(corruptJournalPath));
        Assert.Equal(walBytes, await File.ReadAllBytesAsync(corruptWalPath));
        Assert.Equal(shmBytes, await File.ReadAllBytesAsync(corruptShmPath));
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