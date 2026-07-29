using GmToolkit.Core.Repositories;

using SQLite;

namespace GmToolkit.Data.Tests;

/// <summary>
/// Exercises <see cref="DatabaseExceptionTranslator"/>'s translation logic (issue #32) directly and
/// in isolation, rather than through a live <see cref="GmToolkitDatabase"/>/repository chain that
/// holds a real, currently-open SQLite connection. This is deliberately platform-agnostic: every
/// <see cref="GmToolkitDatabase"/> constructed below is never
/// <see cref="GmToolkitDatabase.InitializeAsync"/>'d, and its <see cref="GmToolkitDatabase.Connection"/>
/// is never touched, so the file backing <see cref="GmToolkitDatabase.DatabasePath"/> is either
/// never created at all, or just a plain, never-opened file on disk -- there's no live OS-level
/// SQLite file lock for these tests' own <see cref="File.Delete(string)"/>/<see cref="File.WriteAllBytes(string,byte[])"/>
/// calls to fight with, unlike <see cref="DatabaseExceptionTranslatorIntegrationTests"/>'s
/// end-to-end tests (which do hold a real, live connection, and are Windows-skipped precisely
/// because deleting a file out from under a real open SQLite connection behaves differently there).
/// GmToolkit.Data's <c>AssemblyInfo.cs</c> grants this test project <c>InternalsVisibleTo</c> access
/// so it can call the internal <see cref="DatabaseExceptionTranslator"/> type directly.
/// </summary>
public class DatabaseExceptionTranslatorTests
{
    [Fact]
    public async Task RunAsync_when_the_database_file_is_missing_throws_a_friendly_DataAccessException_without_invoking_the_operation()
    {
        var database = new GmToolkitDatabase(NeverCreatedPath());
        var operationInvoked = false;

        var exception = await Record.ExceptionAsync(() => DatabaseExceptionTranslator.RunAsync(database, () =>
        {
            operationInvoked = true;
            return Task.CompletedTask;
        }));

        var dataAccessException = Assert.IsType<DataAccessException>(exception);
        Assert.Contains("missing", dataAccessException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Restart GM Toolkit", dataAccessException.Message, StringComparison.Ordinal);
        Assert.False(operationInvoked);
    }

    [Fact]
    public async Task RunAsyncOfT_when_the_database_file_is_missing_throws_a_friendly_DataAccessException_without_invoking_the_operation()
    {
        var database = new GmToolkitDatabase(NeverCreatedPath());
        var operationInvoked = false;

        var exception = await Record.ExceptionAsync(() => DatabaseExceptionTranslator.RunAsync(database, () =>
        {
            operationInvoked = true;
            return Task.FromResult(42);
        }));

        var dataAccessException = Assert.IsType<DataAccessException>(exception);
        Assert.Contains("missing", dataAccessException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(operationInvoked);
    }

    [Fact]
    public async Task RunAsync_when_the_database_file_exists_invokes_the_operation_and_does_not_throw()
    {
        var path = CreateEmptyFile();
        try
        {
            var database = new GmToolkitDatabase(path);
            var operationInvoked = false;

            var exception = await Record.ExceptionAsync(() => DatabaseExceptionTranslator.RunAsync(database, () =>
            {
                operationInvoked = true;
                return Task.CompletedTask;
            }));

            Assert.Null(exception);
            Assert.True(operationInvoked);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Once_a_missing_database_file_reappears_the_translator_stops_throwing()
    {
        // Not a real recovery path the app itself drives (nothing currently re-creates the file
        // mid-session) -- just confirms the existence check is re-evaluated on every call rather
        // than a one-time "poisoned forever" flag latched the first time it throws.
        var path = NeverCreatedPath();
        var database = new GmToolkitDatabase(path);

        try
        {
            var missingFileException = await Record.ExceptionAsync(() => DatabaseExceptionTranslator.RunAsync(database, () => Task.CompletedTask));
            var dataAccessException = Assert.IsType<DataAccessException>(missingFileException);
            Assert.Contains("missing", dataAccessException.Message, StringComparison.OrdinalIgnoreCase);

            File.WriteAllBytes(path, []);

            var exception = await Record.ExceptionAsync(() => DatabaseExceptionTranslator.RunAsync(database, () => Task.CompletedTask));

            Assert.Null(exception);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RunAsync_does_not_rewrap_a_DataAccessException_thrown_directly_by_the_operation()
    {
        var path = CreateEmptyFile();
        try
        {
            var database = new GmToolkitDatabase(path);
            var original = new DataAccessException("already friendly");

            var exception = await Record.ExceptionAsync(() => DatabaseExceptionTranslator.RunAsync(database, () => throw original));

            Assert.Same(original, exception);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(SQLite3.Result.CannotOpen, "couldn't be opened")]
    [InlineData(SQLite3.Result.NotFound, "couldn't be opened")]
    [InlineData(SQLite3.Result.Corrupt, "corrupted")]
    [InlineData(SQLite3.Result.NonDBFile, "corrupted")]
    [InlineData(SQLite3.Result.Format, "corrupted")]
    [InlineData(SQLite3.Result.Full, "no space left")]
    [InlineData(SQLite3.Result.Busy, "locked")]
    [InlineData(SQLite3.Result.Locked, "locked")]
    [InlineData(SQLite3.Result.LockErr, "locked")]
    [InlineData(SQLite3.Result.ReadOnly, "permission")]
    [InlineData(SQLite3.Result.Perm, "permission")]
    [InlineData(SQLite3.Result.AccessDenied, "permission")]
    [InlineData(SQLite3.Result.IOError, "disk error")]
    [InlineData(SQLite3.Result.Misuse, "Something went wrong")]
    public async Task RunAsync_translates_a_SQLiteException_of_the_given_result_into_the_matching_friendly_message(
        SQLite3.Result result, string expectedMessageFragment)
    {
        var path = CreateEmptyFile();
        try
        {
            var database = new GmToolkitDatabase(path);

            var exception = await Record.ExceptionAsync(() => DatabaseExceptionTranslator.RunAsync(
                database, () => throw SQLiteException.New(result, "native sqlite failure")));

            var dataAccessException = Assert.IsType<DataAccessException>(exception);
            Assert.Contains(expectedMessageFragment, dataAccessException.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RunAsync_translates_an_UnauthorizedAccessException_into_a_friendly_permission_message()
    {
        var path = CreateEmptyFile();
        try
        {
            var database = new GmToolkitDatabase(path);

            var exception = await Record.ExceptionAsync(() => DatabaseExceptionTranslator.RunAsync(
                database, () => throw new UnauthorizedAccessException("denied")));

            var dataAccessException = Assert.IsType<DataAccessException>(exception);
            Assert.Contains("permission", dataAccessException.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RunAsync_translates_an_IOException_into_a_friendly_disk_error_message()
    {
        var path = CreateEmptyFile();
        try
        {
            var database = new GmToolkitDatabase(path);

            var exception = await Record.ExceptionAsync(() => DatabaseExceptionTranslator.RunAsync(
                database, () => throw new IOException("boom")));

            var dataAccessException = Assert.IsType<DataAccessException>(exception);
            Assert.Contains("disk error", dataAccessException.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RunAsync_translates_an_unrecognized_exception_into_a_generic_message_that_includes_the_original_text()
    {
        var path = CreateEmptyFile();
        try
        {
            var database = new GmToolkitDatabase(path);

            var exception = await Record.ExceptionAsync(() => DatabaseExceptionTranslator.RunAsync(
                database, () => throw new InvalidOperationException("something unexpected")));

            var dataAccessException = Assert.IsType<DataAccessException>(exception);
            Assert.Contains("Something went wrong talking to the database", dataAccessException.Message, StringComparison.Ordinal);
            Assert.Contains("something unexpected", dataAccessException.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string NeverCreatedPath() => Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");

    private static string CreateEmptyFile()
    {
        var path = NeverCreatedPath();
        File.WriteAllBytes(path, []);
        return path;
    }
}