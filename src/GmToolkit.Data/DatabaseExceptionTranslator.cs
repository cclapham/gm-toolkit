using GmToolkit.Core.Repositories;

using SQLite;

namespace GmToolkit.Data;

/// <summary>
/// Translates whatever a repository call into <see cref="GmToolkitDatabase.Connection"/> throws
/// into a <see cref="DataAccessException"/> with a message a GM can actually understand and act on
/// (issue #32's "friendly messages for DB failures: disk full, file locked, corrupt" task) --
/// every method on <see cref="Repositories.CampaignRepository"/>/
/// <see cref="Repositories.PlayerCharacterRepository"/>/<see cref="Repositories.NpcRepository"/>
/// routes through <see cref="RunAsync(GmToolkitDatabase,Func{Task})"/> or its generic overload
/// rather than calling <see cref="GmToolkitDatabase.Connection"/> directly, so this is the one place
/// that owns the translation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Proactively checks <see cref="File.Exists"/> before every call, rather than relying solely on
/// whatever exception (if any) the operation itself throws.</b> This matters specifically for
/// issue #32's acceptance criterion ("killing the DB file while the app runs produces a
/// comprehensible message, not a crash"): sqlite-net-pcl pools one native connection per database
/// path for the process's lifetime (<c>SQLiteConnectionPool</c>), so once it's open, deleting or
/// overwriting the file out from under it does not reliably surface as an exception from the next
/// query -- confirmed empirically while building this fix (a small scratch console app: inserting a
/// row, deleting the underlying file with the connection still open, then inserting another row and
/// even running <c>PRAGMA integrity_check</c> all still succeeded, because SQLite's own page cache
/// stays warm and satisfies small, already-cached databases like this app's without ever touching
/// the now-missing/corrupted bytes on disk again). Rather than depend on that unreliable, POSIX-vs-
/// Windows-inconsistent native behavior, this class checks the file's own existence up front, which
/// works the same way on every platform this app targets.
/// </para>
/// <para>
/// <b>Still also translates whatever SQLite/IO exception actually surfaces</b> (via the <c>catch</c>
/// below) for the failure modes that genuinely do throw from the driver -- e.g. the disk filling up
/// mid-write (<see cref="SQLite3.Result.Full"/>), a concurrent process holding a lock
/// (<see cref="SQLite3.Result.Busy"/>/<see cref="SQLite3.Result.Locked"/>), or a freshly-opened
/// connection hitting a corrupt file at startup (<see cref="SQLite3.Result.Corrupt"/>/
/// <see cref="SQLite3.Result.NonDBFile"/>) -- so both the "doesn't throw at all" and the "does
/// throw" cases end up with the same friendly, catchable exception type.
/// </para>
/// </remarks>
internal static class DatabaseExceptionTranslator
{
    public static async Task RunAsync(GmToolkitDatabase database, Func<Task> operation)
    {
        EnsureDatabaseFileExists(database);

        try
        {
            await operation().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not DataAccessException)
        {
            throw ToFriendly(ex);
        }
    }

    public static async Task<T> RunAsync<T>(GmToolkitDatabase database, Func<Task<T>> operation)
    {
        EnsureDatabaseFileExists(database);

        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not DataAccessException)
        {
            throw ToFriendly(ex);
        }
    }

    /// <summary>
    /// True only for the specific <see cref="SQLiteException.Result"/> values that indicate the
    /// database file's own bytes are damaged (<see cref="SQLite3.Result.Corrupt"/>/
    /// <see cref="SQLite3.Result.NonDBFile"/>/<see cref="SQLite3.Result.Format"/>) -- as opposed to
    /// transient failures (<see cref="SQLite3.Result.Busy"/>/<see cref="SQLite3.Result.Full"/>/
    /// <see cref="SQLite3.Result.ReadOnly"/>/etc.) or non-SQLite I/O failures, none of which say
    /// anything about whether the file is actually damaged. Used by
    /// <see cref="GmToolkitDatabase.CreateAndInitializeAsync"/> to decide whether a failure during
    /// startup/migration is grounds for destructively moving the existing file aside, or just a
    /// recoverable/transient condition that should be surfaced as an error without touching a
    /// possibly-perfectly-healthy database file.
    /// </summary>
    internal static bool IsCorruption(Exception ex) =>
        ex is SQLiteException { Result: SQLite3.Result.Corrupt or SQLite3.Result.NonDBFile or SQLite3.Result.Format };

    private static void EnsureDatabaseFileExists(GmToolkitDatabase database)
    {
        if (!File.Exists(database.DatabasePath))
        {
            throw new DataAccessException(
                "The database file is missing -- it may have been moved or deleted while GM Toolkit " +
                "was running. Restart GM Toolkit to create a fresh database; anything from this " +
                "session that wasn't already saved will be lost.");
        }
    }

    internal static DataAccessException ToFriendly(Exception ex) => ex switch
    {
        SQLiteException sqliteEx => new DataAccessException(FriendlyMessage(sqliteEx.Result), sqliteEx),
        UnauthorizedAccessException => new DataAccessException(
            "GM Toolkit doesn't have permission to access the database file. Check the file's " +
            "permissions and try again.", ex),
        IOException => new DataAccessException(
            "A disk error occurred while accessing the database file. Check that its drive is " +
            "connected, that the disk isn't full, and that no other program has the file open, " +
            "then try again.", ex),
        _ => new DataAccessException($"Something went wrong talking to the database: {ex.Message}", ex),
    };

    private static string FriendlyMessage(SQLite3.Result result) => result switch
    {
        SQLite3.Result.CannotOpen or SQLite3.Result.NotFound => "The database file couldn't be " +
            "opened -- it may have been moved or deleted. Restart GM Toolkit to create a fresh " +
            "database.",
        SQLite3.Result.Corrupt or SQLite3.Result.NonDBFile or SQLite3.Result.Format =>
            "The database file appears to be corrupted. Restart GM Toolkit, which will move the " +
            "damaged file aside and start a fresh database; the damaged file is kept in case its " +
            "data can be recovered later.",
        SQLite3.Result.Full => "There's no space left on disk to save this. Free up some space " +
            "and try again.",
        SQLite3.Result.Busy or SQLite3.Result.Locked or SQLite3.Result.LockErr =>
            "The database is locked, probably by another copy of GM Toolkit. Close any other " +
            "running copy and try again.",
        SQLite3.Result.ReadOnly or SQLite3.Result.Perm or SQLite3.Result.AccessDenied =>
            "GM Toolkit doesn't have permission to write to the database file. Check the file's " +
            "permissions and try again.",
        SQLite3.Result.IOError => "A disk error occurred while accessing the database. Check that " +
            "its drive is connected and try again.",
        _ => "Something went wrong talking to the database. Try again, and restart GM Toolkit if " +
            "this keeps happening.",
    };
}