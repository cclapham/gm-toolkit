using System.Text.Json;
using System.Text.Json.Serialization;

using GmToolkit.Core.Services;

namespace GmToolkit.Data;

/// <summary>
/// <see cref="IAppSettingsService"/> implementation backed by a small JSON file living alongside
/// the SQLite database (see <see cref="AppDataPaths.SettingsFileName"/>/<see cref="AppDataPaths.GetDesktopSettingsPath"/>;
/// <c>GmToolkit.Android</c> resolves its own path the same way it resolves the database path).
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a JSON file next to the database, not a new SQLite table.</b> There is exactly one
/// setting today (<see cref="ThemePreference"/>), it is read once at startup and written rarely
/// (only when the user changes it in Settings), and it needs to be readable/writable before -- and
/// independent of -- the campaign database's own schema/migration story. A one-row settings table
/// would need a migration (<see cref="GmToolkitDatabase.SchemaVersion"/>) for a single column and
/// would tie a pure UI preference to the campaign-data database's lifecycle for no real benefit.
/// A tiny hand-written JSON file is simpler to reason about, trivially human-readable/editable, and
/// costs nothing extra to add a second setting to later if the file's shape needs it (bump
/// <see cref="SettingsFileVersion"/> the same way <see cref="GmToolkitDatabase.SchemaVersion"/>
/// does for the database, if that day comes).
/// </para>
/// <para>
/// <b>Corrupt/missing file never blocks startup</b> -- same "never a blank/frozen screen"
/// philosophy as <see cref="GmToolkitDatabase.CreateAndInitializeAsync"/>'s corrupt-file recovery
/// (issue #12), scaled down to fit a single preference: a missing file (first run) or a
/// file that fails to parse (hand-edited into invalid JSON, truncated by a crash mid-write, etc.)
/// both just fall back to <see cref="ThemePreference.System"/> rather than throwing. Unlike the
/// database, there's no sidecar file to move aside and no data loss risk worth surfacing to the
/// user for one lost preference -- the next successful <see cref="SetThemePreferenceAsync"/> call
/// overwrites the bad file with a valid one anyway.
/// </para>
/// <para>
/// <b>Writes go through a temp-file-then-move</b> so a process crash mid-write can never leave a
/// half-written (and therefore corrupt-on-next-read) <c>settings.json</c> behind -- the rename is
/// atomic on both Windows and Linux for a same-directory move.
/// </para>
/// </remarks>
public sealed class AppSettingsService : IAppSettingsService
{
    /// <summary>Bumped if <see cref="SettingsFile"/>'s shape ever changes in a way older files
    /// can't be read as -- unused today (nothing to migrate yet), but reserved for the same reason
    /// <see cref="GmToolkitDatabase.SchemaVersion"/> exists.</summary>
    public const int SettingsFileVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _settingsFilePath;

    public AppSettingsService(string settingsFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsFilePath);
        _settingsFilePath = settingsFilePath;
    }

    public async Task<ThemePreference> GetThemePreferenceAsync(CancellationToken cancellationToken = default)
    {
        var file = await ReadAsync(cancellationToken).ConfigureAwait(false);
        return file.ThemePreference;
    }

    public async Task SetThemePreferenceAsync(ThemePreference preference, CancellationToken cancellationToken = default)
    {
        await WriteAsync(new SettingsFile(SettingsFileVersion, preference), cancellationToken).ConfigureAwait(false);
    }

    private async Task<SettingsFile> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_settingsFilePath))
        {
            return SettingsFile.Default;
        }

        try
        {
            await using var stream = File.OpenRead(_settingsFilePath);
            var file = await JsonSerializer.DeserializeAsync<SettingsFile>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            return file ?? SettingsFile.Default;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Missing/corrupt/unreadable settings file: fall back to defaults rather than
            // throwing -- see this class's remarks.
            return SettingsFile.Default;
        }
    }

    private async Task WriteAsync(SettingsFile file, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(file, JsonOptions);

        var tempPath = $"{_settingsFilePath}.tmp-{Guid.NewGuid():N}";
        await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);
        File.Move(tempPath, _settingsFilePath, overwrite: true);
    }

    /// <summary>The on-disk JSON shape. Kept separate from <see cref="ThemePreference"/> itself
    /// (rather than serializing the enum directly) so the file has room to grow -- e.g. a future
    /// second setting, or <see cref="Version"/>-gated migration logic -- without a breaking format
    /// change, mirroring why SQLite row types live separately from Core domain models.</summary>
    private sealed record SettingsFile(int Version, ThemePreference ThemePreference)
    {
        public static SettingsFile Default { get; } = new(SettingsFileVersion, ThemePreference.System);
    }
}