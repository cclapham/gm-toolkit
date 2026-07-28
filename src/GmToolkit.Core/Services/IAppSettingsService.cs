namespace GmToolkit.Core.Services;

/// <summary>
/// Persists small, single-value app-wide settings that aren't tied to any one campaign -- today
/// just <see cref="ThemePreference"/> (issue #31).
/// </summary>
/// <remarks>
/// <para>
/// Defined here (rather than as a concrete class directly in <c>GmToolkit.Data</c>) purely
/// because of the dependency-direction rule: <c>GmToolkit.UI</c> needs to depend on this (its
/// <c>SettingsViewModel</c> reads/writes the theme preference) but cannot reference
/// <c>GmToolkit.Data</c> at all (only <c>Desktop</c>/<c>Android</c> -&gt; <c>UI</c> -&gt;
/// <c>Core</c> &lt;- <c>Data</c> is allowed). Same split as
/// <see cref="Repositories.ICampaignRepository"/>/<c>GmToolkit.Data.Repositories.CampaignRepository</c>
/// -- an interface here, a concrete implementation in <c>GmToolkit.Data</c>
/// (<c>GmToolkit.Data.AppSettingsService</c>), registered by
/// <c>GmToolkit.Data.ServiceCollectionExtensions.AddGmToolkitData</c>.
/// </para>
/// <para>
/// This is deliberately not a general-purpose key/value settings store: there is exactly one
/// setting today, so one strongly-typed method pair is more proportionate than a generic
/// <c>GetAsync&lt;T&gt;(string key)</c> API that would need to invent a schema for a store that
/// doesn't otherwise exist yet. See <c>GmToolkit.Data.AppSettingsService</c>'s remarks for where
/// and how this is actually persisted (a small JSON file next to the SQLite database, not a new
/// database table).
/// </para>
/// </remarks>
public interface IAppSettingsService
{
    /// <summary>Reads the persisted theme preference, or <see cref="ThemePreference.System"/> if
    /// none has ever been saved (first run) or the settings file is missing/corrupt -- this never
    /// throws, so a damaged settings file can never block startup (mirrors
    /// <c>GmToolkitDatabase</c>'s corrupt-file recovery philosophy from issue #12: a lost
    /// preference is a far smaller problem than an app that won't launch).</summary>
    Task<ThemePreference> GetThemePreferenceAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists <paramref name="preference"/> as the new theme preference.</summary>
    Task SetThemePreferenceAsync(ThemePreference preference, CancellationToken cancellationToken = default);
}