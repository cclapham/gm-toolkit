namespace GmToolkit.Data;

/// <summary>
/// Resolves the app-data database path for platforms that can rely on the portable BCL
/// <see cref="Environment.SpecialFolder"/> APIs (desktop: Windows and Linux).
/// </summary>
/// <remarks>
/// Android has no <see cref="Environment.SpecialFolder.LocalApplicationData"/> equivalent through
/// this API — its path must be resolved from <c>Android.App.Application.Context.FilesDir</c> in
/// the GmToolkit.Android project itself, not here, since GmToolkit.Data must stay free of Android
/// SDK types.
/// </remarks>
public static class AppDataPaths
{
    /// <summary>
    /// The database file name, shared across platforms (Android resolves its own containing
    /// directory but should still use this name for the file itself).
    /// </summary>
    public const string DatabaseFileName = "gmtoolkit.db";

    /// <summary>
    /// Returns the desktop app-data database path: <c>%LOCALAPPDATA%\GmToolkit\gmtoolkit.db</c>
    /// on Windows, <c>~/.local/share/GmToolkit/gmtoolkit.db</c> on Linux.
    /// </summary>
    public static string GetDesktopDatabasePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "GmToolkit", DatabaseFileName);
    }
}