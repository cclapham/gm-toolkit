using GmToolkit.Core.Models;
using GmToolkit.Core.Repositories;
using GmToolkit.Data.Repositories;

namespace GmToolkit.Data.Tests;

/// <summary>
/// Exercises <see cref="DatabaseExceptionTranslator"/> (issue #32) end-to-end, through the public
/// repository methods that route through it, against a real <see cref="GmToolkitDatabase"/> whose
/// connection stays open for the whole test -- exactly the shape a real repository call takes in
/// the running app.
/// </summary>
/// <remarks>
/// <para>
/// These tests simulate issue #32's acceptance criterion ("killing the DB file while the app runs
/// produces a comprehensible message, not a crash") for real, by calling
/// <see cref="File.Delete(string)"/> on the database file while <see cref="_repository"/>'s
/// connection is still open. That's safe on Linux/macOS: POSIX allows unlinking a file that's still
/// open (the file's data stays available to existing open handles, but <see cref="File.Exists"/>
/// correctly reports false, which is exactly what <see cref="DatabaseExceptionTranslator"/>'s
/// proactive existence check needs to trigger).
/// </para>
/// <para>
/// <b>It is not safe on Windows.</b> sqlite-net-pcl's native connection there doesn't open the file
/// with share-delete semantics, so <see cref="File.Delete(string)"/> itself throws
/// <see cref="IOException"/> ("The process cannot access the file ... because it is being used by
/// another process") before a test even reaches the code under test -- a genuine platform
/// difference in how Windows locks open files, not a bug in these tests or in
/// <see cref="DatabaseExceptionTranslator"/>. Every <see cref="Fact"/> below is therefore a
/// <see cref="SkipOnWindowsFactAttribute"/> instead, so Linux/macOS CI keeps proving the true
/// end-to-end scenario for real, while Windows CI skips just the specific "delete a file out from
/// under a live OS-level lock" simulation. <see cref="DatabaseExceptionTranslatorTests"/> covers
/// the same translation logic in isolation, in a way that runs (and passes) on every platform.
/// </para>
/// </remarks>
public class DatabaseExceptionTranslatorIntegrationTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
    private GmToolkitDatabase _database = null!;
    private CampaignRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _database = new GmToolkitDatabase(_dbPath);
        await _database.InitializeAsync();
        _repository = new CampaignRepository(_database);
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    [SkipOnWindowsFact]
    public async Task AddAsync_after_the_database_file_is_deleted_mid_session_throws_a_friendly_DataAccessException()
    {
        // The connection stays open (constructed in InitializeAsync above, same as the real app's
        // GmToolkitDatabase, which is opened once at startup and never reopened mid-session), then
        // the file disappears out from under it.
        File.Delete(_dbPath);

        var exception = await Record.ExceptionAsync(() => _repository.AddAsync(new Campaign { Name = "Wandering Souls" }));

        var dataAccessException = Assert.IsType<DataAccessException>(exception);
        Assert.Contains("missing", dataAccessException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Restart GM Toolkit", dataAccessException.Message, StringComparison.Ordinal);
    }

    [SkipOnWindowsFact]
    public async Task GetAllAsync_after_the_database_file_is_deleted_mid_session_throws_a_friendly_DataAccessException()
    {
        File.Delete(_dbPath);

        var exception = await Record.ExceptionAsync(() => _repository.GetAllAsync());

        Assert.IsType<DataAccessException>(exception);
    }

    [SkipOnWindowsFact]
    public async Task DeleteAsync_after_the_database_file_is_deleted_mid_session_throws_a_friendly_DataAccessException()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        await _repository.AddAsync(campaign);
        File.Delete(_dbPath);

        var exception = await Record.ExceptionAsync(() => _repository.DeleteAsync(campaign.Id));

        Assert.IsType<DataAccessException>(exception);
    }

    [SkipOnWindowsFact]
    public async Task Once_the_database_file_reappears_the_missing_file_error_stops_being_thrown()
    {
        // Not a real recovery path the app itself drives (nothing currently re-creates the file
        // mid-session) -- just confirms the existence check is re-evaluated on every call rather
        // than a one-time "poisoned forever" flag latched the first time it throws.
        File.Delete(_dbPath);
        var missingFileException = Assert.IsType<DataAccessException>(await Record.ExceptionAsync(() => _repository.GetAllAsync()));
        Assert.Contains("missing", missingFileException.Message, StringComparison.OrdinalIgnoreCase);

        // Recreating a file at the same path (even an empty one, not a real SQLite database)
        // satisfies the File.Exists gate -- what happens next is served entirely from the pooled
        // connection's own warm page cache (see DatabaseExceptionTranslator's remarks), which is
        // exactly the "doesn't reliably notice the file changed" behavior that gate exists to
        // catch in the first place.
        File.WriteAllBytes(_dbPath, []);

        var exception = await Record.ExceptionAsync(() => _repository.GetAllAsync());

        Assert.Null(exception);
    }

    [SkipOnWindowsFact]
    public async Task PlayerCharacterRepository_AddAsync_after_the_database_file_is_deleted_mid_session_throws_a_friendly_DataAccessException()
    {
        var playerCharacterRepository = new PlayerCharacterRepository(_database);
        File.Delete(_dbPath);

        var exception = await Record.ExceptionAsync(() =>
            playerCharacterRepository.AddAsync(new PlayerCharacter { CampaignId = Guid.NewGuid(), CharacterName = "Brannigan" }));

        Assert.IsType<DataAccessException>(exception);
    }

    [SkipOnWindowsFact]
    public async Task NpcRepository_AddAsync_after_the_database_file_is_deleted_mid_session_throws_a_friendly_DataAccessException()
    {
        var npcRepository = new NpcRepository(_database);
        File.Delete(_dbPath);

        var exception = await Record.ExceptionAsync(() =>
            npcRepository.AddAsync(new Npc { CampaignId = Guid.NewGuid(), Name = "Old Marta" }));

        Assert.IsType<DataAccessException>(exception);
    }
}