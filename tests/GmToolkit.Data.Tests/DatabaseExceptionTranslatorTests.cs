using GmToolkit.Core.Models;
using GmToolkit.Core.Repositories;
using GmToolkit.Data.Repositories;

namespace GmToolkit.Data.Tests;

/// <summary>
/// Exercises <see cref="DatabaseExceptionTranslator"/> (issue #32) through the public repository
/// methods that route through it -- there's no <c>InternalsVisibleTo</c> from this test project
/// into <c>GmToolkit.Data</c> for that internal type itself, and testing it this way is arguably
/// the more valuable proof anyway: it's exactly the code path a real repository call takes.
/// </summary>
public class DatabaseExceptionTranslatorTests : IAsyncLifetime
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

    [Fact]
    public async Task AddAsync_after_the_database_file_is_deleted_mid_session_throws_a_friendly_DataAccessException()
    {
        // Simulates issue #32's acceptance criterion: "killing the DB file while the app runs" --
        // the connection stays open (constructed in InitializeAsync above, same as the real app's
        // GmToolkitDatabase, which is opened once at startup and never reopened mid-session), then
        // the file disappears out from under it.
        File.Delete(_dbPath);

        var exception = await Record.ExceptionAsync(() => _repository.AddAsync(new Campaign { Name = "Wandering Souls" }));

        var dataAccessException = Assert.IsType<DataAccessException>(exception);
        Assert.Contains("missing", dataAccessException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Restart GM Toolkit", dataAccessException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAllAsync_after_the_database_file_is_deleted_mid_session_throws_a_friendly_DataAccessException()
    {
        File.Delete(_dbPath);

        var exception = await Record.ExceptionAsync(() => _repository.GetAllAsync());

        Assert.IsType<DataAccessException>(exception);
    }

    [Fact]
    public async Task DeleteAsync_after_the_database_file_is_deleted_mid_session_throws_a_friendly_DataAccessException()
    {
        var campaign = new Campaign { Name = "Wandering Souls" };
        await _repository.AddAsync(campaign);
        File.Delete(_dbPath);

        var exception = await Record.ExceptionAsync(() => _repository.DeleteAsync(campaign.Id));

        Assert.IsType<DataAccessException>(exception);
    }

    [Fact]
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

    [Fact]
    public async Task PlayerCharacterRepository_AddAsync_after_the_database_file_is_deleted_mid_session_throws_a_friendly_DataAccessException()
    {
        var playerCharacterRepository = new PlayerCharacterRepository(_database);
        File.Delete(_dbPath);

        var exception = await Record.ExceptionAsync(() =>
            playerCharacterRepository.AddAsync(new PlayerCharacter { CampaignId = Guid.NewGuid(), CharacterName = "Brannigan" }));

        Assert.IsType<DataAccessException>(exception);
    }

    [Fact]
    public async Task NpcRepository_AddAsync_after_the_database_file_is_deleted_mid_session_throws_a_friendly_DataAccessException()
    {
        var npcRepository = new NpcRepository(_database);
        File.Delete(_dbPath);

        var exception = await Record.ExceptionAsync(() =>
            npcRepository.AddAsync(new Npc { CampaignId = Guid.NewGuid(), Name = "Old Marta" }));

        Assert.IsType<DataAccessException>(exception);
    }
}