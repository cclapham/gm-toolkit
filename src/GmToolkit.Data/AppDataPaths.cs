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
    /// The app-settings JSON file name (issue #31 -- see <see cref="AppSettingsService"/>), shared
    /// across platforms the same way <see cref="DatabaseFileName"/> is: it lives alongside the
    /// database in whichever directory each platform resolves.
    /// </summary>
    public const string SettingsFileName = "settings.json";

    /// <summary>
    /// Returns the desktop app-data database path: <c>%LOCALAPPDATA%\GmToolkit\gmtoolkit.db</c>
    /// on Windows, <c>~/.local/share/GmToolkit/gmtoolkit.db</c> on Linux.
    /// </summary>
    public static string GetDesktopDatabasePath() => Path.Combine(GetDesktopAppDataDirectory(), DatabaseFileName);

    /// <summary>
    /// Returns the desktop app-settings JSON file path: <c>%LOCALAPPDATA%\GmToolkit\settings.json</c>
    /// on Windows, <c>~/.local/share/GmToolkit/settings.json</c> on Linux -- the same directory
    /// <see cref="GetDesktopDatabasePath"/> resolves, since this is a small sidecar file next to
    /// the database, not a separate app-data location.
    /// </summary>
    public static string GetDesktopSettingsPath() => Path.Combine(GetDesktopAppDataDirectory(), SettingsFileName);

    private static string GetDesktopAppDataDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "GmToolkit");
    }
}