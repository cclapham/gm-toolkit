namespace GmToolkit.Data.Tests;

/// <summary>
/// A <see cref="FactAttribute"/> that skips the test on Windows. Used for the handful of
/// <see cref="DatabaseExceptionTranslatorIntegrationTests"/> that simulate "the DB file disappears
/// while a live connection is open" via a real <see cref="File.Delete(string)"/> -- POSIX allows
/// unlinking a file that's still open (the data stays available to existing handles), but Windows
/// does not: sqlite-net-pcl's native connection holds the file without share-delete semantics, so
/// <see cref="File.Delete(string)"/> itself throws <see cref="IOException"/> before the test ever
/// reaches the code under test. That's a genuine, expected platform difference in how Windows
/// locks open files, not a bug in the test or in <see cref="DatabaseExceptionTranslator"/> -- see
/// that integration test class's remarks for the platform-agnostic tests that cover the same
/// translation logic without depending on this OS-specific behavior.
/// </summary>
public sealed class SkipOnWindowsFactAttribute : FactAttribute
{
    public SkipOnWindowsFactAttribute()
    {
        if (OperatingSystem.IsWindows())
        {
            Skip = "Deleting a file out from under a live SQLite connection throws IOException on " +
                "Windows instead of succeeding (no share-delete semantics on the native file handle) " +
                "-- see DatabaseExceptionTranslatorTests for the platform-agnostic coverage of the " +
                "same translation logic.";
        }
    }
}